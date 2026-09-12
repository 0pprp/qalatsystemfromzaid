using BE_Company.Sales.Services;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Filtering
{
    public interface ISalesFilterRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task ReplaceUserCitiesAsync(int userId, IReadOnlyList<(string CityValue, string? CityName)> cities, CancellationToken ct = default);
        Task ClearUserCitiesAsync(int userId, CancellationToken ct = default);
        Task<IReadOnlyList<SalesFilterCityDTO>> ListUserCitiesAsync(int userId, CancellationToken ct = default);
        Task<bool> UserHasCityAsync(int userId, string cityValue, CancellationToken ct = default);
        Task<(IReadOnlyList<SalesFilterListItemDTO> Items, int Total)> ListRequestsAsync(
            SalesFilterCityScope scope,
            string filterStatus,
            string? search,
            int page,
            int pageSize,
            bool ownByActor,
            int? actorUserId,
            string? actorUserName,
            CancellationToken ct = default);
        Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
            SalesFilterCityScope scope,
            int? actorUserId,
            string? actorUserName,
            CancellationToken ct = default);
        Task<SalesFilterDetailDTO?> GetRequestAsync(int id, CancellationToken ct = default);
        Task<(bool Ok, string? CurrentStatus)> TryTransitionAsync(
            int id,
            string expectedStatus,
            string newStatus,
            int? userId,
            string? changedByUserName,
            string? note,
            string? reason,
            CancellationToken ct = default);
        Task<IReadOnlyList<SalesFilterHistoryDTO>> ListHistoryAsync(int saleRequestId, CancellationToken ct = default);
    }

    public sealed class SalesFilterRepository : ISalesFilterRepository
    {
        private readonly SalesDevelopmentGuard _guard;

        public SalesFilterRepository(SalesDevelopmentGuard guard)
        {
            _guard = guard;
        }

        private string RequireCs() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            await using var c = new SqlConnection(RequireCs());
            await c.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
        }

        public async Task ReplaceUserCitiesAsync(int userId, IReadOnlyList<(string CityValue, string? CityName)> cities, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(RequireCs());
            await c.OpenAsync(ct);
            await using var tx = c.BeginTransaction();
            await c.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.SalesFilterUserCities WHERE UserId = @UserId",
                new { UserId = userId }, transaction: tx, cancellationToken: ct));
            foreach (var city in cities
                         .Where(x => !string.IsNullOrWhiteSpace(x.CityValue))
                         .GroupBy(x => x.CityValue.Trim(), StringComparer.OrdinalIgnoreCase)
                         .Select(g => g.First()))
            {
                await c.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.SalesFilterUserCities (UserId, CityValue, CityName, CreatedAtUtc)
VALUES (@UserId, @CityValue, @CityName, SYSUTCDATETIME());",
                    new
                    {
                        UserId = userId,
                        CityValue = city.CityValue.Trim(),
                        CityName = string.IsNullOrWhiteSpace(city.CityName) ? null : city.CityName.Trim(),
                    },
                    transaction: tx,
                    cancellationToken: ct));
            }

            tx.Commit();
        }

        public async Task ClearUserCitiesAsync(int userId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(RequireCs());
            await c.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.SalesFilterUserCities WHERE UserId = @UserId",
                new { UserId = userId }, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<SalesFilterCityDTO>> ListUserCitiesAsync(int userId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(RequireCs());
            var rows = await c.QueryAsync<SalesFilterCityDTO>(new CommandDefinition(@"
SELECT CityValue, CityName
FROM dbo.SalesFilterUserCities
WHERE UserId = @UserId
ORDER BY CityName, CityValue;",
                new { UserId = userId }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<bool> UserHasCityAsync(int userId, string cityValue, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(cityValue)) return false;
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(RequireCs());
            var n = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.SalesFilterUserCities
WHERE UserId = @UserId AND CityValue = @CityValue;",
                new { UserId = userId, CityValue = cityValue.Trim() }, cancellationToken: ct));
            return n > 0;
        }

        public async Task<(IReadOnlyList<SalesFilterListItemDTO> Items, int Total)> ListRequestsAsync(
            SalesFilterCityScope scope,
            string filterStatus,
            string? search,
            int page,
            int pageSize,
            bool ownByActor,
            int? actorUserId,
            string? actorUserName,
            CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            if (!scope.TrustEntireBranch && scope.CityValues.Count == 0)
            {
                return ([], 0);
            }

            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            var cities = scope.CityValues
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!scope.TrustEntireBranch && cities.Count == 0)
            {
                return ([], 0);
            }

            var q = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
            var actorName = string.IsNullOrWhiteSpace(actorUserName) ? null : actorUserName.Trim();
            await using var c = new SqlConnection(RequireCs());
            var args = new
            {
                FilterStatus = filterStatus,
                Cities = cities.Count == 0 ? new List<string> { "__none__" } : cities,
                TrustBranch = scope.TrustEntireBranch,
                OwnByActor = ownByActor,
                ActorUserId = actorUserId,
                ActorUserName = actorName,
                Q = q,
                Skip = (page - 1) * pageSize,
                Take = pageSize,
            };

            var total = await c.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM dbo.SalesRequests R
WHERE R.FilterStatus = @FilterStatus
  AND R.TargetEmployeeId > 0
  AND (@TrustBranch = 1 OR R.CityValue IN @Cities)
  AND (
        @OwnByActor = 0
        OR (
            (@ActorUserId IS NOT NULL AND R.FilteredByUserId = @ActorUserId)
            OR (
                @ActorUserName IS NOT NULL
                AND R.FilteredByUserName IS NOT NULL
                AND LOWER(LTRIM(RTRIM(R.FilteredByUserName))) = LOWER(@ActorUserName)
            )
        )
      )
  AND (@Q IS NULL OR R.CustomerName LIKE @Q OR R.CustomerPhone LIKE @Q OR R.CustomerAddress LIKE @Q OR R.Notes LIKE @Q);",
                args, cancellationToken: ct));

            var items = (await c.QueryAsync<SalesFilterListItemDTO>(new CommandDefinition(@"
SELECT
    R.Id,
    R.CustomerName,
    R.CustomerPhone,
    R.CityValue,
    R.CityName,
    R.CustomerProvince,
    R.CustomerAddress,
    R.Notes AS WantedDescription,
    R.FilterStatus,
    R.FilterNote,
    R.FilterRejectReason AS RejectReason,
    R.CreatedAtUtc,
    R.FilteredAtUtc
FROM dbo.SalesRequests R
WHERE R.FilterStatus = @FilterStatus
  AND R.TargetEmployeeId > 0
  AND (@TrustBranch = 1 OR R.CityValue IN @Cities)
  AND (
        @OwnByActor = 0
        OR (
            (@ActorUserId IS NOT NULL AND R.FilteredByUserId = @ActorUserId)
            OR (
                @ActorUserName IS NOT NULL
                AND R.FilteredByUserName IS NOT NULL
                AND LOWER(LTRIM(RTRIM(R.FilteredByUserName))) = LOWER(@ActorUserName)
            )
        )
      )
  AND (@Q IS NULL OR R.CustomerName LIKE @Q OR R.CustomerPhone LIKE @Q OR R.CustomerAddress LIKE @Q OR R.Notes LIKE @Q)
ORDER BY R.CreatedAtUtc DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;",
                args, cancellationToken: ct))).ToList();

            return (items, total);
        }

        public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
            SalesFilterCityScope scope,
            int? actorUserId,
            string? actorUserName,
            CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [SalesFilterStatuses.PendingFilter] = 0,
                [SalesFilterStatuses.OnHold] = 0,
                [SalesFilterStatuses.ReadyForSale] = 0,
                [SalesFilterStatuses.Rejected] = 0,
            };
            if (!scope.TrustEntireBranch && scope.CityValues.Count == 0)
            {
                return result;
            }

            var cities = scope.CityValues
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (!scope.TrustEntireBranch && cities.Count == 0)
            {
                return result;
            }

            var actorName = string.IsNullOrWhiteSpace(actorUserName) ? null : actorUserName.Trim();
            await using var c = new SqlConnection(RequireCs());
            var rows = await c.QueryAsync<(string FilterStatus, int Cnt)>(new CommandDefinition(@"
SELECT R.FilterStatus, COUNT(1) AS Cnt
FROM dbo.SalesRequests R
WHERE R.TargetEmployeeId > 0
  AND (@TrustBranch = 1 OR R.CityValue IN @Cities)
  AND (
        R.FilterStatus = N'PendingFilter'
        OR (
            (@ActorUserId IS NOT NULL AND R.FilteredByUserId = @ActorUserId)
            OR (
                @ActorUserName IS NOT NULL
                AND R.FilteredByUserName IS NOT NULL
                AND LOWER(LTRIM(RTRIM(R.FilteredByUserName))) = LOWER(@ActorUserName)
            )
        )
      )
GROUP BY R.FilterStatus;",
                new
                {
                    Cities = cities.Count == 0 ? new List<string> { "__none__" } : cities,
                    TrustBranch = scope.TrustEntireBranch,
                    ActorUserId = actorUserId,
                    ActorUserName = actorName,
                },
                cancellationToken: ct));

            foreach (var row in rows)
            {
                if (!string.IsNullOrWhiteSpace(row.FilterStatus))
                {
                    result[row.FilterStatus] = row.Cnt;
                }
            }

            return result;
        }

        public async Task<SalesFilterDetailDTO?> GetRequestAsync(int id, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(RequireCs());
            return await c.QueryFirstOrDefaultAsync<SalesFilterDetailDTO>(new CommandDefinition(@"
SELECT
    R.Id,
    R.CustomerName,
    R.CustomerPhone,
    R.CityValue,
    R.CityName,
    R.CustomerProvince,
    R.CustomerAddress,
    R.Notes AS WantedDescription,
    R.FilterStatus,
    R.CreatedAtUtc,
    R.FilteredAtUtc,
    R.FilterNote,
    R.FilterRejectReason AS RejectReason,
    R.FilteredByUserId,
    R.FilteredByUserName,
    R.TargetEmployeeId,
    R.TargetEmployeeName
FROM dbo.SalesRequests R
WHERE R.Id = @Id;",
                new { Id = id }, cancellationToken: ct));
        }

        public async Task<(bool Ok, string? CurrentStatus)> TryTransitionAsync(
            int id,
            string expectedStatus,
            string newStatus,
            int? userId,
            string? changedByUserName,
            string? note,
            string? reason,
            CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(RequireCs());
            await c.OpenAsync(ct);
            await using var tx = c.BeginTransaction();

            var current = await c.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                "SELECT FilterStatus FROM dbo.SalesRequests WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id",
                new { Id = id }, transaction: tx, cancellationToken: ct));
            if (current == null)
            {
                tx.Rollback();
                return (false, null);
            }

            if (!string.Equals(current, expectedStatus, StringComparison.OrdinalIgnoreCase))
            {
                tx.Rollback();
                return (false, current);
            }

            var affected = await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesRequests SET
    FilterStatus = @NewStatus,
    FilteredByUserId = @UserId,
    FilteredByUserName = @UserName,
    FilterNote = @Note,
    FilterRejectReason = CASE WHEN @NewStatus = N'Rejected' THEN @Reason ELSE FilterRejectReason END,
    FilteredAtUtc = SYSUTCDATETIME(),
    [Status] = CASE WHEN @NewStatus = N'ReadyForSale' THEN N'PreparedForSale' ELSE [Status] END,
    ProcessingAtUtc = CASE WHEN @NewStatus = N'ReadyForSale' THEN SYSUTCDATETIME() ELSE ProcessingAtUtc END,
    PreparedForSaleNote = CASE
        WHEN @NewStatus = N'ReadyForSale' THEN COALESCE(@Note, N'جاهز من فلترة المبيعات')
        ELSE PreparedForSaleNote
    END
