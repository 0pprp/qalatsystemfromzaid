using BE_Company.Sales.Authorization;
using BE_Company.Sales.Rating;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public sealed class DelegateSalesOrderDto
{
    public int RequestId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public DateTime? CreatedAtIraq { get; set; }
    public string Outcome { get; set; } = DelegateSalesOrdersRules.OutcomeOpen;
    public string StatusLabel { get; set; } = "";
    public string? RejectionReason { get; set; }
    /// <summary>New / Old as typed by the delegate.</summary>
    public string? SaleRequestType { get; set; }
    public string? TargetEmployeeName { get; set; }
    /// <summary>Delegate's own shop rating 1–10 (new sales only).</summary>
    public int? ShopRating { get; set; }
    public bool IsWrongSale { get; set; }
    /// <summary>Sales employee evaluation 1–5; 0 = not evaluated.</summary>
    public int EvaluationLevel { get; set; }
    public string EvaluationLabel { get; set; } = "";
    public int? SaleId { get; set; }
    public int? CustomerId { get; set; }
    public DateTime? SoldAtIraq { get; set; }
    public decimal? FinalSalePrice { get; set; }
    /// <summary>Sold only: true = new customer, false = old customer, null = not sold.</summary>
    public bool? IsNewCustomer { get; set; }
    /// <summary>Why it counts as old: «نفس رقم الهاتف» / «نفس الاسم الثلاثي» / «مبيع قديم».</summary>
    public string? OldCustomerReason { get; set; }
    public int? MatchedCustomerId { get; set; }
    public string? MatchedCustomerName { get; set; }
    /// <summary>Current payment rating of the sold customer: ممتاز / جيد / ضعيف / متلكئ / سيء.</summary>
    public string? PaymentRatingLabel { get; set; }
}

public sealed class DelegateSalesOrdersCountDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public int Count { get; set; }
}

public sealed class DelegateSalesOrdersDto
{
    public int ListId { get; set; }
    public int Total { get; set; }
    public int Sold { get; set; }
    public int Rejected { get; set; }
    public int Open { get; set; }
    /// <summary>Sold ÷ (sold + rejected), 0–100.</summary>
    public double ConversionPercent { get; set; }
    public int NewCustomerSales { get; set; }
    public int NewCustomerSalesThisMonth { get; set; }
    public int OldCustomerSales { get; set; }
    public int SoldThisMonth { get; set; }
    public int OrdersThisMonth { get; set; }
    public int WrongSales { get; set; }
    public decimal TotalSoldValue { get; set; }
    public double? AverageShopRating { get; set; }
    public double? AverageDaysToSale { get; set; }
    public IReadOnlyList<DelegateSalesOrdersCountDto> Evaluations { get; set; } = [];
    public IReadOnlyList<DelegateSalesOrdersCountDto> PaymentRatings { get; set; } = [];
    public IReadOnlyList<DelegateSalesOrderDto> Orders { get; set; } = [];
}

public interface IDelegateSalesOrdersService
{
    Task<DelegateSalesOrdersDto> GetAsync(int listId, CancellationToken ct);
}

/// <summary>
/// Sale requests the list's delegate sent (delegate app: CustomerSourceType «Delegate», creator or SourceListId = list).
/// </summary>
public sealed class DelegateSalesOrdersService : IDelegateSalesOrdersService
{
    private readonly SalesDevelopmentGuard _guard;
    private readonly IRatingDataSource _rating;

    public DelegateSalesOrdersService(SalesDevelopmentGuard guard, IRatingDataSource rating)
    {
        _guard = guard;
        _rating = rating;
    }

