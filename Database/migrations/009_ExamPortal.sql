-- KPSC Examination Portal (KPSCExamPortal): one candidate list for all services.
--
-- The combined portal signs every candidate in from dbo.MarksCandidate (OTP to
-- its MobileNo) and files key answer objections for them too. dbo.PaymentCreate
-- checks the objection window through dbo.Candidate, the separate key answer
-- portal's list, and that portal still uses it, so it is left as it is; the
-- combined portal uses this copy, which checks dbo.MarksCandidate instead.
-- Safe to run again.

IF OBJECT_ID('dbo.ExamPortalPaymentCreate', 'P') IS NOT NULL DROP PROCEDURE dbo.ExamPortalPaymentCreate;
GO

CREATE PROCEDURE dbo.ExamPortalPaymentCreate
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
    -- the same moment cannot both take the same drafts.
    EXEC sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive',
                       @LockOwner = 'Transaction', @LockTimeout = 10000;

    IF NOT EXISTS (SELECT 1
                   FROM dbo.MarksCandidate c
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
