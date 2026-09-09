/* ---------------------------------------------------------------------------
   0006  Notes
   Purpose : Free-text notes attached to a customer, order or invoice.
             Append-only; a wrong note is answered with another note.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Notes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notes
    (
        Id           INT            IDENTITY(1,1) NOT NULL,
        EntityType   VARCHAR(20)    NOT NULL,
        EntityId     INT            NOT NULL,
        Body         NVARCHAR(MAX)  NOT NULL,
        CreatedAtUtc DATETIME2(0)   NOT NULL CONSTRAINT DF_Notes_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedBy    NVARCHAR(100)  NOT NULL,

        CONSTRAINT PK_Notes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Notes_EntityType CHECK (EntityType IN ('Customer', 'Order', 'Invoice')),
        CONSTRAINT CK_Notes_Body CHECK (LEN(Body) > 0)
    );

    /* Notes are always read for one entity, newest first. No foreign key:
       the target lives in one of three tables, and the application enforces
       existence before insert. */
    CREATE NONCLUSTERED INDEX IX_Notes_Entity ON dbo.Notes (EntityType, EntityId, CreatedAtUtc DESC);
END
GO