WHERE Id = @Id AND FilterStatus = @ExpectedStatus;",
                new
                {
                    Id = id,
                    NewStatus = newStatus,
                    ExpectedStatus = expectedStatus,
                    UserId = userId,
                    UserName = Truncate(changedByUserName, 200),
                    Note = note,
                    Reason = reason,
                },
                transaction: tx,
                cancellationToken: ct));

            if (affected != 1)
            {
                var again = await c.ExecuteScalarAsync<string>(new CommandDefinition(
                    "SELECT FilterStatus FROM dbo.SalesRequests WHERE Id = @Id",
                    new { Id = id }, transaction: tx, cancellationToken: ct));
                tx.Rollback();
                return (false, again);
            }

            await c.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.SalesFilterHistory
(SaleRequestId, PreviousStatus, NewStatus, ChangedByUserId, ChangedByUserName, Note, Reason, ChangedAtUtc)
VALUES (@Id, @Previous, @NewStatus, @UserId, @UserName, @Note, @Reason, SYSUTCDATETIME());",
                new
                {
                    Id = id,
                    Previous = expectedStatus,
                    NewStatus = newStatus,
                    UserId = userId,
                    UserName = Truncate(changedByUserName, 200),
                    Note = note,
                    Reason = reason,
                },
                transaction: tx,
                cancellationToken: ct));

            tx.Commit();
            return (true, newStatus);
        }

        private static string? Truncate(string? value, int max)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= max ? trimmed : trimmed[..max];
        }

        public async Task<IReadOnlyList<SalesFilterHistoryDTO>> ListHistoryAsync(int saleRequestId, CancellationToken ct = default)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(RequireCs());
            var rows = await c.QueryAsync<SalesFilterHistoryDTO>(new CommandDefinition(@"
SELECT Id, SaleRequestId, PreviousStatus, NewStatus, ChangedByUserId, ChangedByUserName, Note, Reason, ChangedAtUtc
FROM dbo.SalesFilterHistory
WHERE SaleRequestId = @Id
ORDER BY ChangedAtUtc ASC, Id ASC;",
                new { Id = saleRequestId }, cancellationToken: ct));
            return rows.ToList();
        }

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.SalesFilterUserCities', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesFilterUserCities (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NOT NULL,
        CityValue NVARCHAR(100) NOT NULL,
        CityName NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SalesFilterUserCities_At DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_SalesFilterUserCities_User_City UNIQUE (UserId, CityValue)
    );
    CREATE INDEX IX_SalesFilterUserCities_User ON dbo.SalesFilterUserCities (UserId);
