using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class SalesRequestRepository : ISalesRequestRepository
    {
        private readonly SalesDevelopmentGuard _guard;
        private static readonly object SchemaGate = new();
        private static bool _schemaReady;

        public SalesRequestRepository(SalesDevelopmentGuard guard)
        {
            _guard = guard;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct)
        {
            if (_schemaReady)
            {
                return;
            }

            lock (SchemaGate)
            {
                if (_schemaReady)
                {
                    return;
                }
            }

            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = SchemaSql;
            await command.ExecuteNonQueryAsync(ct);
            lock (SchemaGate)
            {
                _schemaReady = true;
            }
        }

        public async Task<SalesRequestDTO> InsertAsync(SalesRequestDTO row, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesRequests
(CreatedByUserId, CreatedByName, CreatedByUserType, TargetEmployeeId, TargetEmployeeName, CityValue, CityName,
 CustomerSourceType, ExistingCustomerId, CustomerSourceCityValue, CustomerName, CustomerPhone, CustomerProvince,
 CustomerAddress, Notes, MaxSalePrice, Status, CreatedAtUtc, AssignedAtUtc, AssignedByUserId, AssignedByName, PendingNote, ReturnNote,
 SaleRequestType, SourceListId, FilterStatus)
OUTPUT INSERTED.Id
VALUES
(@CreatedByUserId, @CreatedByName, @CreatedByUserType, @TargetEmployeeId, @TargetEmployeeName, @CityValue, @CityName,
 @CustomerSourceType, @ExistingCustomerId, @CustomerSourceCityValue, @CustomerName, @CustomerPhone, @CustomerProvince,
 @CustomerAddress, @Notes, @MaxSalePrice, @Status, @CreatedAtUtc, @AssignedAtUtc, @AssignedByUserId, @AssignedByName, @PendingNote, @ReturnNote,
 @SaleRequestType, @SourceListId, @FilterStatus);",
                row, cancellationToken: ct));
            row.Id = id;
            return row;
        }

        public async Task<SalesRequestDTO?> GetByIdAsync(int id, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.QueryFirstOrDefaultAsync<SalesRequestDTO>(new CommandDefinition(
                SelectSql + " WHERE Id = @Id AND ISNULL(IsDeleted, 0) = 0", new { Id = id }, cancellationToken: ct));
        }

        public async Task<SalesRequestDTO?> GetByIdIncludingDeletedAsync(int id, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.QueryFirstOrDefaultAsync<SalesRequestDTO>(new CommandDefinition(
                SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<SalesRequestDTO>> ListAsync(int? targetEmployeeId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct, string? filterStatus = null, int? take = null)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var top = take is > 0 and <= 5000 ? take.Value : 0;
            var topClause = top > 0 ? $"TOP ({top}) " : "";
            var sql = $@"
SELECT {topClause}Id, CreatedByUserId, CreatedByName, CreatedByUserType, TargetEmployeeId, TargetEmployeeName,
 CityValue, CityName, CustomerSourceType, ExistingCustomerId, CustomerSourceCityValue,
 CustomerName, CustomerPhone, CustomerProvince, CustomerAddress, Notes, MaxSalePrice, Status,
 CreatedAtUtc, ViewedAtUtc, ProcessingAtUtc, ConvertedToSaleId, CompletedAtUtc, RejectedAtUtc, RejectionReason,
 AssignedAtUtc, AssignedByUserId, AssignedByName, PendingNote, PreparedForSaleNote, ReturnNote, ManagerReadAtUtc,
 SaleRequestType, SourceListId,
 FilterStatus, FilteredByUserId, FilterNote, FilterRejectReason, FilteredAtUtc,
 ISNULL(IsDeleted, 0) AS IsDeleted, DeletedAtUtc, DeletedByUserId, DeletedByName, DeleteReason,
 ExceptionHoldStatus, ActiveExceptionId
FROM dbo.SalesRequests WHERE ISNULL(IsDeleted, 0) = 0
AND (@TargetEmployeeId IS NULL OR TargetEmployeeId = @TargetEmployeeId)
AND (@Status IS NULL OR Status = @Status)
AND (@FilterStatus IS NULL OR FilterStatus = @FilterStatus)
AND (@FromUtc IS NULL OR CreatedAtUtc >= @FromUtc)
AND (@ToUtc IS NULL OR CreatedAtUtc <= @ToUtc)
ORDER BY CreatedAtUtc DESC";
            var rows = await connection.QueryAsync<SalesRequestDTO>(new CommandDefinition(sql,
                new { TargetEmployeeId = targetEmployeeId, Status = status, FilterStatus = filterStatus, FromUtc = fromUtc, ToUtc = toUtc },
                cancellationToken: ct));
            return rows.ToList();
        }

        /// <summary>
        /// Paged lean list for manager board (20k+ production). No Timeline/History hydrate.
        /// Always excludes held-unassigned rows (same as ListForManagerAsync).
        /// </summary>
        public async Task<(IReadOnlyList<SalesRequestDTO> Items, int Total)> ListManagerPageAsync(
            string? tab,
            int? targetEmployeeId,
            string? q,
            int skip,
            int take,
            CancellationToken ct)
        {
            skip = Math.Max(0, skip);
            take = Math.Clamp(take <= 0 ? 40 : take, 1, 100);
            var tabKey = NormalizeTab(tab);
            var like = NormalizeSearch(q);
            var emp = targetEmployeeId is > 0 ? targetEmployeeId : null;

            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var args = new
            {
                Tab = tabKey,
                TargetEmployeeId = emp,
                Q = like,
                Skip = skip,
                Take = take
            };

            var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM dbo.SalesRequests R WHERE " + ManagerListWhereSql,
                args, cancellationToken: ct));

            var items = (await connection.QueryAsync<SalesRequestDTO>(new CommandDefinition(
                ManagerListSelectSql + @"
WHERE " + ManagerListWhereSql + @"
ORDER BY R.CreatedAtUtc DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;",
                args, cancellationToken: ct))).ToList();

            return (items, total);
        }

        public async Task<Dictionary<string, int>> CountManagerTabsAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var row = await connection.QueryFirstAsync<(
                int All,
                int Unassigned,
                int Legal,
                int Sent,
                int Incoming,
                int Prepared,
                int Inspected,
                int Pending,
                int Rejected,
                int Sold,
                int Deleted)>(new CommandDefinition(@"
SELECT
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @" THEN 1 ELSE 0 END) AS [All],
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @"
      AND R.Status NOT IN (N'Rejected', N'Completed', N'LegalFollowup')
      AND (R.Status = N'New' OR ISNULL(R.TargetEmployeeId, 0) <= 0)
      AND (R.ExceptionHoldStatus IS NULL OR R.ExceptionHoldStatus NOT IN (N'Pending', N'Approved', N'Rejected'))
    THEN 1 ELSE 0 END) AS Unassigned,
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @" AND R.Status = N'LegalFollowup' THEN 1 ELSE 0 END) AS Legal,
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @"
      AND R.Status = N'New'
      AND (
        R.CustomerSourceType IN (N'EmployeeSubmitted', N'Follower', N'Delegate')
        OR R.CreatedByUserType IN (N'Follower', N'متابع', N'Delegate', N'مندوب')
      ) THEN 1 ELSE 0 END) AS Sent,
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @" AND R.Status IN (N'Assigned', N'Viewed', N'Returned') THEN 1 ELSE 0 END) AS Incoming,
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @" AND R.Status IN (N'PreparedForSale', N'InProgress', N'ConvertedToSale') THEN 1 ELSE 0 END) AS Prepared,
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @" AND R.Status = N'Inspected' THEN 1 ELSE 0 END) AS Inspected,
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @" AND R.Status = N'Pending' THEN 1 ELSE 0 END) AS Pending,
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @" AND R.Status = N'Rejected' THEN 1 ELSE 0 END) AS Rejected,
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 0 AND NOT " + SqlIsHeldUnassigned + @" AND R.Status = N'Completed' THEN 1 ELSE 0 END) AS Sold,
  SUM(CASE WHEN ISNULL(R.IsDeleted, 0) = 1 THEN 1 ELSE 0 END) AS Deleted