    private sealed class Row
    {
        public int Id { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
        public string? Status { get; set; }
        public string? FilterStatus { get; set; }
        public bool IsDeleted { get; set; }
        public string? RejectionReason { get; set; }
        public string? SaleRequestType { get; set; }
        public int? ExistingCustomerId { get; set; }
        public string? TargetEmployeeName { get; set; }
        public int? ShopRating { get; set; }
        public bool IsWrongSale { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public int? SaleId { get; set; }
        public string? DraftStatus { get; set; }
        public int? EvaluationLevel { get; set; }
        public int? DraftCustomerId { get; set; }
        public string? DraftFullName { get; set; }
        public string? DraftPhone { get; set; }
        public decimal? FinalSalePrice { get; set; }
        public DateTime? DraftCompletedAt { get; set; }
    }

    private const string OrdersSql = @"
SELECT R.Id, R.CustomerName, R.CustomerPhone, R.CreatedAtUtc, R.Status, R.FilterStatus,
       CAST(ISNULL(R.IsDeleted, 0) AS bit) AS IsDeleted,
       COALESCE(NULLIF(LTRIM(RTRIM(R.RejectionReason)), N''), NULLIF(LTRIM(RTRIM(R.FilterRejectReason)), N''), R.DeleteReason) AS RejectionReason,
       R.SaleRequestType, R.ExistingCustomerId, R.TargetEmployeeName, R.ShopRating,
       CAST(ISNULL(R.IsWrongSale, 0) AS bit) AS IsWrongSale, R.CompletedAtUtc,
       D.SaleId, D.Status AS DraftStatus, D.EvaluationLevel, D.CustomerId AS DraftCustomerId,
       D.FullName AS DraftFullName, D.Phone AS DraftPhone, D.FinalSalePrice, D.CompletedAt AS DraftCompletedAt
FROM dbo.SalesRequests R
OUTER APPLY (
    SELECT TOP 1 d.SaleId, d.Status, d.EvaluationLevel, d.CustomerId, d.FullName, d.Phone, d.FinalSalePrice, d.CompletedAt
    FROM dbo.SalesDrafts d
    WHERE d.SalesRequestId = R.Id OR (R.ConvertedToSaleId IS NOT NULL AND d.SaleId = R.ConvertedToSaleId)
    ORDER BY CASE WHEN d.Status = N'Completed' THEN 0 ELSE 1 END, d.SaleId DESC
) D
WHERE (R.CustomerSourceType = N'Delegate'
       OR R.CreatedByUserType = N'Delegate'
       OR (R.CreatedByUserType LIKE N'%مندوب%' AND R.CreatedByUserType NOT LIKE N'%موظف%'))
  AND (R.SourceListId = @ListId OR R.CreatedByUserId = @ListId)
ORDER BY R.CreatedAtUtc DESC, R.Id DESC;";

    public async Task<DelegateSalesOrdersDto> GetAsync(int listId, CancellationToken ct)
    {
        var result = new DelegateSalesOrdersDto { ListId = listId };
        if (listId <= 0)
        {
            return result;
        }

        var cs = _guard.GetSalesConnectionString()
                 ?? throw new InvalidOperationException("لا يوجد اتصال بقاعدة بيانات الفرع.");
        List<Row> rows;
        await using (var connection = new SqlConnection(cs))
        {
            var exists = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT CASE WHEN OBJECT_ID(N'dbo.SalesRequests', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.SalesDrafts', N'U') IS NOT NULL THEN 1 ELSE 0 END",
                cancellationToken: ct));
            if (exists == 0)
            {
                return result;
            }

            rows = (await connection.QueryAsync<Row>(new CommandDefinition(
                OrdersSql, new { ListId = listId }, commandTimeout: 120, cancellationToken: ct))).ToList();
        }

        var orders = rows.Select(ToDto).ToList();
        var sold = orders.Where(o => o.Outcome == DelegateSalesOrdersRules.OutcomeSold).ToList();
        if (sold.Count > 0)
        {
            await MarkNewOrOldAsync(cs, rows, orders, ct);
            await AddPaymentRatingsAsync(sold, ct);
        }

        Summarize(result, orders, IraqToday());
        return result;
    }

