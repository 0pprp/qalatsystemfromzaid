using System.Text.Json.Nodes;
using BE_SalesEmployee.Sales.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class SalesDayDetailMergerTests
{
    [Fact]
    public void Merge_SumsDaySalesOrdersAndAgents()
    {
        var najaf = new JsonObject
        {
            ["year"] = 2026,
            ["month"] = 11,
            ["day"] = 1,
            ["salesCount"] = 2,
            ["salePrice"] = 300,
            ["downPayment"] = 40,
            ["itemQuantity"] = 3,
            ["ordersAdded"] = 4,
            ["ordersCompleted"] = 1,
            ["ordersRejected"] = 0,
            ["sellers"] = new JsonArray
            {
                new JsonObject { ["name"] = "أحمد", ["count"] = 2, ["salePrice"] = 300 }
            },
            ["creators"] = new JsonArray
            {
                new JsonObject { ["name"] = "علي", ["role"] = "متابع", ["count"] = 3 }
            },
            ["agents"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "أحمد",
                    ["orders"] = 2,
                    ["statuses"] = new JsonArray
                    {
                        new JsonObject { ["status"] = "Completed", ["count"] = 1 },
                        new JsonObject { ["status"] = "Pending", ["count"] = 1 }
                    }
                }
            },
            ["orderStatuses"] = new JsonArray
            {
                new JsonObject { ["status"] = "Completed", ["count"] = 1 }
            },
            ["items"] = new JsonArray
            {
                new JsonObject { ["itemName"] = "ايفون", ["quantity"] = 2, ["salesCount"] = 2, ["salePrice"] = 200 }
            }
        };
        var basra = new JsonObject
        {
            ["salesCount"] = 1,
            ["salePrice"] = 100,
            ["ordersAdded"] = 1,
            ["sellers"] = new JsonArray
            {
                new JsonObject { ["name"] = "أحمد", ["count"] = 1, ["salePrice"] = 100 }
            },
            ["creators"] = new JsonArray
            {
                new JsonObject { ["name"] = "علي", ["role"] = "متابع", ["count"] = 1 }
            },
            ["agents"] = new JsonArray
            {
                new JsonObject
                {
                    ["name"] = "أحمد",
                    ["orders"] = 1,
                    ["statuses"] = new JsonArray
                    {
                        new JsonObject { ["status"] = "Pending", ["count"] = 1 }
                    }
                }
            },
            ["orderStatuses"] = new JsonArray
            {
                new JsonObject { ["status"] = "Pending", ["count"] = 1 }
            }
        };

        var merged = SalesDayDetailMerger.Merge(
            [("1", "النجف", najaf), ("9", "البصرة", basra)],
            ["كربلاء"]);

        Assert.Equal(3, merged["salesCount"]!.GetValue<int>());
        Assert.Equal(400m, merged["salePrice"]!.GetValue<decimal>());
        Assert.Equal(5, merged["ordersAdded"]!.GetValue<int>());
        Assert.Equal(3, merged["sellers"]!.AsArray()[0]!["count"]!.GetValue<int>());
        Assert.Equal(4, merged["creators"]!.AsArray()[0]!["count"]!.GetValue<int>());
        Assert.Equal(3, merged["agents"]!.AsArray()[0]!["orders"]!.GetValue<int>());
        Assert.Equal(2, merged["agents"]!.AsArray()[0]!["statuses"]!.AsArray()[0]!["count"]!.GetValue<int>());
        Assert.Equal("Pending", merged["agents"]!.AsArray()[0]!["statuses"]!.AsArray()[0]!["status"]!.GetValue<string>());
        Assert.True(merged["partial"]!.GetValue<bool>());
        Assert.Equal(2, merged["branches"]!.AsArray().Count);
    }
}