FROM dbo.SalesRequests R;",
                cancellationToken: ct));

            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["all"] = row.All,
                ["unassigned"] = row.Unassigned,
                ["legal"] = row.Legal,
                ["sent"] = row.Sent,
                ["incoming"] = row.Incoming,
                ["prepared"] = row.Prepared,
                ["inspected"] = row.Inspected,
                ["pending"] = row.Pending,
                ["rejected"] = row.Rejected,
                ["sold"] = row.Sold,
                ["deleted"] = row.Deleted,
            };
        }

        public async Task<(IReadOnlyList<SalesRequestDTO> Items, int Total)> ListDeletedPageAsync(
            string? q,
            int skip,
            int take,
            CancellationToken ct)
        {
            skip = Math.Max(0, skip);
            take = Math.Clamp(take <= 0 ? 40 : take, 1, 100);
            var like = NormalizeSearch(q);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var args = new { Q = like, Skip = skip, Take = take };
            var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1) FROM dbo.SalesRequests R
WHERE ISNULL(R.IsDeleted, 0) = 1
  AND (@Q IS NULL OR R.CustomerName LIKE @Q OR R.CustomerPhone LIKE @Q OR CAST(R.Id AS NVARCHAR(20)) LIKE @Q);",
                args, cancellationToken: ct));
            var items = (await connection.QueryAsync<SalesRequestDTO>(new CommandDefinition(
                ManagerListSelectSql + @"
WHERE ISNULL(R.IsDeleted, 0) = 1
  AND (@Q IS NULL OR R.CustomerName LIKE @Q OR R.CustomerPhone LIKE @Q OR CAST(R.Id AS NVARCHAR(20)) LIKE @Q)
ORDER BY R.DeletedAtUtc DESC, R.CreatedAtUtc DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;",
                args, cancellationToken: ct))).ToList();
            return (items, total);
        }

        private static string NormalizeTab(string? tab)
        {
            var key = (tab ?? "all").Trim().ToLowerInvariant();
            return key switch
            {
                "all" or "unassigned" or "legal" or "sent" or "incoming"
                    or "prepared" or "inspected" or "pending" or "rejected" or "sold" => key,
                _ => "all"
            };
        }

        private static string? NormalizeSearch(string? q)
        {
            var text = (q ?? "").Trim();
            if (text.Length == 0)
            {
                return null;
            }

            if (text.Length > 80)
            {
                text = text[..80];
            }

            return "%" + text.Replace("%", "").Replace("_", "") + "%";
        }

        private const string ManagerListSelectSql = @"
