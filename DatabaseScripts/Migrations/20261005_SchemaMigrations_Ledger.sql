-- Branch schema migration ledger (apply once per branch DB).
IF OBJECT_ID(N'dbo.__SchemaMigrations', N'U') IS NULL
BEGIN
  CREATE TABLE dbo.__SchemaMigrations (
    MigrationId NVARCHAR(128) NOT NULL PRIMARY KEY,
    AppliedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SchemaMigrations_Applied DEFAULT (SYSUTCDATETIME()),
    Checksum NVARCHAR(64) NOT NULL,
    Notes NVARCHAR(400) NULL
  );
END
GO

-- Example registration after applying a migration file (ops):
-- INSERT INTO dbo.__SchemaMigrations (MigrationId, Checksum, Notes)
-- VALUES (N'20261005_CustomersSales_Create_ScopeIdentity_Txn', N'<sha256>', N'F07 sales create');
-- Do not INSERT here without actually applying the corresponding CREATE OR ALTER script.
GO
