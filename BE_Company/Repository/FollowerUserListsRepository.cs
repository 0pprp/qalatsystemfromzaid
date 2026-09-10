using BE_Company.DTO;
using BE_Company.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Repository
{
    public sealed class FollowerUserListsRepository : IFollowerUserListsRepository
    {
        private readonly string _cs;
        private static int _schemaReady;

        public FollowerUserListsRepository(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DataBaseConnection")
                  ?? throw new InvalidOperationException("DataBaseConnection missing.");
        }

        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            if (Volatile.Read(ref _schemaReady) == 1) return;
            await using var c = new SqlConnection(_cs);
            await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerUserLists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerUserLists (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerUserLists PRIMARY KEY,
        UserId INT NOT NULL,
        ListId INT NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerUserLists_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_FollowerUserLists_User_List UNIQUE (UserId, ListId)
    );
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_FollowerUserLists_UserId'
      AND object_id = OBJECT_ID(N'dbo.FollowerUserLists')
)
    CREATE INDEX IX_FollowerUserLists_UserId ON dbo.FollowerUserLists (UserId) INCLUDE (ListId);
", cancellationToken: ct));
            Volatile.Write(ref _schemaReady, 1);
        }

        public async Task<IReadOnlyList<FollowerListOptionDTO>> GetAvailableListsAsync(CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            var rows = await c.QueryAsync<FollowerListOptionDTO>(new CommandDefinition(@"
SELECT
    d.DelegateID AS ListId,
    d.DelegateName AS ListName,
    d.ReceiptName,
    d.CityID AS CityId,
    ci.CityName
FROM dbo.Delegates d
LEFT JOIN dbo.Cities ci ON ci.CityID = d.CityID
WHERE ISNULL(d.DevicePaymentState, 0) = 1
ORDER BY d.DelegateName;", cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<IReadOnlyList<int>> GetAssignedListIdsAsync(int userId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            var ids = await c.QueryAsync<int>(new CommandDefinition(@"
SELECT ListId FROM dbo.FollowerUserLists WHERE UserId = @UserId ORDER BY ListId;",
                new { UserId = userId }, cancellationToken: ct));
            return ids.ToList();
        }

        public async Task ReplaceAssignmentsAsync(int userId, IEnumerable<int> listIds, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            var distinct = listIds.Where(id => id > 0).Distinct().ToList();

            await using var c = new SqlConnection(_cs);
            await c.OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);

            try
            {
                await c.ExecuteAsync(new CommandDefinition(
                    "DELETE FROM dbo.FollowerUserLists WHERE UserId = @UserId;",
                    new { UserId = userId },
                    transaction: tx,
                    cancellationToken: ct));

                foreach (var listId in distinct)
                {
                    await c.ExecuteAsync(new CommandDefinition(@"
IF EXISTS (SELECT 1 FROM dbo.Delegates WHERE DelegateID = @ListId)
AND NOT EXISTS (SELECT 1 FROM dbo.FollowerUserLists WHERE UserId = @UserId AND ListId = @ListId)
    INSERT INTO dbo.FollowerUserLists (UserId, ListId) VALUES (@UserId, @ListId);",
                        new { UserId = userId, ListId = listId },
                        transaction: tx,
                        cancellationToken: ct));
                }

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        public async Task ClearAssignmentsAsync(int userId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            await c.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.FollowerUserLists WHERE UserId = @UserId;",
                new { UserId = userId },
                cancellationToken: ct));
        }

        /// <summary>
        /// Follower app logs in with Users.AsyncID. Align AsyncID to the account password when set.
        /// </summary>
        public async Task AlignFollowerAsyncIdAsync(int userId, string? password, CancellationToken ct = default)
        {
            if (userId <= 0 || string.IsNullOrWhiteSpace(password)) return;
            await using var c = new SqlConnection(_cs);
            await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.Users
SET AsyncID = @Password
WHERE UserID = @UserId
  AND (UserType = N'متابع' OR UserType LIKE N'متابع%');",
                new { UserId = userId, Password = password.Trim() },
                cancellationToken: ct));
        }
    }
}