SELECT R.Id, R.CreatedByUserId, R.CreatedByName, R.CreatedByUserType, R.TargetEmployeeId, R.TargetEmployeeName,
 R.CityValue, R.CityName, R.CustomerSourceType, R.ExistingCustomerId, R.CustomerSourceCityValue,
 R.CustomerName, R.CustomerPhone, R.CustomerProvince, R.CustomerAddress, R.Notes, R.MaxSalePrice, R.Status,
 R.CreatedAtUtc, R.ViewedAtUtc, R.ProcessingAtUtc, R.ConvertedToSaleId, R.CompletedAtUtc, R.RejectedAtUtc, R.RejectionReason,
 R.AssignedAtUtc, R.AssignedByUserId, R.AssignedByName, R.PendingNote, R.PreparedForSaleNote, R.ReturnNote, R.ManagerReadAtUtc,
 R.SaleRequestType, R.SourceListId,
 R.FilterStatus, R.FilteredByUserId, R.FilterNote, R.FilterRejectReason, R.FilteredAtUtc,
 ISNULL(R.IsDeleted, 0) AS IsDeleted, R.DeletedAtUtc, R.DeletedByUserId, R.DeletedByName, R.DeleteReason,
 R.ExceptionHoldStatus, R.ActiveExceptionId
