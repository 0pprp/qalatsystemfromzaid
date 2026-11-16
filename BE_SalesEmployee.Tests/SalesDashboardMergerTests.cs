using System.Text.Json.Nodes;
using BE_SalesEmployee.Sales.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class SalesDashboardMergerTests
{
    [Fact]
    public void Merge_SumsBranchesAndRecomputesGrowth()
    {
        var najaf = Branch(4, 400m, 2, 200m, "ايفون", 2, 400m, "علي", "متابع", 2, 300m);
        var basra = Branch(6, 900m, 6, 600m, "ايفون", 1, 100m, "علي", "متابع", 1, 100m);
        basra["items"]!.AsArray().Add(new JsonObject
        {
            ["itemName"] = "دراجة كهربائية",
            ["quantity"] = 3,
            ["salesCount"] = 3,
            ["salePrice"] = 800
        });

        var merged = SalesDashboardMerger.Merge(
            [("najaf", "النجف", najaf), ("basra", "البصرة", basra)],
            ["كربلاء"]);

        Assert.Equal(10, merged["monthSalesCount"]!.GetValue<int>());
        Assert.Equal(1300m, merged["monthSalePrice"]!.GetValue<decimal>());
        Assert.Equal(8, merged["previousMonthSalesCount"]!.GetValue<int>());
        Assert.Equal(25m, merged["salesCountGrowthPercent"]!.GetValue<decimal>());
        Assert.Equal(62.5m, merged["salePriceGrowthPercent"]!.GetValue<decimal>());
        Assert.True(merged["partial"]!.GetValue<bool>());
        Assert.Equal(2, merged["branches"]!.AsArray().Count);

        var items = merged["items"]!.AsArray();
        Assert.Equal(2, items.Count);
        Assert.Equal("ايفون", items[1]!["itemName"]!.GetValue<string>());
        Assert.Equal(3, items[1]!["quantity"]!.GetValue<int>());
        Assert.Equal("هواتف", items[1]!["category"]!.GetValue<string>());
        Assert.Equal("دراجات", items[0]!["category"]!.GetValue<string>());

        var people = merged["recommenders"]!.AsArray();
        Assert.Single(people);
        Assert.Equal(3, people[0]!["salesCount"]!.GetValue<int>());
        Assert.Equal(400m, people[0]!["salePrice"]!.GetValue<decimal>());
    }

    [Fact]
    public void Merge_WeightsSaleScoresAcrossBranches()
    {
        var najaf = Branch(2, 200m, 1, 100m, "ايفون", 1, 100m, "علي", "متابع", 1, 100m);
        najaf["scoredSalesCount"] = 2;
        najaf["highScoreSales"] = 2;
        najaf["lowScoreSales"] = 0;
        najaf["scoreSum"] = 16;
        najaf["sellers"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "أحمد",
                ["salesCount"] = 2,
                ["salePrice"] = 200,
                ["scoredSales"] = 2,
                ["highScores"] = 2,
                ["lowScores"] = 0,
                ["scoreSum"] = 16
            }
        };
        najaf["saleScores"] = new JsonArray
        {
            new JsonObject { ["score"] = 8, ["count"] = 2, ["salePrice"] = 200 }
        };

        var basra = Branch(2, 200m, 1, 100m, "ايفون", 1, 100m, "علي", "متابع", 1, 100m);
        basra["scoredSalesCount"] = 2;
        basra["highScoreSales"] = 0;
        basra["lowScoreSales"] = 2;
        basra["scoreSum"] = 10;
        basra["sellers"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "أحمد",
                ["salesCount"] = 2,
                ["salePrice"] = 200,
                ["scoredSales"] = 2,
                ["highScores"] = 0,
                ["lowScores"] = 2,
                ["scoreSum"] = 10
            }
        };

        var merged = SalesDashboardMerger.Merge(
            [("najaf", "النجف", najaf), ("basra", "البصرة", basra)],
            []);

        Assert.Equal(6.5m, merged["averageSaleScore"]!.GetValue<decimal>());
        Assert.Equal(4, merged["sellers"]!.AsArray()[0]!["scoredSales"]!.GetValue<int>());
        Assert.Equal(6.5m, merged["sellers"]!.AsArray()[0]!["averageScore"]!.GetValue<decimal>());
    }

    [Fact]
    public void Merge_SumsPaymentRatingBands()
    {
        var najaf = Branch(2, 200m, 1, 100m, "ايفون", 1, 100m, "علي", "متابع", 1, 100m);
        najaf["scoredSalesCount"] = 2;
        najaf["excellentSales"] = 1;
        najaf["badSales"] = 1;
        najaf["scoreSum"] = 0;
        najaf["sellers"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "أحمد",
                ["salesCount"] = 2,
                ["salePrice"] = 200,
                ["scoredSales"] = 2,
                ["excellentSales"] = 1,
                ["badSales"] = 1,
                ["scoreSum"] = 0
            }
        };
        najaf["saleScores"] = new JsonArray
        {
            new JsonObject { ["label"] = "ممتاز", ["score"] = 10, ["count"] = 1, ["salePrice"] = 150 },
            new JsonObject { ["label"] = "سيء", ["score"] = -10, ["count"] = 1, ["salePrice"] = 50 }
        };

        var basra = Branch(1, 80m, 1, 80m, "ايفون", 1, 80m, "علي", "متابع", 1, 80m);
        basra["scoredSalesCount"] = 1;
        basra["excellentSales"] = 1;
        basra["scoreSum"] = 10;
        basra["saleScores"] = new JsonArray
        {
            new JsonObject { ["label"] = "ممتاز", ["score"] = 10, ["count"] = 1, ["salePrice"] = 80 }
        };

        var merged = SalesDashboardMerger.Merge(
            [("najaf", "النجف", najaf), ("basra", "البصرة", basra)],
            []);

        Assert.Equal(2, merged["excellentSales"]!.GetValue<int>());
        Assert.Equal(1, merged["badSales"]!.GetValue<int>());
        var scores = merged["saleScores"]!.AsArray();
        Assert.Equal("ممتاز", scores[0]!["label"]!.GetValue<string>());
        Assert.Equal(2, scores[0]!["count"]!.GetValue<int>());
        Assert.Equal("سيء", scores[1]!["label"]!.GetValue<string>());
        Assert.Equal(1, merged["sellers"]!.AsArray()[0]!["excellentSales"]!.GetValue<int>());
        Assert.Equal(1, merged["sellers"]!.AsArray()[0]!["badSales"]!.GetValue<int>());
    }

    [Fact]
    public void AqeelBranches_AreIdentifiedByNameDatabaseOrLink()
    {
        Assert.True(SalesOverviewBranches.IsAqeel("الكرخ - عقيل"));
        Assert.True(SalesOverviewBranches.IsAqeel("الرصافة", "DatabaseCompanyRusafaAqeel"));
        Assert.True(SalesOverviewBranches.IsAqeel("كرخ", link: "http://shortnewkarakaqeel.alsaaeidy.com/api/"));
        Assert.False(SalesOverviewBranches.IsAqeel("النجف", "DatabaseCompanyNajaf"));
        Assert.False(SalesOverviewBranches.IsAqeel("ميسان"));
        Assert.True(SalesOverviewBranches.IsExcluded("الكوت", "DatabaseCompanyKot", "http://sharenewkot.alsaaeidy.com/api/"));
        Assert.True(SalesOverviewBranches.IsExcluded("ميسان", "DatabaseCompanyMaysan", "http://sharenewmaysanl.alsaaeidy.com/api/"));
        Assert.True(SalesOverviewBranches.IsExcluded("ميسان عقيل"));
        Assert.False(SalesOverviewBranches.IsExcluded("النجف", "DatabaseCompanyNajaf", "http://sharenewnajaf.alsaaeidy.com/api/"));
        Assert.False(SalesOverviewBranches.IsExcluded("البصرة", "DatabaseCompanyBasra"));
    }

    [Fact]
    public void Merge_RecomputesCollectedPercentFromSums()
    {
        var najaf = Branch(4, 400m, 2, 200m, "ايفون", 2, 400m, "علي", "متابع", 2, 300m);
        najaf["monthCollected"] = 100;
        var basra = Branch(6, 900m, 6, 600m, "ايفون", 1, 100m, "علي", "متابع", 1, 100m);
        basra["monthCollected"] = 550;

        var merged = SalesDashboardMerger.Merge(
            [("najaf", "النجف", najaf), ("basra", "البصرة", basra)],
            []);

        Assert.Equal(650m, merged["monthCollected"]!.GetValue<decimal>());
        Assert.Equal(50m, merged["collectedPercent"]!.GetValue<decimal>());
    }

    [Fact]
    public void Growth_IsNullWhenPreviousMonthIsEmpty()
    {
        Assert.Null(SalesDashboardMerger.GrowthPercent(4, 0));
        Assert.Equal(0m, SalesDashboardMerger.GrowthPercent(0, 0));
    }

    private static JsonObject Branch(
        int sales,
        decimal price,
        int previousSales,
        decimal previousPrice,
        string item,
        int quantity,
        decimal itemPrice,
        string recommender,
        string role,
        int recommenderSales,
        decimal recommenderPrice) => new()
    {
        ["year"] = 2026,
        ["month"] = 11,
        ["daysInMonth"] = 30,
        ["employeesOnShift"] = 1,
        ["monthSalesCount"] = sales,
        ["monthSalePrice"] = price,
        ["previousMonthSalesCount"] = previousSales,
        ["previousMonthSalePrice"] = previousPrice,
        ["ordersCreated"] = sales,
        ["ordersConverted"] = 1,
        ["daily"] = new JsonArray
        {
            new JsonObject { ["day"] = 1, ["salesCount"] = sales, ["salePrice"] = price }
        },
        ["items"] = new JsonArray
        {
            new JsonObject
            {
                ["itemName"] = item,
                ["quantity"] = quantity,
                ["salesCount"] = quantity,
                ["salePrice"] = itemPrice
            }
        },
        ["recommenders"] = new JsonArray
        {
            new JsonObject
            {
                ["name"] = recommender,
                ["role"] = role,
                ["salesCount"] = recommenderSales,
                ["salePrice"] = recommenderPrice
            }
        },
        ["orderStatuses"] = new JsonArray
        {
            new JsonObject { ["status"] = "New", ["count"] = 2 }
        }
    };
}
