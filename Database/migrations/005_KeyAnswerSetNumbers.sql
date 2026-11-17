-- Renames the question paper set columns of dbo.KeyAnswers from SetA..SetL
-- to Set1..Set12. The columns hold the sets by position: Set1 is set A of
-- Paper I and set M of Paper II, Set2 is B or N, and so on up to Set12
-- (L or X). See KPSCObjections\Security\KeySets.cs.
--
-- Data is kept. The application from this version on needs the new names,
-- so run this before (or together with) deploying it. Safe to run more
-- than once: a column already renamed is skipped.
SET NOCOUNT ON;

DECLARE @Old TABLE (Position int, OldName sysname);
INSERT INTO @Old (Position, OldName) VALUES
    (1, 'SetA'), (2, 'SetB'), (3, 'SetC'), (4, 'SetD'), (5, 'SetE'), (6, 'SetF'),
    (7, 'SetG'), (8, 'SetH'), (9, 'SetI'), (10, 'SetJ'), (11, 'SetK'), (12, 'SetL');

DECLARE @Position int, @OldName sysname, @NewName sysname, @Path nvarchar(300);

DECLARE c CURSOR LOCAL FAST_FORWARD FOR SELECT Position, OldName FROM @Old ORDER BY Position;
OPEN c;
FETCH NEXT FROM c INTO @Position, @OldName;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @NewName = 'Set' + CAST(@Position AS varchar(2));

    IF COL_LENGTH('dbo.KeyAnswers', @OldName) IS NOT NULL
       AND COL_LENGTH('dbo.KeyAnswers', @NewName) IS NULL
    BEGIN
        SET @Path = N'dbo.KeyAnswers.' + @OldName;
        EXEC sp_rename @Path, @NewName, 'COLUMN';
    END

    FETCH NEXT FROM c INTO @Position, @OldName;
END

CLOSE c;
DEALLOCATE c;
GO