FROM dbo.SalesRequests R
";

        /// <summary>
        /// Held on the exceptions board (unassigned). Must use IS NOT NULL — otherwise
        /// SQL three-valued logic treats NULL ExceptionHoldStatus as UNKNOWN and
        /// <c>NOT (UNKNOWN AND TargetEmployeeId&lt;=0)</c> hides every new employee/delegate/follower submit.
        /// </summary>
        private const string SqlIsHeldUnassigned = @"
(
  R.ExceptionHoldStatus IS NOT NULL
  AND R.ExceptionHoldStatus IN (N'Pending', N'Approved', N'Rejected')
  AND ISNULL(R.TargetEmployeeId, 0) <= 0
)";

        /// <summary>Shared WHERE for paged manager list + tab filter + search.</summary>
        private const string ManagerListWhereSql = @"
ISNULL(R.IsDeleted, 0) = 0
AND NOT " + SqlIsHeldUnassigned + @"
AND (@TargetEmployeeId IS NULL OR R.TargetEmployeeId = @TargetEmployeeId)
AND (@Q IS NULL OR R.CustomerName LIKE @Q OR R.CustomerPhone LIKE @Q OR CAST(R.Id AS NVARCHAR(20)) LIKE @Q)
AND (
  @Tab = N'all'
  OR (
    @Tab = N'unassigned'
    AND R.Status NOT IN (N'Rejected', N'Completed', N'LegalFollowup')
    AND (R.Status = N'New' OR ISNULL(R.TargetEmployeeId, 0) <= 0)
    AND (R.ExceptionHoldStatus IS NULL OR R.ExceptionHoldStatus NOT IN (N'Pending', N'Approved', N'Rejected'))
  )
  OR (@Tab = N'legal' AND R.Status = N'LegalFollowup')
  OR (
    @Tab = N'sent'
    AND R.Status = N'New'
    AND (
      R.CustomerSourceType IN (N'EmployeeSubmitted', N'Follower', N'Delegate')
      OR R.CreatedByUserType IN (N'Follower', N'متابع', N'Delegate', N'مندوب')
    )
  )
  OR (@Tab = N'incoming' AND R.Status IN (N'Assigned', N'Viewed', N'Returned'))
  OR (@Tab = N'prepared' AND R.Status IN (N'PreparedForSale', N'InProgress', N'ConvertedToSale'))
  OR (@Tab = N'inspected' AND R.Status = N'Inspected')
  OR (@Tab = N'pending' AND R.Status = N'Pending')
  OR (@Tab = N'rejected' AND R.Status = N'Rejected')
  OR (@Tab = N'sold' AND R.Status = N'Completed')
)";

        public async Task UpdateAsync(SalesRequestDTO row, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesRequests SET
 [Status] = @Status, TargetEmployeeId = @TargetEmployeeId, TargetEmployeeName = @TargetEmployeeName,
 CityValue = @CityValue, CityName = @CityName,
 CustomerSourceType = @CustomerSourceType, ExistingCustomerId = @ExistingCustomerId,
 CustomerSourceCityValue = @CustomerSourceCityValue,
 CustomerName = @CustomerName, CustomerPhone = @CustomerPhone,
 CustomerProvince = @CustomerProvince, CustomerAddress = @CustomerAddress, Notes = @Notes,
 MaxSalePrice = @MaxSalePrice,
 ViewedAtUtc = @ViewedAtUtc, ProcessingAtUtc = @ProcessingAtUtc,
 ConvertedToSaleId = @ConvertedToSaleId, CompletedAtUtc = @CompletedAtUtc,
 RejectedAtUtc = @RejectedAtUtc, RejectionReason = @RejectionReason,
 AssignedAtUtc = @AssignedAtUtc, AssignedByUserId = @AssignedByUserId, AssignedByName = @AssignedByName,
 PendingNote = @PendingNote, PreparedForSaleNote = @PreparedForSaleNote, ReturnNote = @ReturnNote, ManagerReadAtUtc = @ManagerReadAtUtc,
 FilterStatus = @FilterStatus, FilteredByUserId = @FilteredByUserId, FilterNote = @FilterNote,
 FilterRejectReason = @FilterRejectReason, FilteredAtUtc = @FilteredAtUtc,
 IsDeleted = @IsDeleted, DeletedAtUtc = @DeletedAtUtc, DeletedByUserId = @DeletedByUserId, DeletedByName = @DeletedByName,
 DeleteReason = @DeleteReason,
 ExceptionHoldStatus = @ExceptionHoldStatus, ActiveExceptionId = @ActiveExceptionId
WHERE Id = @Id", row, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<SalesRequestDTO>> ListDeletedAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<SalesRequestDTO>(new CommandDefinition(
                SelectSql + @" WHERE ISNULL(IsDeleted, 0) = 1
ORDER BY DeletedAtUtc DESC, CreatedAtUtc DESC",
                cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<int> CountByStatusAsync(string status, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM dbo.SalesRequests WHERE Status = @Status AND ISNULL(IsDeleted, 0) = 0",
                new { Status = status }, cancellationToken: ct));
        }

        public async Task<int> CountUnreadEmployeeSubmittedAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM dbo.SalesRequests
