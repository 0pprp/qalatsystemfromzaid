using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class DirectorFollowerSaleAuthDedupeRulesTests
{
    [Fact]
    public void Deduplicate_SameCustomerDifferentLists_KeepsOneLatestAuthenticated()
    {
        var older = new DirectorFollowerSaleAuthRowDto
        {
            CustomerId = 100,
            ListId = 1,
            ListName = "الرشيدية",
            Authenticated = true,
            AuthenticatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RemainingAmount = 2_653_000,
        };
        var newer = new DirectorFollowerSaleAuthRowDto
        {
            CustomerId = 100,
            ListId = 2,
            ListName = "الامين",
            Authenticated = true,
            AuthenticatedAtUtc = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            RemainingAmount = 2_653_000,
        };

        var result = DirectorFollowerSaleAuthDedupeRules.Deduplicate([older, newer]);

        Assert.Single(result);
        Assert.Equal(2, result[0].ListId);
        Assert.Equal("الامين", result[0].ListName);
    }

    [Fact]
    public void Deduplicate_AuthenticatedBeatsPending_SameCustomer()
    {
        var pending = new DirectorFollowerSaleAuthRowDto
        {
            CustomerId = 50,
            ListId = 9,
            Authenticated = false,
            SaleDraftId = 700,
        };
        var done = new DirectorFollowerSaleAuthRowDto
        {
            CustomerId = 50,
            ListId = 8,
            Authenticated = true,
            AuthenticatedAtUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            SaleDraftId = 700,
        };

        var result = DirectorFollowerSaleAuthDedupeRules.Deduplicate([pending, done]);

        Assert.Single(result);
        Assert.True(result[0].Authenticated);
        Assert.Equal(8, result[0].ListId);
    }

    [Fact]
    public void Deduplicate_SameSaleDraftId_DifferentCustomers_KeepsOne()
    {
        var a = new DirectorFollowerSaleAuthRowDto
        {
            CustomerId = 1,
            SaleDraftId = 55,
            ListId = 10,
            Authenticated = true,
            AuthenticatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var b = new DirectorFollowerSaleAuthRowDto
        {
            CustomerId = 1,
            SaleDraftId = 55,
            ListId = 11,
            Authenticated = true,
            AuthenticatedAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var result = DirectorFollowerSaleAuthDedupeRules.Deduplicate([a, b]);

        Assert.Single(result);
        Assert.Equal(11, result[0].ListId);
    }

    [Fact]
    public void Deduplicate_DistinctCustomers_Unchanged()
    {
        var a = new DirectorFollowerSaleAuthRowDto { CustomerId = 1, ListId = 1, Authenticated = true };
        var b = new DirectorFollowerSaleAuthRowDto { CustomerId = 2, ListId = 1, Authenticated = true };

        var result = DirectorFollowerSaleAuthDedupeRules.Deduplicate([a, b]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void SaleKey_PrefersSaleDraftIdThenCustomerSaleIdThenCustomerId()
    {
        Assert.Equal("SD:9", DirectorFollowerSaleAuthDedupeRules.SaleKey(
            new DirectorFollowerSaleAuthRowDto { CustomerId = 1, SaleDraftId = 9, CustomerSaleId = 3 }));
        Assert.Equal("CS:3", DirectorFollowerSaleAuthDedupeRules.SaleKey(
            new DirectorFollowerSaleAuthRowDto { CustomerId = 1, CustomerSaleId = 3 }));
        Assert.Equal("C:1", DirectorFollowerSaleAuthDedupeRules.SaleKey(
            new DirectorFollowerSaleAuthRowDto { CustomerId = 1 }));
    }
}
