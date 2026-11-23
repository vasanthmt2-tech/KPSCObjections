-- Marks objection candidates (KPSCMarksObjection).
--
-- The marks app shares the KPSC_OBJECTIONS database with the answer key
-- objection portal, so its candidates are kept in their own table.
-- Uploading or deleting candidates in one app never touches the other
-- app's candidates. Safe to run more than once.
--
-- Candidates log in with registration number, examination and date of
-- birth (Login.aspx.cs). FailedLogins counts wrong dates of birth in a row;
-- after CandidateLoginMaxFailures (Web.config) the registration number is
-- locked until LockedUntil.

-- Same columns as dbo.Candidate, plus each paper's answer sheet barcode and
-- the login lockout. Two Excel files fill it (UploadCandidates.aspx.cs):
-- the candidate file (application number, hall ticket number = RegNo,
-- name, date of birth) and the version code file (RegNo, version, barcode
-- and paper, one row per candidate per paper).
IF OBJECT_ID('dbo.MarksCandidate', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MarksCandidate
    (
        Applno        varchar(25)  NOT NULL,
        RegNo         varchar(10)  NOT NULL CONSTRAINT PK_MarksCandidate PRIMARY KEY,
        Name          varchar(200) NULL,
        Dob           varchar(10)  NULL,
        MobileNo      varchar(10)  NULL,
        P1VersionCode char(1)      NULL,
        P2VersionCode char(1)      NULL,
        ExamCode      char(1)      NULL,
        P1Barcode     varchar(20)  NULL,
        P2Barcode     varchar(20)  NULL,
        FailedLogins  int          NOT NULL CONSTRAINT DF_MarksCandidate_FailedLogins DEFAULT (0),
        LockedUntil   datetime2(0) NULL
    );

    CREATE INDEX IX_MarksCandidate_Exam ON dbo.MarksCandidate (ExamCode);
END
GO

-- For a database where an earlier draft of this script created the table
-- without the lockout columns.
IF COL_LENGTH('dbo.MarksCandidate', 'FailedLogins') IS NULL
    ALTER TABLE dbo.MarksCandidate
        ADD FailedLogins int NOT NULL CONSTRAINT DF_MarksCandidate_FailedLogins DEFAULT (0);
GO

IF COL_LENGTH('dbo.MarksCandidate', 'LockedUntil') IS NULL
    ALTER TABLE dbo.MarksCandidate ADD LockedUntil datetime2(0) NULL;
GO

-- Answer sheet barcodes, one per paper, from the version code file.
IF COL_LENGTH('dbo.MarksCandidate', 'P1Barcode') IS NULL
    ALTER TABLE dbo.MarksCandidate ADD P1Barcode varchar(20) NULL;
GO

IF COL_LENGTH('dbo.MarksCandidate', 'P2Barcode') IS NULL
    ALTER TABLE dbo.MarksCandidate ADD P2Barcode varchar(20) NULL;
GO

-- An earlier draft also created OTP objects; login no longer uses an OTP.
IF OBJECT_ID('dbo.MarksUpdateCandidateOTP', 'P') IS NOT NULL
    DROP PROCEDURE dbo.MarksUpdateCandidateOTP;
GO

IF OBJECT_ID('dbo.MarksCandidateOTP', 'U') IS NOT NULL
    DROP TABLE dbo.MarksCandidateOTP;
GO