WHERE ISNULL(IsDeleted, 0) = 0
  AND CustomerSourceType = N'EmployeeSubmitted'
  AND ManagerReadAtUtc IS NULL",
                cancellationToken: ct));
        }

        public async Task InsertHistoryAsync(SalesRequestHistoryDTO row, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesRequestHistory
(RequestId, EventType, PreviousStatus, Status, ActorUserId, ActorName, ActorType, EmployeeId, Note, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES
(@RequestId, @Event, @PreviousStatus, @Status, @ActorUserId, @ActorName, @ActorType, @EmployeeId, @Note, @CreatedAtUtc);",
                row, cancellationToken: ct));
            row.Id = id;
        }

        public async Task<IReadOnlyList<SalesRequestHistoryDTO>> ListHistoryAsync(int requestId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<SalesRequestHistoryDTO>(new CommandDefinition(@"
SELECT Id, RequestId, EventType AS Event, PreviousStatus, Status, ActorUserId, ActorName, ActorType, EmployeeId, Note, CreatedAtUtc
FROM dbo.SalesRequestHistory
WHERE RequestId = @RequestId
ORDER BY CreatedAtUtc ASC, Id ASC",
                new { RequestId = requestId }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task InsertNameTransferAsync(SalesRequestNameTransferDTO row, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesRequestNameTransfers
(SaleRequestId, FromEmployeeId, FromEmployeeName, ToEmployeeId, ToEmployeeName, TransferReason,
 TransferredAtUtc, TransferredByUserId, TransferredByName)
OUTPUT INSERTED.Id
VALUES
(@SaleRequestId, @FromEmployeeId, @FromEmployeeName, @ToEmployeeId, @ToEmployeeName, @TransferReason,
 @TransferredAtUtc, @TransferredByUserId, @TransferredByName);",
                row, cancellationToken: ct));
            row.Id = id;
        }

        public async Task<IReadOnlyList<SalesRequestNameTransferDTO>> ListNameTransfersAsync(int saleRequestId, CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<SalesRequestNameTransferDTO>(new CommandDefinition(@"
SELECT Id, SaleRequestId, FromEmployeeId, FromEmployeeName, ToEmployeeId, ToEmployeeName,
 TransferReason, TransferredAtUtc, TransferredByUserId, TransferredByName
FROM dbo.SalesRequestNameTransfers
WHERE SaleRequestId = @SaleRequestId
ORDER BY TransferredAtUtc ASC, Id ASC",
                new { SaleRequestId = saleRequestId }, cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<bool> TryTransferTargetAsync(
            int requestId,
            int expectedFromEmployeeId,
            int toEmployeeId,
            string? toEmployeeName,
            string status,
            DateTime assignedAtUtc,
            string filterStatus,
            CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);
            try
            {
                var updated = await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesRequests WITH (UPDLOCK, ROWLOCK) SET
 [Status] = @Status,
 TargetEmployeeId = @ToEmployeeId,
 TargetEmployeeName = @ToEmployeeName,
 ViewedAtUtc = NULL,
 AssignedAtUtc = @AssignedAtUtc,
 FilterStatus = @FilterStatus
WHERE Id = @Id AND TargetEmployeeId = @ExpectedFromEmployeeId
  AND ISNULL(IsDeleted, 0) = 0
  AND [Status] NOT IN (N'Completed', N'Rejected')",
                    new
                    {
                        Id = requestId,
                        ExpectedFromEmployeeId = expectedFromEmployeeId,
                        ToEmployeeId = toEmployeeId,
                        ToEmployeeName = toEmployeeName,
                        Status = status,
                        AssignedAtUtc = assignedAtUtc,
                        FilterStatus = filterStatus
                    },
                    transaction: (SqlTransaction)tx,
                    cancellationToken: ct));
                if (updated != 1)
                {
                    await tx.RollbackAsync(ct);
                    return false;
                }

                await tx.CommitAsync(ct);
                return true;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

        private const string SelectSql = @"
SELECT Id, CreatedByUserId, CreatedByName, CreatedByUserType, TargetEmployeeId, TargetEmployeeName,
 CityValue, CityName, CustomerSourceType, ExistingCustomerId, CustomerSourceCityValue,
 CustomerName, CustomerPhone, CustomerProvince, CustomerAddress, Notes, MaxSalePrice, Status,
 CreatedAtUtc, ViewedAtUtc, ProcessingAtUtc, ConvertedToSaleId, CompletedAtUtc, RejectedAtUtc, RejectionReason,
 AssignedAtUtc, AssignedByUserId, AssignedByName, PendingNote, PreparedForSaleNote, ReturnNote, ManagerReadAtUtc,
 SaleRequestType, SourceListId,
 FilterStatus, FilteredByUserId, FilterNote, FilterRejectReason, FilteredAtUtc,
 ISNULL(IsDeleted, 0) AS IsDeleted, DeletedAtUtc, DeletedByUserId, DeletedByName, DeleteReason,
 ExceptionHoldStatus, ActiveExceptionId
FROM dbo.SalesRequests";

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.SalesRequests', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRequests (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        CreatedByUserId INT NOT NULL,
        CreatedByName NVARCHAR(200) NULL,
        CreatedByUserType NVARCHAR(50) NULL,
        TargetEmployeeId INT NOT NULL,
        TargetEmployeeName NVARCHAR(200) NULL,
        CityValue NVARCHAR(100) NULL,
        CityName NVARCHAR(200) NULL,
        CustomerSourceType NVARCHAR(30) NOT NULL,
        ExistingCustomerId INT NULL,
        CustomerSourceCityValue NVARCHAR(100) NULL,
        CustomerName NVARCHAR(200) NOT NULL,
        CustomerPhone NVARCHAR(50) NULL,
        CustomerProvince NVARCHAR(100) NULL,
        CustomerAddress NVARCHAR(400) NULL,
        Notes NVARCHAR(1000) NULL,
        MaxSalePrice DECIMAL(18,0) NULL,
        Status NVARCHAR(30) NOT NULL,
        CreatedAtUtc DATETIME NOT NULL CONSTRAINT DF_SalesRequests_CreatedAt DEFAULT (GETUTCDATE()),
        ViewedAtUtc DATETIME NULL,
        ProcessingAtUtc DATETIME NULL,
        ConvertedToSaleId INT NULL,
        CompletedAtUtc DATETIME NULL,
        RejectedAtUtc DATETIME NULL,
        RejectionReason NVARCHAR(400) NULL
    );
END;
IF COL_LENGTH(N'dbo.SalesDrafts', N'SalesRequestId') IS NULL
BEGIN
    ALTER TABLE dbo.SalesDrafts ADD SalesRequestId INT NULL;
END;
IF COL_LENGTH(N'dbo.SalesRequests', N'AssignedAtUtc') IS NULL
    ALTER TABLE dbo.SalesRequests ADD AssignedAtUtc DATETIME NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'AssignedByUserId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD AssignedByUserId INT NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'AssignedByName') IS NULL
    ALTER TABLE dbo.SalesRequests ADD AssignedByName NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'PendingNote') IS NULL
    ALTER TABLE dbo.SalesRequests ADD PendingNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'PreparedForSaleNote') IS NULL
    ALTER TABLE dbo.SalesRequests ADD PreparedForSaleNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'ReturnNote') IS NULL
    ALTER TABLE dbo.SalesRequests ADD ReturnNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'ManagerReadAtUtc') IS NULL
    ALTER TABLE dbo.SalesRequests ADD ManagerReadAtUtc DATETIME NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'SaleRequestType') IS NULL
    ALTER TABLE dbo.SalesRequests ADD SaleRequestType NVARCHAR(20) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'SourceListId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD SourceListId INT NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'ShopRating') IS NULL
    ALTER TABLE dbo.SalesRequests ADD ShopRating INT NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'ShopRatingNote') IS NULL
    ALTER TABLE dbo.SalesRequests ADD ShopRatingNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'ShopStockEstimatedValue') IS NULL
    ALTER TABLE dbo.SalesRequests ADD ShopStockEstimatedValue DECIMAL(18,0) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'EstimatedDailyRevenue') IS NULL
    ALTER TABLE dbo.SalesRequests ADD EstimatedDailyRevenue DECIMAL(18,0) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'IsWrongSale') IS NULL
    ALTER TABLE dbo.SalesRequests ADD IsWrongSale BIT NOT NULL CONSTRAINT DF_SalesRequests_IsWrongSale DEFAULT (0);
