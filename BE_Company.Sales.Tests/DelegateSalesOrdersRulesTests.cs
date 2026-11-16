using BE_Company.Sales.Services;
using Xunit;
using R = BE_Company.Sales.Services.DelegateSalesOrdersRules;

namespace BE_Company.Sales.Tests;

/// <summary>«طلبات بيع المندوب» on متابعة المندوبين.</summary>
public sealed class DelegateSalesOrdersRulesTests
{
    [Theory]
    [InlineData("Completed", "ReadyForSale", false, null, R.OutcomeSold)]
    [InlineData("InProgress", "ReadyForSale", false, "Completed", R.OutcomeSold)]
    [InlineData("Rejected", "ReadyForSale", false, null, R.OutcomeRejected)]
    [InlineData("New", "Rejected", false, null, R.OutcomeRejected)]
    [InlineData("Assigned", "ReadyForSale", true, null, R.OutcomeRejected)]
    [InlineData("InProgress", "ReadyForSale", false, "Rejected", R.OutcomeRejected)]
    [InlineData("Completed", "ReadyForSale", true, null, R.OutcomeSold)]
    [InlineData("PreparedForSale", "ReadyForSale", false, "Pending", R.OutcomeOpen)]
    [InlineData("New", "PendingFilter", false, null, R.OutcomeOpen)]
    public void Outcome_Buckets(string status, string filter, bool deleted, string? draft, string expected) =>
        Assert.Equal(expected, R.Outcome(status, filter, deleted, draft));

    [Theory]
    [InlineData(5, 5)]
    [InlineData(1, 1)]
    [InlineData(null, 0)]
    [InlineData(9, 0)]
    public void EvaluationBucket_UnknownIsZero(int? level, int expected) =>
        Assert.Equal(expected, R.EvaluationBucket(level));

    private static R.CustomerIndex Index() => new([
        new R.ExistingCustomer(10, "علي حسين محمد كاظم", "07701234567"),
        new R.ExistingCustomer(20, "أحمد جاسم عبد", "07800000000"),
        new R.ExistingCustomer(50, "زيد كريم سالم", "07711111111"),
    ]);

    [Fact]
    public void PhoneMatch_MakesOldCustomer()
    {
        var hit = Index().FindEarlier("شخص آخر تماما", "+964 770 123 4567", ownCustomerId: 99);
        Assert.NotNull(hit);
        Assert.Equal(10, hit!.Value.Customer.CustomerId);
        Assert.Equal("نفس رقم الهاتف", hit.Value.Reason);
    }

    [Fact]
    public void TripleNameMatch_IgnoresHamzaAndFourthName()
    {
        var hit = Index().FindEarlier("احمد جاسم عبد الله", "07999999999", ownCustomerId: 99);
        Assert.NotNull(hit);
        Assert.Equal(20, hit!.Value.Customer.CustomerId);
        Assert.Equal("نفس الاسم الثلاثي", hit.Value.Reason);
    }

    [Fact]
    public void OwnCustomerRow_AndLaterCustomers_DoNotCount()
    {
        // The sale created customer 50 itself — no earlier match means a new customer.
        Assert.Null(Index().FindEarlier("زيد كريم سالم", "07711111111", ownCustomerId: 50));
        // Customer 50 was created after this sale's customer 30 → still new.
        Assert.Null(Index().FindEarlier("زيد كريم سالم", null, ownCustomerId: 30));
    }

    [Fact]
    public void TwoPartName_NeverMatchesByName()
    {
        Assert.Null(R.TripleKey("علي حسين"));
        Assert.Null(Index().FindEarlier("علي حسين", null, ownCustomerId: 99));
    }

    [Theory]
    [InlineData("Old", null, true)]
    [InlineData("New", 15, true)]
    [InlineData("New", null, false)]
    [InlineData(null, 0, false)]
    public void DeclaredOld(string? type, int? existing, bool expected) =>
        Assert.Equal(expected, R.IsDeclaredOld(type, existing));

    [Fact]
    public void Summarize_CountsMatchFilters()
    {
        var today = new DateTime(2026, 11, 15);
        var orders = new List<DelegateSalesOrderDto>
        {
            Sold(1, new DateTime(2026, 11, 3), isNew: true, eval: 5, pay: "ممتاز", price: 1_000_000),
            Sold(2, new DateTime(2026, 10, 20), isNew: true, eval: 4, pay: "جيد", price: 500_000),
            Sold(3, new DateTime(2026, 11, 10), isNew: false, eval: 3, pay: "سيء", price: 700_000),
            new() { RequestId = 4, Outcome = R.OutcomeRejected, EvaluationLevel = 1, CreatedAtIraq = new DateTime(2026, 11, 1), IsWrongSale = true, ShopRating = 3 },
            new() { RequestId = 5, Outcome = R.OutcomeOpen, EvaluationLevel = 0, CreatedAtIraq = new DateTime(2026, 11, 12), ShopRating = 9 },
        };

        var dto = new DelegateSalesOrdersDto();
        DelegateSalesOrdersService.Summarize(dto, orders, today);

        Assert.Equal(5, dto.Total);
        Assert.Equal(3, dto.Sold);
        Assert.Equal(1, dto.Rejected);
        Assert.Equal(1, dto.Open);
        Assert.Equal(75.0, dto.ConversionPercent);
        Assert.Equal(2, dto.NewCustomerSales);
        Assert.Equal(1, dto.NewCustomerSalesThisMonth);
        Assert.Equal(1, dto.OldCustomerSales);
        Assert.Equal(2, dto.SoldThisMonth);
        Assert.Equal(1, dto.WrongSales);
        Assert.Equal(2_200_000m, dto.TotalSoldValue);
        Assert.Equal(6.0, dto.AverageShopRating);
        Assert.Equal(1, dto.Evaluations.Single(e => e.Label == "ممتاز").Count);
        Assert.Equal(1, dto.Evaluations.Single(e => e.Label == "مرفوض").Count);
        Assert.Equal(1, dto.Evaluations.Single(e => e.Label == "بدون تقييم").Count);
        Assert.Equal(1, dto.PaymentRatings.Single(p => p.Label == "سيء").Count);
        Assert.Equal(0, dto.PaymentRatings.Single(p => p.Label == "ضعيف").Count);
    }

    private static DelegateSalesOrderDto Sold(int id, DateTime soldAt, bool isNew, int eval, string pay, decimal price) => new()
    {
        RequestId = id,
        Outcome = R.OutcomeSold,
        CreatedAtIraq = soldAt.AddDays(-2),
        SoldAtIraq = soldAt,
        IsNewCustomer = isNew,
        EvaluationLevel = eval,
        PaymentRatingLabel = pay,
        FinalSalePrice = price,
    };
}
