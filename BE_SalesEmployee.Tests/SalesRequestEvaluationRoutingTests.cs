using System.Text.Json.Nodes;
using BE_SalesEmployee.Sales.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// Evaluation routes each request only to its sourceCity branch (local province scope).
/// </summary>
public sealed class SalesRequestEvaluationRoutingTests
{
    [Fact]
    public void Group_By_SourceCity_Batches_Same_Branch()
    {
        var body = """
        {
          "items": [
            { "requestId": 29, "sourceCityValue": "karkh", "customerName": "صادق جعفر حنيو", "customerPhone": "07729160098" },
            { "requestId": 30, "sourceCityValue": "karkh", "customerName": "علي", "customerPhone": "07801110000" },
            { "requestId": 10, "sourceCityValue": "najaf", "customerName": "أحمد", "customerPhone": "07802220000" }
          ]
        }
        """;

        var groups = SalesRequestEvaluationRouter.GroupItemsBySourceCity(body);
        Assert.Equal(2, groups.Count);
        Assert.Equal(2, groups["karkh"].Count);
        Assert.Equal(1, groups["najaf"].Count);
        Assert.Equal(new[] { 29, 30 }, groups["karkh"].Select(SalesRequestEvaluationRouter.ReadRequestId).OrderBy(x => x));
    }

    [Fact]
    public void Build_Branch_Payload_Contains_Only_That_City_Items()
    {
        var groups = SalesRequestEvaluationRouter.GroupItemsBySourceCity("""
        {
          "items": [
            { "requestId": 1, "sourceCityValue": "karkh", "customerName": "أ" },
            { "requestId": 2, "sourceCityValue": "najaf", "customerName": "ب" },
            { "requestId": 3, "sourceCityValue": "karkh", "customerName": "ج" }
          ]
        }
        """);

        var payload = SalesRequestEvaluationRouter.BuildEvaluatePayload(groups["karkh"]);
        var items = (payload["items"] as JsonArray)!;
        Assert.Equal(2, items.Count);
        Assert.All(items, n =>
            Assert.Equal("karkh", SalesRequestEvaluationRouter.ReadSourceCity(n!.AsObject())));
    }

    [Fact]
    public void CollectSummaries_Does_Not_Import_Other_Branch_Matches_For_Same_Key()
    {
        // Karkh home result must win alone — Najaf Legal must not pollute counts/overall.
        var karkh = JsonNode.Parse("""
        {
          "items": [{
            "key": "karkh:29",
            "requestId": 29,
            "sourceCityValue": "karkh",
            "phone": { "resultCount": 1, "worstScore": 5, "worstRatingLevel": "Good", "worstRatingLabel": "جيد" },
            "tripleName": { "resultCount": 0 },
            "fatherGrandfather": { "resultCount": 3, "worstScore": 5, "worstRatingLevel": "Good", "worstRatingLabel": "جيد" }
          }]
        }
        """)!;
        var najafNoise = JsonNode.Parse("""
        {
          "items": [{
            "key": "karkh:29",
            "requestId": 29,
            "sourceCityValue": "karkh",
            "phone": { "resultCount": 1, "worstScore": -10, "worstRatingLevel": "Legal", "worstRatingLabel": "قانونية" },
            "tripleName": { "resultCount": 10 },
            "fatherGrandfather": { "resultCount": 10, "worstScore": -10, "worstRatingLevel": "Legal", "worstRatingLabel": "قانونية" }
          }]
        }
        """)!;

        // Router must only ever collect the home-branch chunk; if a foreign chunk appears, home wins.
        var merged = SalesRequestEvaluationRouter.CollectHomeBranchSummaries(
        [
            ("karkh", karkh),
            ("najaf", najafNoise)
        ]);

        var item = (merged["items"] as JsonArray)![0]!.AsObject();
        Assert.Equal(1, item["phone"]!["resultCount"]!.GetValue<int>());
        Assert.Equal(3, item["fatherGrandfather"]!["resultCount"]!.GetValue<int>());
        Assert.Equal(5, item["overallScore"]!.GetValue<int>());
        Assert.Equal("جيد", item["overallRatingLabel"]!.GetValue<string>());
    }

    [Fact]
    public void Acl_Rejects_SourceCity_Not_In_Allowed_List()
    {
        Assert.False(SalesRequestCrossBranchEvaluationMerger.IsCityAllowed(
            "karkh", ["najaf", "basra"]));
        Assert.True(SalesRequestCrossBranchEvaluationMerger.IsCityAllowed(
            "karkh", ["najaf", "karkh"]));
    }

    [Fact]
    public void Hits_Stay_On_Source_City_Only()
    {
        var karkh = JsonNode.Parse("""
        { "requestId": 29, "category": "phone", "total": 1,
          "items": [{ "customerId": 7, "score": 5, "fullName": "Karkh" }] }
        """)!;

        var page = SalesRequestEvaluationRouter.HitsFromHomeBranch(
            "karkh", "الكرخ", karkh, page: 1, pageSize: 30);

        Assert.Equal(1, page["total"]!.GetValue<int>());
        Assert.Equal("karkh", (page["items"] as JsonArray)![0]!["cityValue"]!.GetValue<string>());
    }

    [Fact]
    public void Missing_SourceCity_On_Hits_Is_Invalid()
    {
        Assert.False(SalesRequestEvaluationRouter.TryReadHitsSourceCity("{}", out _));
        Assert.True(SalesRequestEvaluationRouter.TryReadHitsSourceCity(
            """{"sourceCityValue":"karkh","requestId":29}""", out var city));
        Assert.Equal("karkh", city);
    }
}