IF COL_LENGTH(N'dbo.SalesRequests', N'FilterStatus') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilterStatus NVARCHAR(30) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilteredByUserId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilteredByUserId INT NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilterNote') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilterNote NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilterRejectReason') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilterRejectReason NVARCHAR(400) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'FilteredAtUtc') IS NULL
    ALTER TABLE dbo.SalesRequests ADD FilteredAtUtc DATETIME2 NULL;
-- Legacy: keep pre-filter assigned work visible to sales employees.
UPDATE dbo.SalesRequests
SET FilterStatus = N'ReadyForSale'
WHERE FilterStatus IS NULL
  AND TargetEmployeeId > 0
  AND [Status] IN (N'Assigned', N'Viewed', N'PreparedForSale', N'Pending', N'InProgress',
                   N'ConvertedToSale', N'Inspected', N'Completed', N'Returned', N'Rejected');
UPDATE dbo.SalesRequests SET FilterStatus = N'ReadyForSale' WHERE FilterStatus IS NULL;
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
IF OBJECT_ID(N'dbo.SalesRequestHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRequestHistory (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        RequestId INT NOT NULL,
        EventType NVARCHAR(50) NOT NULL,
        Status NVARCHAR(30) NULL,
        ActorUserId INT NULL,
        ActorName NVARCHAR(200) NULL,
        ActorType NVARCHAR(50) NULL,
        EmployeeId INT NULL,
        Note NVARCHAR(1000) NULL,
        CreatedAtUtc DATETIME NOT NULL CONSTRAINT DF_SalesRequestHistory_CreatedAt DEFAULT (GETUTCDATE())
    );
    CREATE INDEX IX_SalesRequestHistory_RequestId ON dbo.SalesRequestHistory (RequestId, CreatedAtUtc, Id);