END

IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SalesFilterUserCities_Users')
BEGIN
    ALTER TABLE dbo.SalesFilterUserCities WITH NOCHECK
    ADD CONSTRAINT FK_SalesFilterUserCities_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserID);
END

IF COL_LENGTH(N'dbo.SalesRequests', N'FilterStatus') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilterStatus NVARCHAR(30) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilteredByUserId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilteredByUserId INT NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilteredByUserName') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilteredByUserName NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilterNote') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilterNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilterRejectReason') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilterRejectReason NVARCHAR(400) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilteredAtUtc') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilteredAtUtc DATETIME2 NULL;

-- Legacy backfill: existing requests already in the sales-employee path must remain visible.
UPDATE dbo.SalesRequests
SET FilterStatus = N'ReadyForSale'
WHERE FilterStatus IS NULL
  AND TargetEmployeeId > 0
  AND [Status] IN (N'Assigned', N'Viewed', N'PreparedForSale', N'Pending', N'InProgress',
                   N'ConvertedToSale', N'Inspected', N'Completed', N'Returned', N'Rejected');

UPDATE dbo.SalesRequests
SET FilterStatus = N'PendingFilter'
WHERE FilterStatus IS NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequests_FilterStatus' AND object_id = OBJECT_ID(N'dbo.SalesRequests')
)
    CREATE INDEX IX_SalesRequests_FilterStatus ON dbo.SalesRequests (FilterStatus);

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequests_City_FilterStatus' AND object_id = OBJECT_ID(N'dbo.SalesRequests')
)
    CREATE INDEX IX_SalesRequests_City_FilterStatus ON dbo.SalesRequests (CityValue, FilterStatus);

IF OBJECT_ID(N'dbo.SalesFilterHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesFilterHistory (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SaleRequestId INT NOT NULL,
        PreviousStatus NVARCHAR(30) NULL,
        NewStatus NVARCHAR(30) NOT NULL,
        ChangedByUserId INT NULL,
        ChangedByUserName NVARCHAR(200) NULL,
        Note NVARCHAR(1000) NULL,
        Reason NVARCHAR(400) NULL,
        ChangedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SalesFilterHistory_At DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_SalesFilterHistory_Request ON dbo.SalesFilterHistory (SaleRequestId, ChangedAtUtc, Id);
END

IF OBJECT_ID(N'dbo.SalesFilterHistory', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SalesFilterHistory', N'ChangedByUserName') IS NULL
    ALTER TABLE dbo.SalesFilterHistory ADD ChangedByUserName NVARCHAR(200) NULL;
";
    }
}
