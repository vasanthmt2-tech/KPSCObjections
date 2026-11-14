-- Admin accounts for the objection portal.
-- Passwords are stored as PBKDF2-HMAC-SHA256 hashes; create users with
-- Database\New-AdminUser.ps1, never by inserting a password directly.
IF OBJECT_ID('dbo.AdminUsers', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminUsers
    (
        UserName        varchar(50)   NOT NULL CONSTRAINT PK_AdminUsers PRIMARY KEY,
        PasswordHash    varbinary(32) NOT NULL,
        PasswordSalt    varbinary(16) NOT NULL,
        Iterations      int           NOT NULL,
        IsActive        bit           NOT NULL CONSTRAINT DF_AdminUsers_IsActive DEFAULT (1),
        FailedAttempts  int           NOT NULL CONSTRAINT DF_AdminUsers_Failed DEFAULT (0),
        LockoutUntil    datetime2(0)  NULL,
        LastLoginAt     datetime2(0)  NULL,
        CreatedDate     datetime2(0)  NOT NULL CONSTRAINT DF_AdminUsers_Created DEFAULT (GETDATE())
    );
END
GO
