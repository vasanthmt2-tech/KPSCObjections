-- Separate objection windows for answer key objections and marks objections.
--
-- dbo.Exam.ObjectionStart / ObjectionEnd stay the answer key objection window
-- (pages, payment procedures and the separate KPSCObjections portal are
-- unchanged). The new MarksObjectionStart / MarksObjectionEnd are the marks
-- objection window used by the Examination Portal.
--
-- When the columns are added, each examination's marks window starts as a copy
-- of its current window, so nothing changes for candidates on the day this is
-- run. Safe to run again: an existing marks window is never overwritten.

SET XACT_ABORT ON;
GO

IF COL_LENGTH('dbo.Exam', 'MarksObjectionStart') IS NULL
BEGIN
    BEGIN TRAN;

    ALTER TABLE dbo.Exam ADD MarksObjectionStart datetime2(0) NULL, MarksObjectionEnd datetime2(0) NULL;

    -- Dynamic SQL: the new columns are not visible to this batch's compiled statements.
    EXEC (N'UPDATE dbo.Exam SET MarksObjectionStart = ObjectionStart, MarksObjectionEnd = ObjectionEnd;');

    COMMIT;
END
GO
