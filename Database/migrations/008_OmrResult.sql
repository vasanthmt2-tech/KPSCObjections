-- OMR result portal (KPSCOmrResult).
--
-- Candidates sign in with the marks portal's candidates (dbo.MarksCandidate)
-- and see their result for each paper, with the key answer and their
-- marked answer for every question. The portal keeps the final key in its
-- own tables, so the final key (with GRACE and several accepted answers
-- such as 3/4) never overwrites the provisional key of the answer key
-- objection portal (dbo.KeyAnswers). Safe to run more than once; runs on
-- SQL Server 2014.

-- The final key: same layout as dbo.KeyAnswers (master question, key
-- answer, the question number in each of the paper's 12 sets by position),
-- but the key answer is text: "2", "3/4" or "GRACE".
IF OBJECT_ID('dbo.OmrKeyAnswers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OmrKeyAnswers
    (
        KeyAnswerId  bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_OmrKeyAnswers PRIMARY KEY,
        ExamCode     varchar(5)   NOT NULL,
        SubjectCode  varchar(5)   NOT NULL,
        QuestionNo   int          NOT NULL,
        MasterAnswer varchar(20)  NULL,
        Set1  varchar(20) NULL, Set2  varchar(20) NULL, Set3  varchar(20) NULL,
        Set4  varchar(20) NULL, Set5  varchar(20) NULL, Set6  varchar(20) NULL,
        Set7  varchar(20) NULL, Set8  varchar(20) NULL, Set9  varchar(20) NULL,
        Set10 varchar(20) NULL, Set11 varchar(20) NULL, Set12 varchar(20) NULL,
        CreatedDate  datetime2    NOT NULL CONSTRAINT DF_OmrKeyAnswers_Created DEFAULT (GETDATE()),
        CONSTRAINT UQ_OmrKeyAnswers_Exam_Subject_Question UNIQUE (ExamCode, SubjectCode, QuestionNo)
    );
END
GO

-- The uploaded final key files (one per examination and paper).
IF OBJECT_ID('dbo.OmrUploadedKeyAnswerDocuments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OmrUploadedKeyAnswerDocuments
    (
        DocumentId  int IDENTITY(1,1) NOT NULL CONSTRAINT PK_OmrUploadedKeyAnswerDocuments PRIMARY KEY,
        ExamCode    varchar(50)    NOT NULL,
        SubjectCode varchar(5)     NOT NULL,
        FileName    nvarchar(255)  NOT NULL,
        FilePath    nvarchar(500)  NOT NULL,
        UploadedOn  datetime       NOT NULL CONSTRAINT DF_OmrUploadedKeyAnswerDocuments_UploadedOn DEFAULT (GETDATE())
    );
END
GO

-- One row per candidate per paper, in the layout of the OMR result file:
-- S.No, RegNo, Version, Barcode, Paper, Dist, Centre, Q1..Q100, Cr., InCr.,
-- NA., LeftBlank, MarksObt., Rank. The columns have the file's names without
-- the dots, except Cr. and InCr., which are Correct and Incorrect. Q1..Q100 hold the answer the candidate marked in the question
-- order of their version: 1 to 4, 5 = not attempted, NULL = left blank.
-- A registration number belongs to one examination (dbo.MarksCandidate),
-- so the table has no examination column.

-- An earlier draft of this script kept the 100 answers in one column. If that
-- table is still there and empty, it is replaced by this layout.
-- The emptiness check runs as dynamic SQL: SQL Server evaluates every part of
-- an IF condition, so a direct SELECT fails on a database without the table.
IF OBJECT_ID('dbo.OmrResult', 'U') IS NOT NULL
   AND COL_LENGTH('dbo.OmrResult', 'Answers') IS NOT NULL
    EXEC('IF NOT EXISTS (SELECT 1 FROM dbo.OmrResult) DROP TABLE dbo.OmrResult;');
GO

IF OBJECT_ID('dbo.OmrResult', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.OmrResult
    (
        SNo       int           NULL,
        RegNo     varchar(10)   NOT NULL,
        Version   char(1)       NOT NULL,
        Barcode   varchar(20)   NULL,
        Paper     varchar(2)    NOT NULL,
        Dist      nvarchar(100) NULL,
        Centre    nvarchar(200) NULL,
        Q1   varchar(5)  NULL,
        Q2   varchar(5)  NULL,
        Q3   varchar(5)  NULL,
        Q4   varchar(5)  NULL,
        Q5   varchar(5)  NULL,
        Q6   varchar(5)  NULL,
        Q7   varchar(5)  NULL,
        Q8   varchar(5)  NULL,
        Q9   varchar(5)  NULL,
        Q10  varchar(5)  NULL,
        Q11  varchar(5)  NULL,
        Q12  varchar(5)  NULL,
        Q13  varchar(5)  NULL,
        Q14  varchar(5)  NULL,
        Q15  varchar(5)  NULL,
        Q16  varchar(5)  NULL,
        Q17  varchar(5)  NULL,
        Q18  varchar(5)  NULL,
        Q19  varchar(5)  NULL,
        Q20  varchar(5)  NULL,
        Q21  varchar(5)  NULL,
        Q22  varchar(5)  NULL,
        Q23  varchar(5)  NULL,
        Q24  varchar(5)  NULL,
        Q25  varchar(5)  NULL,
        Q26  varchar(5)  NULL,
        Q27  varchar(5)  NULL,
        Q28  varchar(5)  NULL,
        Q29  varchar(5)  NULL,
        Q30  varchar(5)  NULL,
        Q31  varchar(5)  NULL,
        Q32  varchar(5)  NULL,
        Q33  varchar(5)  NULL,
        Q34  varchar(5)  NULL,
        Q35  varchar(5)  NULL,
        Q36  varchar(5)  NULL,
        Q37  varchar(5)  NULL,
        Q38  varchar(5)  NULL,
        Q39  varchar(5)  NULL,
        Q40  varchar(5)  NULL,
        Q41  varchar(5)  NULL,
        Q42  varchar(5)  NULL,
        Q43  varchar(5)  NULL,
        Q44  varchar(5)  NULL,
        Q45  varchar(5)  NULL,
        Q46  varchar(5)  NULL,
        Q47  varchar(5)  NULL,
        Q48  varchar(5)  NULL,
        Q49  varchar(5)  NULL,
        Q50  varchar(5)  NULL,
        Q51  varchar(5)  NULL,
        Q52  varchar(5)  NULL,
        Q53  varchar(5)  NULL,
        Q54  varchar(5)  NULL,
        Q55  varchar(5)  NULL,
        Q56  varchar(5)  NULL,
        Q57  varchar(5)  NULL,
        Q58  varchar(5)  NULL,
        Q59  varchar(5)  NULL,
        Q60  varchar(5)  NULL,
        Q61  varchar(5)  NULL,
        Q62  varchar(5)  NULL,
        Q63  varchar(5)  NULL,
        Q64  varchar(5)  NULL,
        Q65  varchar(5)  NULL,
        Q66  varchar(5)  NULL,
        Q67  varchar(5)  NULL,
        Q68  varchar(5)  NULL,
        Q69  varchar(5)  NULL,
        Q70  varchar(5)  NULL,
        Q71  varchar(5)  NULL,
        Q72  varchar(5)  NULL,
        Q73  varchar(5)  NULL,
        Q74  varchar(5)  NULL,
        Q75  varchar(5)  NULL,
        Q76  varchar(5)  NULL,
        Q77  varchar(5)  NULL,
        Q78  varchar(5)  NULL,
        Q79  varchar(5)  NULL,
        Q80  varchar(5)  NULL,
        Q81  varchar(5)  NULL,
        Q82  varchar(5)  NULL,
        Q83  varchar(5)  NULL,
        Q84  varchar(5)  NULL,
        Q85  varchar(5)  NULL,
        Q86  varchar(5)  NULL,
        Q87  varchar(5)  NULL,
        Q88  varchar(5)  NULL,
        Q89  varchar(5)  NULL,
        Q90  varchar(5)  NULL,
        Q91  varchar(5)  NULL,
        Q92  varchar(5)  NULL,
        Q93  varchar(5)  NULL,
        Q94  varchar(5)  NULL,
        Q95  varchar(5)  NULL,
        Q96  varchar(5)  NULL,
        Q97  varchar(5)  NULL,
        Q98  varchar(5)  NULL,
        Q99  varchar(5)  NULL,
        Q100 varchar(5)  NULL,
        Correct   int           NOT NULL,
        Incorrect int           NOT NULL,
        NA        int           NOT NULL,
        LeftBlank int           NOT NULL,
        MarksObt  decimal(7,2)  NOT NULL,
        [Rank]    int           NULL,
        CONSTRAINT PK_OmrResult PRIMARY KEY (RegNo, Paper)
    );
END
GO
