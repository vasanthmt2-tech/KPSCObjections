-- Marks objections (KPSCMarksObjection).
--
-- One row per candidate, examination and paper: the remarks and the
-- optional supporting PDF. There is no fee, so a row is final once saved.
-- This app shares the KPSC_OBJECTIONS database with the answer key
-- objection portal, so its data is kept in its own table and the portal's
-- dbo.Objections is not touched. Safe to run more than once.
IF OBJECT_ID('dbo.MarksObjection', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MarksObjection
    (
        MarksObjectionId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_MarksObjection PRIMARY KEY,
        Regno            varchar(10)    NOT NULL,
        ExamCode         varchar(5)     NOT NULL,
        PaperCode        varchar(2)     NOT NULL,
        VersionCode      char(1)        NULL,
        Remarks          nvarchar(700)  NOT NULL,
        FileName         varchar(50)    NULL,
        Document         varbinary(max) NULL,
        SubmittedDate    datetime2(0)   NOT NULL CONSTRAINT DF_MarksObjection_Submitted DEFAULT (GETDATE()),
        IPAddress        varchar(45)    NOT NULL,
        CONSTRAINT UQ_MarksObjection_Paper UNIQUE (Regno, ExamCode, PaperCode)
    );

    CREATE INDEX IX_MarksObjection_Exam ON dbo.MarksObjection (ExamCode, PaperCode);
END
GO