    private static DelegateSalesOrderDto ToDto(Row r)
    {
        var outcome = DelegateSalesOrdersRules.Outcome(r.Status, r.FilterStatus, r.IsDeleted, r.DraftStatus);
        var eval = DelegateSalesOrdersRules.EvaluationBucket(r.EvaluationLevel);
        return new DelegateSalesOrderDto
        {
            RequestId = r.Id,
            CustomerName = r.DraftFullName ?? r.CustomerName,
            CustomerPhone = r.DraftPhone ?? r.CustomerPhone,
            CreatedAtIraq = ToIraq(r.CreatedAtUtc),
            Outcome = outcome,
            StatusLabel = DelegateSalesOrdersRules.StatusLabel(outcome, r.Status, r.FilterStatus),
            RejectionReason = outcome == DelegateSalesOrdersRules.OutcomeRejected ? r.RejectionReason : null,
            SaleRequestType = r.SaleRequestType,
            TargetEmployeeName = r.TargetEmployeeName,
            ShopRating = r.ShopRating,
            IsWrongSale = r.IsWrongSale,
            EvaluationLevel = eval,
            EvaluationLabel = DelegateSalesOrdersRules.EvaluationBuckets.First(b => b.Level == eval).Label,
            SaleId = r.SaleId,
            CustomerId = outcome == DelegateSalesOrdersRules.OutcomeSold ? r.DraftCustomerId : null,
            SoldAtIraq = outcome == DelegateSalesOrdersRules.OutcomeSold
                ? ToIraq(r.CompletedAtUtc) ?? r.DraftCompletedAt
                : null,
            FinalSalePrice = outcome == DelegateSalesOrdersRules.OutcomeSold ? r.FinalSalePrice : null
        };
    }

    private static async Task MarkNewOrOldAsync(
        string cs, IReadOnlyList<Row> rows, IReadOnlyList<DelegateSalesOrderDto> orders, CancellationToken ct)
    {
        List<DelegateSalesOrdersRules.ExistingCustomer> customers;
        await using (var connection = new SqlConnection(cs))
        {
            customers = (await connection.QueryAsync<DelegateSalesOrdersRules.ExistingCustomer>(new CommandDefinition(
                "SELECT CustomerID AS CustomerId, CustomerName AS Name, PhoneNumber AS Phone FROM dbo.Customers",
                commandTimeout: 120, cancellationToken: ct))).ToList();
        }

        var index = new DelegateSalesOrdersRules.CustomerIndex(customers);
        for (var i = 0; i < orders.Count; i++)
        {
            var o = orders[i];
            if (o.Outcome != DelegateSalesOrdersRules.OutcomeSold)
            {
                continue;
            }

            var r = rows[i];
            if (DelegateSalesOrdersRules.IsDeclaredOld(r.SaleRequestType, r.ExistingCustomerId))
            {
                o.IsNewCustomer = false;
                o.OldCustomerReason = "مبيع قديم (زبون موجود)";
                o.MatchedCustomerId = r.ExistingCustomerId;
                continue;
            }

            var hit = index.FindEarlier(o.CustomerName, o.CustomerPhone, o.CustomerId);
            if (hit is { } h)
            {
                o.IsNewCustomer = false;
                o.OldCustomerReason = h.Reason;
                o.MatchedCustomerId = h.Customer.CustomerId;
                o.MatchedCustomerName = h.Customer.Name;
            }
            else
            {
                o.IsNewCustomer = true;
            }
        }
    }

    private async Task AddPaymentRatingsAsync(IReadOnlyList<DelegateSalesOrderDto> sold, CancellationToken ct)
    {
        var ids = sold.Where(o => o.CustomerId is > 0).Select(o => o.CustomerId!.Value).Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        try
        {
            var facts = await _rating.GetFactsByCustomerIdsAsync(ids, ct);
            var rated = CustomerRatingCalculator.EvaluateMany(facts, IraqToday(), usePhantomSaleDate: false)
                .GroupBy(r => r.CustomerId)
                .ToDictionary(g => g.Key, g => CustomerRatingLabels.FromLevel(g.First().Level));
            foreach (var o in sold)
            {
                if (o.CustomerId is int id && rated.TryGetValue(id, out var label))
                {
                    o.PaymentRatingLabel = label;
                }
            }
        }
        catch
        {
            // Ratings are a bonus on this card — never fail the orders list.
        }
    }

