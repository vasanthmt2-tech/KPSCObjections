-- Objection remarks up to 2000 characters (was 700).
--
-- Widens dbo.Objections.Remarks and dbo.MarksObjection.Remarks to
-- nvarchar(2000) and the @Remarks parameter of dbo.ObjectionsInsert to match.
-- Widening keeps every existing row as it is. Safe to run again.

SET XACT_ABORT ON;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.Objections') AND name = 'Remarks' AND max_length < 4000)
    ALTER TABLE dbo.Objections ALTER COLUMN Remarks nvarchar(2000) NOT NULL;
GO

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.MarksObjection') AND name = 'Remarks' AND max_length < 4000)
    ALTER TABLE dbo.MarksObjection ALTER COLUMN Remarks nvarchar(2000) NOT NULL;
GO

ALTER PROCEDURE dbo.ObjectionsInsert
(
    @Regno         varchar(10),
    @PaperCode     varchar(2),
    @VersionCode   char(1),
    @QuestionNo    int,
    @KeyAnswer     varchar(10),
    @ObjectionAns  varchar(10),
    @CategoryCode  int,
    @Remarks       nvarchar(2000),
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
