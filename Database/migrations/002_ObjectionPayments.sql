-- Links objections to payments, adds the objection window, and makes the
-- objection save transactional. Safe to run more than once.
-- Written for SQL Server 2014 (no CREATE OR ALTER / DROP IF EXISTS).

------------------------------------------------------------------------
-- 1. Objection window per examination
------------------------------------------------------------------------
IF COL_LENGTH('dbo.Exam', 'ObjectionStart') IS NULL
    ALTER TABLE dbo.Exam ADD ObjectionStart datetime2(0) NULL, ObjectionEnd datetime2(0) NULL;
GO

-- Window for examination 1: 16-Nov-2026 3:00 PM to 18-Nov-2026 11:59 PM.
-- A NULL start or end means "no limit" on that side.
UPDATE dbo.Exam
SET ObjectionStart = '2026-11-16T15:00:00',
    ObjectionEnd   = '2026-11-18T23:59:59'
WHERE ExamCode = '1' AND ObjectionStart IS NULL AND ObjectionEnd IS NULL;
GO

------------------------------------------------------------------------
-- 2. Objection -> payment link
--    PaymentId NULL                 : draft, not yet paid for
--    PaymentId -> PENDING payment   : payment started, awaiting verification
--    PaymentId -> SUCCESS payment   : submitted
--    (a FAILED payment releases its objections back to draft)
------------------------------------------------------------------------
IF COL_LENGTH('dbo.Objections', 'PaymentId') IS NULL
    ALTER TABLE dbo.Objections ADD PaymentId bigint NULL
        CONSTRAINT FK_Objections_Payment REFERENCES dbo.PaymentTransactions (PaymentId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Objections_PaymentId' AND object_id = OBJECT_ID('dbo.Objections'))
    CREATE NONCLUSTERED INDEX IX_Objections_PaymentId ON dbo.Objections (PaymentId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PaymentTransactions_RegNo' AND object_id = OBJECT_ID('dbo.PaymentTransactions'))
    CREATE NONCLUSTERED INDEX IX_PaymentTransactions_RegNo ON dbo.PaymentTransactions (RegNo, PaymentStatus);
GO

-- Room for IPv6 addresses.
ALTER TABLE dbo.Objections ALTER COLUMN IPAddress varchar(45) NOT NULL;
ALTER TABLE dbo.ObjectionsDocument ALTER COLUMN IPAddress varchar(45) NOT NULL;
GO

------------------------------------------------------------------------
-- 3. Save one draft objection with its document, all or nothing
------------------------------------------------------------------------
IF OBJECT_ID('dbo.ObjectionsInsert', 'P') IS NOT NULL DROP PROCEDURE dbo.ObjectionsInsert;
GO
CREATE PROCEDURE dbo.ObjectionsInsert
(
    @Regno         varchar(10),
    @PaperCode     varchar(2),
    @VersionCode   char(1),
    @QuestionNo    int,
    @KeyAnswer     varchar(10),
    @ObjectionAns  varchar(10),
    @CategoryCode  int,
    @Remarks       nvarchar(700),
    @FileName      varchar(50),
    @ObjectionDocs varbinary(max),
    @User          varchar(50),
    @IPAddress     varchar(45)
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRAN;

        IF EXISTS (SELECT 1 FROM dbo.Objections WITH (UPDLOCK, HOLDLOCK)
                   WHERE Regno = @Regno AND PaperCode = @PaperCode AND QuestionNo = @QuestionNo)
        BEGIN
            ROLLBACK;
            SELECT 'Data already exist.' AS Result;
            RETURN;
        END

        INSERT INTO dbo.Objections
            (Regno, PaperCode, VersionCode, QuestionNo, KeyAnswer, ObjectionAnswer,
             CategoryCode, Remarks, CreatedUser, CreatedDate, IPAddress)
        VALUES
            (@Regno, @PaperCode, @VersionCode, @QuestionNo, @KeyAnswer, @ObjectionAns,
             @CategoryCode, @Remarks, @User, GETDATE(), @IPAddress);

        INSERT INTO dbo.ObjectionsDocument
            (Regno, PaperCode, VersionCode, QuestionNo, FileName, ObjectionDocs,
             CreatedUser, CreatedDate, IPAddress)
        VALUES
            (@Regno, @PaperCode, @VersionCode, @QuestionNo, @FileName, @ObjectionDocs,
             @User, GETDATE(), @IPAddress);

        COMMIT;
        SELECT 'S' AS Result;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        SELECT ERROR_MESSAGE() AS Result;
    END CATCH
END
GO

------------------------------------------------------------------------
-- 4. Start a payment for all of a candidate's draft objections
--    Result: OK | PENDING_EXISTS | WINDOW_CLOSED | NO_DRAFTS
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

    -- One payment at a time per candidate.
    EXEC sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive',
                       @LockOwner = 'Transaction', @LockTimeout = 10000;

    IF EXISTS (SELECT 1 FROM dbo.PaymentTransactions WHERE RegNo = @RegNo AND PaymentStatus = 'PENDING')
    BEGIN
        COMMIT;
        SELECT 'PENDING_EXISTS' AS Result, CAST(NULL AS decimal(10, 2)) AS Amount;
        RETURN;
    END

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

------------------------------------------------------------------------
-- 5. Record the verified outcome of a payment. Only a PENDING payment
--    changes, so calling this twice for the same order is harmless.
--    @ResultStatus: SUCCESS | FAILED | PENDING (still unknown)
--    Returns the payment's status after the call.
------------------------------------------------------------------------
IF OBJECT_ID('dbo.PaymentApplyResult', 'P') IS NOT NULL DROP PROCEDURE dbo.PaymentApplyResult;
GO
CREATE PROCEDURE dbo.PaymentApplyResult
    @MerchantOrderNo varchar(50),
    @ResultStatus    varchar(20),
    @SBIReferenceNo  varchar(100) = NULL,
    @PaymentMode     varchar(30) = NULL,
    @PaymentDate     datetime2(0) = NULL,
    @GatewayResponse nvarchar(max) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ResultStatus NOT IN ('SUCCESS', 'FAILED', 'PENDING')
        THROW 50010, 'Invalid payment result status.', 1;

    DECLARE @PaymentId bigint;

    BEGIN TRAN;

    SELECT @PaymentId = PaymentId
    FROM dbo.PaymentTransactions WITH (UPDLOCK, ROWLOCK)
    WHERE MerchantOrderNo = @MerchantOrderNo AND PaymentStatus = 'PENDING';

    IF @PaymentId IS NOT NULL
    BEGIN
        UPDATE dbo.PaymentTransactions
        SET PaymentStatus             = @ResultStatus,
            SBIReferenceNo            = COALESCE(@SBIReferenceNo, SBIReferenceNo),
            PaymentMode               = COALESCE(@PaymentMode, PaymentMode),
            PaymentDate               = COALESCE(@PaymentDate, PaymentDate),
            GatewayResponse           = COALESCE(@GatewayResponse, GatewayResponse),
            VerificationAttempts      = VerificationAttempts + 1,
            LastVerificationAttemptAt = GETDATE(),
            VerifiedDate              = CASE WHEN @ResultStatus IN ('SUCCESS', 'FAILED') THEN GETDATE() ELSE VerifiedDate END
        WHERE PaymentId = @PaymentId;

        -- A failed payment hands its objections back as drafts so the
        -- candidate can pay again without re-entering them.
        IF @ResultStatus = 'FAILED'
            UPDATE dbo.Objections SET PaymentId = NULL WHERE PaymentId = @PaymentId;
    END

    COMMIT;

    SELECT PaymentStatus FROM dbo.PaymentTransactions WHERE MerchantOrderNo = @MerchantOrderNo;
END
GO
