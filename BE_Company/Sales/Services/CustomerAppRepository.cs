using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public sealed class CustomerAppAccountRow
    {
        public int CustomerID { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? SaleDate { get; set; }
        public long TotalAmount { get; set; }
        public long DailyInstallment { get; set; }
        public long PaidAmount { get; set; }
        public long RemainingAmount { get; set; }
        public int PaymentsCount { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }

    public sealed class CustomerAppItemRow
    {
        public int CustomerID { get; set; }
        public int CustomerSaleID { get; set; }
        public string? ItemName { get; set; }
        public int Quantity { get; set; }
    }

    public sealed class CustomerAppSaleRow
    {
        public int CustomerSaleID { get; set; }
        public DateTime? DateCreate { get; set; }
        public long TotalAmount { get; set; }
        public long DailyInstallment { get; set; }
    }

    public sealed class CustomerAppStatusRow
    {
        public int Id { get; set; }
        public Guid ExternalCrmOrderUuid { get; set; }
        public string? Status { get; set; }
        public string? FilterStatus { get; set; }
        public bool IsDeleted { get; set; }
        public string? RejectionReason { get; set; }
        public string? DeleteReason { get; set; }
        public int? ConvertedToSaleId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? AssignedAtUtc { get; set; }
        public DateTime? ViewedAtUtc { get; set; }
        public DateTime? ProcessingAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
        public DateTime? LastHistoryAtUtc { get; set; }
    }

    public interface ICustomerAppRepository
    {
        /// <summary>Non-fake customers whose stored phone matches (last 10 digits) and that have at least one sale.</summary>
        Task<IReadOnlyList<CustomerAppAccountRow>> ListAccountsByPhoneAsync(string matchKey, CancellationToken ct);
        Task<CustomerAppAccountRow?> GetAccountByIdAndPhoneAsync(int customerId, string matchKey, CancellationToken ct);
        Task<IReadOnlyList<CustomerAppItemRow>> ListItemsAsync(IReadOnlyCollection<int> customerIds, CancellationToken ct);
        Task<IReadOnlyList<CustomerAppSaleRow>> ListSalesAsync(int customerId, CancellationToken ct);
        Task<IReadOnlyList<CustomerAppPaymentDTO>> ListPaymentsAsync(int customerId, CancellationToken ct);
        Task<IReadOnlyList<CustomerAppStatusRow>> ListStatusesAsync(IReadOnlyCollection<Guid> orderUuids, CancellationToken ct);

        /// <summary>The account posted from a sales draft (SalesDrafts.CustomerId), or null until the draft is posted.</summary>
        Task<CustomerAppAccountRow?> GetAccountBySaleDraftAsync(int saleId, CancellationToken ct);
    }

    /// <summary>
    /// Read-only Dapper queries over the branch's existing views. Totals come from View_CustomersDelegate
    /// (already IQD-rounded); per-sale and per-payment figures come from the *Delegate views it is built on,
    /// so the detail page adds up to the same totals.
    /// </summary>
    public sealed class CustomerAppRepository : ICustomerAppRepository
    {
        private readonly SalesDevelopmentGuard _guard;

        public CustomerAppRepository(SalesDevelopmentGuard guard)
        {
            _guard = guard;
        }

        private static readonly string PhoneFilterSql =
            "ISNULL(C.IsFakeSale, 0) = 0 AND " + CustomerAppPhoneMatch.MatchPredicateSql("C.PhoneNumber", "@Phone");

        private const string AccountSelectSql = @"
SELECT D.CustomerID,
       D.CustomerName,
       D.DateSaleDevice AS SaleDate,
       CAST(ROUND(ISNULL(D.AmountTotalSales, 0), 0) AS BIGINT) AS TotalAmount,
       CAST(ROUND(ISNULL(D.AmountDaySales, 0), 0) AS BIGINT) AS DailyInstallment,
       CAST(ROUND(ISNULL(D.ReceiptsTotal, 0), 0) AS BIGINT) AS PaidAmount,
       CAST(ROUND(ISNULL(D.AmountRemaining, 0), 0) AS BIGINT) AS RemainingAmount,
       CAST(ISNULL(D.CountReceiptDevice, 0) AS INT) AS PaymentsCount,
       D.LastPaymentDate
FROM dbo.View_CustomersDelegate D";

        public async Task<IReadOnlyList<CustomerAppAccountRow>> ListAccountsByPhoneAsync(string matchKey, CancellationToken ct)
        {
            await using var connection = new SqlConnection(RequireConnection());
            var rows = await connection.QueryAsync<CustomerAppAccountRow>(new CommandDefinition(
                AccountSelectSql + @"
WHERE D.CustomerID IN (SELECT C.CustomerID FROM dbo.Customers C WHERE " + PhoneFilterSql + @")
  AND EXISTS (SELECT 1 FROM dbo.CustomersSales S WHERE S.CustomerID = D.CustomerID)
ORDER BY D.DateSaleDevice DESC, D.CustomerID DESC",
                new { Phone = matchKey },
                cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<CustomerAppAccountRow?> GetAccountByIdAndPhoneAsync(int customerId, string matchKey, CancellationToken ct)
        {
            await using var connection = new SqlConnection(RequireConnection());
            return await connection.QueryFirstOrDefaultAsync<CustomerAppAccountRow>(new CommandDefinition(
                AccountSelectSql + @"
WHERE D.CustomerID = @CustomerId
  AND D.CustomerID IN (SELECT C.CustomerID FROM dbo.Customers C WHERE C.CustomerID = @CustomerId AND " + PhoneFilterSql + ")",
                new { CustomerId = customerId, Phone = matchKey },
                cancellationToken: ct));
        }

        public async Task<CustomerAppAccountRow?> GetAccountBySaleDraftAsync(int saleId, CancellationToken ct)
        {
            await using var connection = new SqlConnection(RequireConnection());
            try
            {
                return await connection.QueryFirstOrDefaultAsync<CustomerAppAccountRow>(new CommandDefinition(
                    AccountSelectSql + @"
WHERE D.CustomerID = (SELECT TOP 1 S.CustomerId FROM dbo.SalesDrafts S WHERE S.SaleId = @SaleId AND S.CustomerId IS NOT NULL)",
                    new { SaleId = saleId },
                    cancellationToken: ct));
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                // Branch never used sales drafts (no dbo.SalesDrafts yet).
                return null;
            }
        }

        public async Task<IReadOnlyList<CustomerAppItemRow>> ListItemsAsync(IReadOnlyCollection<int> customerIds, CancellationToken ct)
        {
            if (customerIds.Count == 0)
            {
                return [];
            }

            await using var connection = new SqlConnection(RequireConnection());
            var rows = await connection.QueryAsync<CustomerAppItemRow>(new CommandDefinition(@"
SELECT V.CustomerID,
       V.CustomerSaleID,
       V.ItemName,
       CAST(ROUND(SUM(CAST(ISNULL(V.Quantity, 0) AS DECIMAL(18, 3))), 0) AS INT) AS Quantity
FROM dbo.View_SelectItemsSalesItemsNames V
WHERE V.CustomerID IN @Ids
GROUP BY V.CustomerID, V.CustomerSaleID, V.ItemName",
                new { Ids = customerIds.Distinct().ToArray() },
                cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<IReadOnlyList<CustomerAppSaleRow>> ListSalesAsync(int customerId, CancellationToken ct)
        {
            await using var connection = new SqlConnection(RequireConnection());
            var rows = await connection.QueryAsync<CustomerAppSaleRow>(new CommandDefinition(@"
SELECT S.CustomerSaleID,
       S.DateCreate,
       CAST(ROUND(ISNULL(S.AmountTotalSalesDenar, 0), 0) AS BIGINT) AS TotalAmount,
       CAST(ROUND(ISNULL(S.AmountDaySalesDenar, 0), 0) AS BIGINT) AS DailyInstallment
FROM dbo.View_CustomersSalesDelegate S
WHERE S.CustomerID = @CustomerId
ORDER BY S.DateCreate DESC, S.CustomerSaleID DESC",
                new { CustomerId = customerId },
                cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<IReadOnlyList<CustomerAppPaymentDTO>> ListPaymentsAsync(int customerId, CancellationToken ct)
        {
            await using var connection = new SqlConnection(RequireConnection());
            var rows = await connection.QueryAsync<CustomerAppPaymentDTO>(new CommandDefinition(@"
SELECT P.CustomerPaymentID AS PaymentId,
       P.PaymentDate AS [Date],
       CAST(ROUND(ISNULL(P.AmountDenar, 0), 0) AS BIGINT) AS Amount
FROM dbo.View_CustomersPaymentsDelegate P
WHERE P.CustomerID = @CustomerId
ORDER BY P.PaymentDate DESC, P.CustomerPaymentID DESC",
                new { CustomerId = customerId },
                cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<IReadOnlyList<CustomerAppStatusRow>> ListStatusesAsync(IReadOnlyCollection<Guid> orderUuids, CancellationToken ct)
        {
            if (orderUuids.Count == 0)
            {
                return [];
            }

            await using var connection = new SqlConnection(RequireConnection());
            var rows = await connection.QueryAsync<CustomerAppStatusRow>(new CommandDefinition(@"
SELECT R.Id, R.ExternalCrmOrderUuid, R.Status, R.FilterStatus, ISNULL(R.IsDeleted, 0) AS IsDeleted,
       R.RejectionReason, R.DeleteReason, R.ConvertedToSaleId,
       R.CreatedAtUtc, R.AssignedAtUtc, R.ViewedAtUtc, R.ProcessingAtUtc, R.CompletedAtUtc, R.RejectedAtUtc, R.DeletedAtUtc,
       (SELECT MAX(h.CreatedAtUtc) FROM dbo.SalesRequestHistory h WHERE h.RequestId = R.Id) AS LastHistoryAtUtc
FROM dbo.SalesRequests R
WHERE R.ExternalCrmOrderUuid IN @Uuids",
                new { Uuids = orderUuids.Distinct().ToArray() },
                cancellationToken: ct));
            return rows.ToList();
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
    }
}
