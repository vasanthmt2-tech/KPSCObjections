-- Lets a candidate pay for new objections while an earlier payment is still
-- being verified. Each payment covers only the objections that were drafts
-- when it started, so two payments never cover the same objection, and a
-- failed payment still hands its own objections back as drafts.
-- Safe to run more than once. Written for SQL Server 2014.

------------------------------------------------------------------------
-- Start a payment for all of a candidate's draft objections
--    Result: OK | WINDOW_CLOSED | NO_DRAFTS
------------------------------------------------------------------------
IF OBJECT_ID('dbo.PaymentCreate', 'P') IS NOT NULL DROP PROCEDURE dbo.PaymentCreate;
GO
CREATE PROCEDURE dbo.PaymentCreate
    @RegNo           varchar(10),
    @MerchantOrderNo varchar(50),
    @FeePerObjection decimal(10, 2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Resource nvarchar(255) = N'KPSC_PAY_' + @RegNo;
    DECLARE @Count int, @PaymentId bigint, @Amount decimal(10, 2);

    BEGIN TRAN;

    -- Payments of one candidate are created one at a time, so two clicks at
    -- the same moment cannot both take the same drafts. Earlier payments that
    -- are still being verified do not block a new one: they keep their own
    -- objections, and only the current drafts go into the new payment.
    EXEC sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive',
                       @LockOwner = 'Transaction', @LockTimeout = 10000;

    IF NOT EXISTS (SELECT 1
                   FROM dbo.Candidate c
                   INNER JOIN dbo.Exam e ON e.ExamCode = LTRIM(RTRIM(c.ExamCode))
                   WHERE c.RegNo = @RegNo
                     AND (e.ObjectionStart IS NULL OR e.ObjectionStart <= GETDATE())
                     AND (e.ObjectionEnd   IS NULL OR e.ObjectionEnd   >= GETDATE()))
    BEGIN
        COMMIT;
        SELECT 'WINDOW_CLOSED' AS Result, CAST(NULL AS decimal(10, 2)) AS Amount;
        RETURN;
    END

    SELECT @Count = COUNT(*) FROM dbo.Objections WHERE Regno = @RegNo AND PaymentId IS NULL;

    IF @Count = 0
    BEGIN
        COMMIT;
        SELECT 'NO_DRAFTS' AS Result, CAST(NULL AS decimal(10, 2)) AS Amount;
        RETURN;
    END

    SET @Amount = @Count * @FeePerObjection;

    INSERT INTO dbo.PaymentTransactions (RegNo, MerchantOrderNo, Amount, PaymentStatus, PaymentInitiatedAt)
    VALUES (@RegNo, @MerchantOrderNo, @Amount, 'PENDING', GETDATE());

    SET @PaymentId = SCOPE_IDENTITY();

    UPDATE dbo.Objections SET PaymentId = @PaymentId WHERE Regno = @RegNo AND PaymentId IS NULL;

    COMMIT;

    SELECT 'OK' AS Result, @Amount AS Amount;
END
GO
