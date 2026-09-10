using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services.FollowerIdentity
{
    /// <summary>
    /// Authenticated follower is always a company <c>Users</c> row with an active FollowerProfile.
    /// Never a Delegates row.
    /// </summary>
    public sealed class FollowerUserIdentity
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string? AsyncId { get; set; }
        public string? UserType { get; set; }
        public int? CityId { get; set; }
        public string? CityName { get; set; }
        public bool IsActive { get; set; }
    }

    public interface IFollowerIdentityService
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task<FollowerUserIdentity?> ResolveByAsyncIdAsync(string? asyncId, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerUserIdentity>> ListActiveAsync(CancellationToken ct = default);
    }

    public sealed class FollowerIdentityService : IFollowerIdentityService
    {
        private readonly string _cs;

        public FollowerIdentityService(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DataBaseConnection")
                  ?? throw new InvalidOperationException("DataBaseConnection missing.");
        }

        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            await using var c = new SqlConnection(_cs);
            await c.OpenAsync(ct);
            await c.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
        }

        public async Task<FollowerUserIdentity?> ResolveByAsyncIdAsync(string? asyncId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(asyncId)) return null;
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            var row = await c.QueryFirstOrDefaultAsync<FollowerUserIdentity>(new CommandDefinition(@"
SELECT TOP 1
    u.UserID AS UserId,
    u.UserName,
    u.AsyncID AS AsyncId,
    u.UserType,
    fp.CityId,
    COALESCE(fp.CityName, ci.CityName) AS CityName,
    CAST(CASE WHEN fp.IsActive = 1 AND ISNULL(u.UserState, 1) = 1 THEN 1 ELSE 0 END AS BIT) AS IsActive
FROM dbo.Users u
INNER JOIN dbo.FollowerProfiles fp ON fp.UserId = u.UserID
LEFT JOIN dbo.Cities ci ON ci.CityID = fp.CityId
WHERE LTRIM(RTRIM(u.AsyncID)) = LTRIM(RTRIM(@AsyncId));",
                new { AsyncId = asyncId.Trim().TrimEnd('/') }, cancellationToken: ct));

            if (row is null || !row.IsActive || row.UserId <= 0) return null;
            return row;
        }

        public async Task<IReadOnlyList<FollowerUserIdentity>> ListActiveAsync(CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            var rows = await c.QueryAsync<FollowerUserIdentity>(new CommandDefinition(@"
SELECT
    u.UserID AS UserId,
    u.UserName,
    u.AsyncID AS AsyncId,
    u.UserType,
    fp.CityId,
    COALESCE(fp.CityName, ci.CityName) AS CityName,
    CAST(1 AS BIT) AS IsActive
FROM dbo.FollowerProfiles fp
INNER JOIN dbo.Users u ON u.UserID = fp.UserId
LEFT JOIN dbo.Cities ci ON ci.CityID = fp.CityId
WHERE fp.IsActive = 1 AND ISNULL(u.UserState, 1) = 1
ORDER BY u.UserName;", cancellationToken: ct));
            return rows.ToList();
        }

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.FollowerProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerProfiles (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_FollowerProfiles_IsActive DEFAULT(1),
        CityId INT NULL,
        CityName NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerProfiles_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId INT NULL,
        UpdatedAtUtc DATETIME2 NULL,
        CONSTRAINT UQ_FollowerProfiles_UserId UNIQUE (UserId),
        CONSTRAINT FK_FollowerProfiles_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserID)
    );
    CREATE INDEX IX_FollowerProfiles_Active ON dbo.FollowerProfiles (IsActive) WHERE IsActive = 1;
END;";
    }
}