    /// <summary>All counts the FE shows; each matches a filter over <see cref="DelegateSalesOrdersDto.Orders"/>.</summary>
    public static void Summarize(DelegateSalesOrdersDto dto, IReadOnlyList<DelegateSalesOrderDto> orders, DateTime iraqToday)
    {
        var sold = orders.Where(o => o.Outcome == DelegateSalesOrdersRules.OutcomeSold).ToList();
        dto.Orders = orders;
        dto.Total = orders.Count;
        dto.Sold = sold.Count;
        dto.Rejected = orders.Count(o => o.Outcome == DelegateSalesOrdersRules.OutcomeRejected);
        dto.Open = orders.Count(o => o.Outcome == DelegateSalesOrdersRules.OutcomeOpen);
        var decided = dto.Sold + dto.Rejected;
        dto.ConversionPercent = decided == 0 ? 0 : Math.Round(dto.Sold * 100.0 / decided, 1);
        dto.NewCustomerSales = sold.Count(o => o.IsNewCustomer == true);
        dto.NewCustomerSalesThisMonth = sold.Count(o =>
            o.IsNewCustomer == true && DelegateSalesOrdersRules.InMonth(o.SoldAtIraq, iraqToday));
        dto.OldCustomerSales = sold.Count(o => o.IsNewCustomer == false);
        dto.SoldThisMonth = sold.Count(o => DelegateSalesOrdersRules.InMonth(o.SoldAtIraq, iraqToday));
        dto.OrdersThisMonth = orders.Count(o => DelegateSalesOrdersRules.InMonth(o.CreatedAtIraq, iraqToday));
        dto.WrongSales = orders.Count(o => o.IsWrongSale);
        dto.TotalSoldValue = sold.Sum(o => o.FinalSalePrice ?? 0);
        var shop = orders.Where(o => o.ShopRating is >= 1 and <= 10).Select(o => (double)o.ShopRating!.Value).ToList();
        dto.AverageShopRating = shop.Count == 0 ? null : Math.Round(shop.Average(), 1);
        var days = sold.Where(o => o.SoldAtIraq != null && o.CreatedAtIraq != null)
            .Select(o => Math.Max(0, (o.SoldAtIraq!.Value.Date - o.CreatedAtIraq!.Value.Date).TotalDays)).ToList();
        dto.AverageDaysToSale = days.Count == 0 ? null : Math.Round(days.Average(), 1);
        dto.Evaluations = DelegateSalesOrdersRules.EvaluationBuckets
            .Select(b => new DelegateSalesOrdersCountDto
            {
                Key = b.Level.ToString(),
                Label = b.Label,
                Count = orders.Count(o => o.EvaluationLevel == b.Level)
            })
            .ToList();
        string[] payLabels =
        [
            CustomerRatingLabels.Excellent, CustomerRatingLabels.Good, CustomerRatingLabels.Weak,
            CustomerRatingLabels.Rejected, CustomerRatingLabels.Bad
        ];
        dto.PaymentRatings = payLabels
            .Select(l => new DelegateSalesOrdersCountDto
            {
                Key = l,
                Label = l,
                Count = sold.Count(o => o.PaymentRatingLabel == l)
            })
            .ToList();
    }

    private static DateTime? ToIraq(DateTime? utc) =>
        utc is DateTime d ? DateTime.SpecifyKind(d.AddHours(3), DateTimeKind.Unspecified) : null;

    private static DateTime IraqToday() => DateTime.UtcNow.AddHours(3).Date;
}
