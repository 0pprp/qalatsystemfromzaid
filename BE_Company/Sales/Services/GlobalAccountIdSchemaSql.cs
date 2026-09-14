namespace BE_Company.Sales.Services;

/// <summary>
/// Shared idempotent DDL for dbo.Users.GlobalAccountId. Safe for multiple NULLs (non-unique filtered index).
/// </summary>
public static class GlobalAccountIdSchemaSql
{
    public const bool UsesUniqueFilteredIndex = true;

    public const string EnsureScript = """
SET XACT_ABORT ON;
BEGIN TRAN;
IF COL_LENGTH(N'dbo.Users', N'GlobalAccountId') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD GlobalAccountId UNIQUEIDENTIFIER NULL;
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_GlobalAccountId' AND object_id = OBJECT_ID(N'dbo.Users'))
BEGIN
    -- UNIQUE filtered: multiple NULL values allowed; non-null GlobalAccountId must be unique per DB.
    CREATE UNIQUE NONCLUSTERED INDEX IX_Users_GlobalAccountId
        ON dbo.Users(GlobalAccountId)
        WHERE GlobalAccountId IS NOT NULL;
END
COMMIT;
""";

    public const string VerifyScript = """
SELECT
  DB_NAME() AS DatabaseName,
  CASE WHEN COL_LENGTH(N'dbo.Users', N'GlobalAccountId') IS NULL THEN 0 ELSE 1 END AS ColumnExists,
  TYPE_NAME(system_type_id) AS ColumnType,
  is_nullable AS IsNullable,
  CASE WHEN EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_GlobalAccountId' AND object_id = OBJECT_ID(N'dbo.Users')
  ) THEN 1 ELSE 0 END AS IndexExists
FROM sys.columns
WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'GlobalAccountId';
""";
}
