using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class BranchManagerNewSaleDTO
    {
        public int SaleId { get; set; }
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? Status { get; set; }
        public int? CustomerId { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? NearestLandmark { get; set; }
        public string? NationalCardNumber { get; set; }
        public int? EvaluationLevel { get; set; }
        public string? EvaluationNote { get; set; }
        public decimal? FinalSalePrice { get; set; }
        public decimal? DailyInstallment { get; set; }
        public decimal? DownPayment { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime? BranchManagerReadAtUtc { get; set; }
        public bool IsRead => BranchManagerReadAtUtc != null;
        public List<SalesDraftItemDTO> Items { get; set; } = [];
    }

    public interface IBranchManagerNewSalesService
    {
        Task EnsureSchemaAsync(CancellationToken ct);
        Task<IReadOnlyList<BranchManagerNewSaleDTO>> ListAsync(bool unreadOnly, CancellationToken ct);
        Task<BranchManagerNewSaleDTO?> GetAsync(int saleId, CancellationToken ct);
        Task<int> CountUnreadAsync(CancellationToken ct);
        Task<bool> MarkReadAsync(int saleId, CancellationToken ct);
        Task<int> MarkAllReadAsync(CancellationToken ct);
    }

    public sealed class BranchManagerNewSalesService : IBranchManagerNewSalesService
    {
        private const string EnsureReadColumnSql = @"
IF COL_LENGTH(N'dbo.SalesDrafts', N'BranchManagerReadAtUtc') IS NULL
    ALTER TABLE dbo.SalesDrafts ADD BranchManagerReadAtUtc DATETIME2 NULL;
IF OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.SalesDrafts', N'BranchManagerReadAtUtc') IS NOT NULL
   AND NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_SalesDrafts_BmUnread'
          AND object_id = OBJECT_ID(N'dbo.SalesDrafts'))
BEGIN
    EXEC(N'CREATE NONCLUSTERED INDEX IX_SalesDrafts_BmUnread
        ON dbo.SalesDrafts (Status, BranchManagerReadAtUtc, CompletedAt DESC)
        WHERE Status = N''Completed'';');
END;";

        private readonly SalesDevelopmentGuard _guard;
        private readonly IIraqClock _clock;

        public BranchManagerNewSalesService(SalesDevelopmentGuard guard, IIraqClock clock)
        {
            _guard = guard;
            _clock = clock;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            await connection.OpenAsync(ct);
            foreach (var sql in SalesDraftSchema.Commands)
            {
                await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
            }

            await connection.ExecuteAsync(new CommandDefinition(EnsureReadColumnSql, cancellationToken: ct));
        }

        public async Task<IReadOnlyList<BranchManagerNewSaleDTO>> ListAsync(bool unreadOnly, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var sql = @"
SELECT TOP 200
    SaleId, EmployeeId, UserName AS EmployeeName, Status, CustomerId,
    FullName, Phone, Province, Address, NearestLandmark, NationalCardNumber,
    EvaluationLevel, EvaluationNote, FinalSalePrice, DailyInstallment, DownPayment,
    CreatedAt, CompletedAt, BranchManagerReadAtUtc
FROM dbo.SalesDrafts
WHERE Status = N'Completed'
  AND (@UnreadOnly = 0 OR BranchManagerReadAtUtc IS NULL)
ORDER BY CompletedAt DESC, SaleId DESC";
            var rows = (await connection.QueryAsync<BranchManagerNewSaleDTO>(new CommandDefinition(
                sql,
                new { UnreadOnly = unreadOnly ? 1 : 0 },
                cancellationToken: ct))).ToList();
            return rows;
        }

        public async Task<BranchManagerNewSaleDTO?> GetAsync(int saleId, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var header = await connection.QueryFirstOrDefaultAsync<BranchManagerNewSaleDTO>(new CommandDefinition(
                @"SELECT SaleId, EmployeeId, UserName AS EmployeeName, Status, CustomerId,
                         FullName, Phone, Province, Address, NearestLandmark, NationalCardNumber,
                         EvaluationLevel, EvaluationNote, FinalSalePrice, DailyInstallment, DownPayment,
                         CreatedAt, CompletedAt, BranchManagerReadAtUtc
                  FROM dbo.SalesDrafts
                  WHERE SaleId = @SaleId AND Status = N'Completed'",
                new { SaleId = saleId },
                cancellationToken: ct));
            if (header == null)
            {
                return null;
            }

            var items = await connection.QueryAsync<SalesDraftItemDTO>(new CommandDefinition(
                @"SELECT SaleItemId, SaleId, ProductId, ProductName, Quantity, UnitSalePrice, LineSalePrice
                  FROM dbo.SalesDraftItems WHERE SaleId = @SaleId",
                new { SaleId = saleId },
                cancellationToken: ct));
            header.Items = items.ToList();
            return header;
        }

        public async Task<int> CountUnreadAsync(CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                @"SELECT COUNT(1) FROM dbo.SalesDrafts
                  WHERE Status = N'Completed' AND BranchManagerReadAtUtc IS NULL",
                cancellationToken: ct));
        }

        public async Task<bool> MarkReadAsync(int saleId, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var affected = await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.SalesDrafts
SET BranchManagerReadAtUtc = @Now
WHERE SaleId = @SaleId AND Status = N'Completed' AND BranchManagerReadAtUtc IS NULL",
                new { SaleId = saleId, Now = _clock.UtcNow },
                cancellationToken: ct));
            return affected > 0;
        }

        public async Task<int> MarkAllReadAsync(CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            return await connection.ExecuteAsync(new CommandDefinition(
                @"UPDATE dbo.SalesDrafts
SET BranchManagerReadAtUtc = @Now
WHERE Status = N'Completed' AND BranchManagerReadAtUtc IS NULL",
                new { Now = _clock.UtcNow },
                cancellationToken: ct));
        }

        private string RequireConnection()
        {
            return _guard.GetSalesConnectionString()
                   ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
        }
    }
}