END;
IF COL_LENGTH(N'dbo.SalesRequestHistory', N'PreviousStatus') IS NULL
    ALTER TABLE dbo.SalesRequestHistory ADD PreviousStatus NVARCHAR(30) NULL;
IF OBJECT_ID(N'dbo.SalesRequestNameTransfers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesRequestNameTransfers (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SaleRequestId INT NOT NULL,
        FromEmployeeId INT NOT NULL,
        FromEmployeeName NVARCHAR(200) NULL,
        ToEmployeeId INT NOT NULL,
        ToEmployeeName NVARCHAR(200) NULL,
        TransferReason NVARCHAR(1000) NOT NULL,
        TransferredAtUtc DATETIME2 NOT NULL CONSTRAINT DF_SalesRequestNameTransfers_At DEFAULT (SYSUTCDATETIME()),
        TransferredByUserId INT NOT NULL,
        TransferredByName NVARCHAR(200) NULL
    );
END;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequestNameTransfers_SaleRequestId'
      AND object_id = OBJECT_ID(N'dbo.SalesRequestNameTransfers')
)
    CREATE INDEX IX_SalesRequestNameTransfers_SaleRequestId
        ON dbo.SalesRequestNameTransfers (SaleRequestId, TransferredAtUtc, Id);
IF COL_LENGTH(N'dbo.SalesRequests', N'IsDeleted') IS NULL
    ALTER TABLE dbo.SalesRequests ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SalesRequests_IsDeleted DEFAULT (0);
