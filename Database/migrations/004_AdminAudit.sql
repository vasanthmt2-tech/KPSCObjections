-- Audit trail of admin actions (sign-ins, uploads, deletes, downloads,
-- objection window changes, payment re-checks). Written by
-- Security\AdminAudit.cs and shown on the admin Audit Trail page.
--
-- Rows can only be added: a trigger refuses UPDATE and DELETE, so the
-- application (or anyone using its database login) cannot rewrite or
-- remove the trail. A DBA can still archive old rows by disabling the
-- trigger deliberately. Safe to run more than once.
IF OBJECT_ID('dbo.AdminAudit', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminAudit
    (
        AuditId     bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AdminAudit PRIMARY KEY,
        AuditTime   datetime2(0)   NOT NULL CONSTRAINT DF_AdminAudit_Time DEFAULT (GETDATE()),
        AdminUser   varchar(50)    NOT NULL,
        Action      varchar(50)    NOT NULL,
        Details     nvarchar(1000) NULL,
        ClientIp    varchar(45)    NULL
    );

    CREATE INDEX IX_AdminAudit_Time ON dbo.AdminAudit (AuditTime DESC);
END
GO

IF OBJECT_ID('dbo.TR_AdminAudit_NoChange', 'TR') IS NULL
    EXEC ('CREATE TRIGGER dbo.TR_AdminAudit_NoChange ON dbo.AdminAudit
           INSTEAD OF UPDATE, DELETE
           AS
           BEGIN
               SET NOCOUNT ON;
               THROW 50001, ''The admin audit trail cannot be changed or deleted.'', 1;
           END');
GO