IF COL_LENGTH(N'dbo.SalesRequests', N'DeletedAtUtc') IS NULL
    ALTER TABLE dbo.SalesRequests ADD DeletedAtUtc DATETIME2 NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'DeletedByUserId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD DeletedByUserId INT NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'DeletedByName') IS NULL
    ALTER TABLE dbo.SalesRequests ADD DeletedByName NVARCHAR(200) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'DeleteReason') IS NULL
    ALTER TABLE dbo.SalesRequests ADD DeleteReason NVARCHAR(1000) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'ExceptionHoldStatus') IS NULL
    ALTER TABLE dbo.SalesRequests ADD ExceptionHoldStatus NVARCHAR(30) NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'ActiveExceptionId') IS NULL
    ALTER TABLE dbo.SalesRequests ADD ActiveExceptionId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.SalesRequests', N'MaxSalePrice') IS NULL
    ALTER TABLE dbo.SalesRequests ADD MaxSalePrice DECIMAL(18,0) NULL;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequests_IsDeleted' AND object_id = OBJECT_ID(N'dbo.SalesRequests')
)
    CREATE INDEX IX_SalesRequests_IsDeleted ON dbo.SalesRequests (IsDeleted);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequests_Active_Created' AND object_id = OBJECT_ID(N'dbo.SalesRequests')
)
    CREATE INDEX IX_SalesRequests_Active_Created ON dbo.SalesRequests (IsDeleted, CreatedAtUtc DESC);
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_SalesRequests_Active_Status_Created' AND object_id = OBJECT_ID(N'dbo.SalesRequests')
)
    CREATE INDEX IX_SalesRequests_Active_Status_Created ON dbo.SalesRequests (IsDeleted, Status, CreatedAtUtc DESC);
-- Past manager rejects belonged in Rejected; move them to Deleted with the rejection reason.
-- Employee-app rejects (ActorType = موظف مبيعات) stay in Rejected.
IF OBJECT_ID(N'dbo.SalesRequestHistory', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.SalesRequests', N'IsDeleted') IS NOT NULL
BEGIN
    UPDATE sr
    SET
        IsDeleted = 1,
        DeletedAtUtc = ISNULL(sr.RejectedAtUtc, SYSUTCDATETIME()),
        DeletedByUserId = ISNULL(h.ActorUserId, sr.DeletedByUserId),
        DeletedByName = ISNULL(NULLIF(LTRIM(RTRIM(h.ActorName)), N''), N'مدير مبيعات'),
        DeleteReason = COALESCE(
            NULLIF(LTRIM(RTRIM(sr.RejectionReason)), N''),
            NULLIF(LTRIM(RTRIM(h.Note)), N''),
            N'رفض مدير المبيعات')
    FROM dbo.SalesRequests sr
    CROSS APPLY (
        SELECT TOP (1) ActorUserId, ActorName, Note
        FROM dbo.SalesRequestHistory
        WHERE RequestId = sr.Id
          AND EventType = N'Rejected'
          AND (ActorType = N'مدير مبيعات' OR ActorType = N'SalesManager')
        ORDER BY CreatedAtUtc DESC, Id DESC
    ) h
    WHERE sr.Status = N'Rejected'
      AND ISNULL(sr.IsDeleted, 0) = 0;
END";
    }
}
