using System.Text.Json;
using System.Text.Json.Nodes;
using BE_SalesEmployee.Sales.Services;
using Xunit;

namespace BE_SalesEmployee.Tests
{
    public sealed class SalesRequestCrossBranchEvaluationMergerTests
    {
        [Fact]
        public void Merge_Takes_Worst_Rating_From_Other_Branch()
        {
            var najaf = JsonNode.Parse("""
            {
              "items": [{
                "requestId": 10,
                "sourceCityValue": "najaf",
                "key": "najaf:10",
                "tripleName": { "resultCount": 0, "worstRatingLabel": "لا توجد نتائج" },
                "phone": { "resultCount": 0, "worstRatingLabel": "لا توجد نتائج" },
                "fatherGrandfather": { "resultCount": 0, "worstRatingLabel": "لا توجد نتائج" }
              }]
            }
            """)!;

            var basra = JsonNode.Parse("""
            {
              "items": [{
                "requestId": 10,
                "sourceCityValue": "najaf",
                "key": "najaf:10",
                "tripleName": { "resultCount": 0, "worstRatingLabel": "لا توجد نتائج" },
                "phone": { "resultCount": 1, "worstScore": -10, "worstRatingLevel": "Legal", "worstRatingLabel": "قانونية" },
                "fatherGrandfather": { "resultCount": 0, "worstRatingLabel": "لا توجد نتائج" }
              }]
            }
            """)!;

            var merged = SalesRequestCrossBranchEvaluationMerger.MergeSummaries([najaf, basra]);
            var item = (merged["items"] as JsonArray)![0]!.AsObject();
            Assert.Equal(-10, item["overallScore"]!.GetValue<int>());
            Assert.Equal("قانونية", item["overallRatingLabel"]!.GetValue<string>());
            Assert.Equal(1, item["phone"]!["resultCount"]!.GetValue<int>());
        }

        [Fact]
        public void Merge_Legacy_Helper_Still_Sums_When_Called_Directly()
        {
            // MergeSummaries remains for tooling; evaluate path uses CollectHomeBranchSummaries instead.
            var a = JsonNode.Parse("""
            {
              "items": [{
                "key": "najaf:1",
                "requestId": 1,
                "sourceCityValue": "najaf",
                "phone": { "resultCount": 2, "worstScore": 10, "worstRatingLevel": "Excellent", "worstRatingLabel": "ممتاز" },
                "tripleName": { "resultCount": 0 },
                "fatherGrandfather": { "resultCount": 0 }
              }]
            }
            """)!;
            var b = JsonNode.Parse("""
            {
              "items": [{
                "key": "najaf:1",
                "requestId": 1,
                "sourceCityValue": "najaf",
                "phone": { "resultCount": 1, "worstScore": 5, "worstRatingLevel": "Good", "worstRatingLabel": "جيد" },
                "tripleName": { "resultCount": 0 },
                "fatherGrandfather": { "resultCount": 0 }
              }]
            }
            """)!;

            var merged = SalesRequestCrossBranchEvaluationMerger.MergeSummaries([a, b]);
            var phone = (merged["items"] as JsonArray)![0]!["phone"]!.AsObject();
            Assert.Equal(3, phone["resultCount"]!.GetValue<int>());
            Assert.Equal(5, phone["worstScore"]!.GetValue<int>());
        }

        [Fact]
        public void Hits_Dedupe_By_Branch_And_CustomerId()
        {
            var najaf = JsonNode.Parse("""
            { "requestId": 1, "category": "phone", "total": 1,
              "items": [{ "customerId": 5, "score": 10, "fullName": "A" }] }
            """)!;
            var basra = JsonNode.Parse("""
            { "requestId": 1, "category": "phone", "total": 1,
              "items": [{ "customerId": 5, "score": -10, "fullName": "A-legal" }] }
            """)!;

            var page = SalesRequestCrossBranchEvaluationMerger.MergeHitsPages(
            [
                ("najaf", "النجف", najaf),
                ("basra", "البصرة", basra)
            ], page: 1, pageSize: 30);

            Assert.Equal(2, page["total"]!.GetValue<int>());
            var items = (page["items"] as JsonArray)!;
            Assert.Equal(2, items.Count);
            Assert.Equal(-10, items[0]!["score"]!.GetValue<int>());
            Assert.Equal("basra", items[0]!["cityValue"]!.GetValue<string>());
        }

        [Fact]
        public void Hits_Dedupe_Same_Branch_Customer_Twice()
        {
            var basra = JsonNode.Parse("""
            { "requestId": 1, "category": "phone", "total": 1,
              "items": [{ "customerId": 5, "score": -10, "fullName": "A-legal" }] }
            """)!;

            var page = SalesRequestCrossBranchEvaluationMerger.MergeHitsPages(
            [
                ("basra", "البصرة", basra),
                ("basra", "البصرة", basra)
            ], page: 1, pageSize: 30);

            // Totals reflect unique city:customerId profiles after dedupe.
            Assert.Equal(1, page["total"]!.GetValue<int>());
            Assert.Single((page["items"] as JsonArray)!);
        }

        [Fact]
        public void Merge_Keeps_Separate_Keys_For_Same_RequestId_Different_SourceCities()
        {
            var najafReq = JsonNode.Parse("""
            {
              "items": [{
                "key": "1:5",
                "requestId": 5,
                "sourceCityValue": "1",
                "phone": { "resultCount": 1, "worstScore": 10, "worstRatingLevel": "Excellent", "worstRatingLabel": "ممتاز" },
                "tripleName": { "resultCount": 0 },
                "fatherGrandfather": { "resultCount": 0 }
              }]
            }
            """)!;
            var basraReq = JsonNode.Parse("""
            {
              "items": [{
                "key": "2:5",
                "requestId": 5,
                "sourceCityValue": "2",
                "phone": { "resultCount": 2, "worstScore": -10, "worstRatingLevel": "Legal", "worstRatingLabel": "قانونية" },
                "tripleName": { "resultCount": 0 },
                "fatherGrandfather": { "resultCount": 0 }
              }]
            }
            """)!;

            var merged = SalesRequestCrossBranchEvaluationMerger.MergeSummaries([najafReq, basraReq]);
            var items = (merged["items"] as JsonArray)!;
            Assert.Equal(2, items.Count);
            var byKey = items.Cast<JsonNode>().Select(n => n!.AsObject())
                .ToDictionary(o => o["key"]!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);
            Assert.Equal(1, byKey["1:5"]["phone"]!["resultCount"]!.GetValue<int>());
            Assert.Equal(2, byKey["2:5"]["phone"]!["resultCount"]!.GetValue<int>());
        }

        [Fact]
        public void Evaluate_Path_Home_Branch_Only_Ignores_Foreign_Chunk()
        {
            var karkh = JsonNode.Parse("""
            {
              "items": [{
                "key": "karkh:29",
                "requestId": 29,
                "sourceCityValue": "karkh",
                "tripleName": { "resultCount": 0 },
                "phone": { "resultCount": 1, "worstScore": 5, "worstRatingLevel": "Good", "worstRatingLabel": "جيد" },
                "fatherGrandfather": { "resultCount": 2, "worstScore": 5, "worstRatingLevel": "Good", "worstRatingLabel": "جيد" }
              }]
            }
            """)!;
            var najafNoise = JsonNode.Parse("""
            {
              "items": [{
                "key": "karkh:29",
                "requestId": 29,
                "sourceCityValue": "karkh",
                "tripleName": { "resultCount": 10 },
                "phone": { "resultCount": 1, "worstScore": -10, "worstRatingLevel": "Legal", "worstRatingLabel": "قانونية" },
                "fatherGrandfather": { "resultCount": 7, "worstScore": -10, "worstRatingLevel": "Legal", "worstRatingLabel": "قانونية" }
              }]
            }
            """)!;

            var merged = SalesRequestEvaluationRouter.CollectHomeBranchSummaries(
            [
                ("karkh", karkh),
                ("najaf", najafNoise)
            ]);
            var item = (merged["items"] as JsonArray)![0]!.AsObject();
            Assert.Equal(1, item["phone"]!["resultCount"]!.GetValue<int>());
            Assert.Equal(2, item["fatherGrandfather"]!["resultCount"]!.GetValue<int>());
            Assert.Equal(5, item["overallScore"]!.GetValue<int>());
            Assert.Equal("جيد", item["overallRatingLabel"]!.GetValue<string>());
        }

        [Fact]
        public void Merge_CrossBranch_Sums_Triple_Phone_Kinship_For_Same_Key_Legacy_Only()
        {
            // Legacy MergeSummaries sums — not used by evaluate after home-province routing.
            var najaf = JsonNode.Parse("""
            {
              "items": [{
                "key": "najaf:48",
                "requestId": 48,
                "sourceCityValue": "najaf",
                "tripleName": { "resultCount": 1, "worstScore": 10, "worstRatingLevel": "Excellent", "worstRatingLabel": "ممتاز" },
                "phone": { "resultCount": 0, "worstRatingLabel": "لا توجد نتائج" },
                "fatherGrandfather": { "resultCount": 1, "worstScore": 5, "worstRatingLevel": "Good", "worstRatingLabel": "جيد" }
              }]
            }
            """)!;
            var basra = JsonNode.Parse("""
            {
              "items": [{
                "key": "najaf:48",
                "requestId": 48,
                "sourceCityValue": "najaf",
                "tripleName": { "resultCount": 2, "worstScore": 0, "worstRatingLevel": "Weak", "worstRatingLabel": "ضعيف" },
                "phone": { "resultCount": 2, "worstScore": -10, "worstRatingLevel": "Legal", "worstRatingLabel": "قانونية" },
                "fatherGrandfather": { "resultCount": 2, "worstScore": -5, "worstRatingLevel": "Rejected", "worstRatingLabel": "مرفوض" }
              }]
            }
            """)!;
            var baghdad = JsonNode.Parse("""
            {
              "items": [{
                "key": "najaf:48",
                "requestId": 48,
                "sourceCityValue": "najaf",
                "tripleName": { "resultCount": 1, "worstScore": 5, "worstRatingLevel": "Good", "worstRatingLabel": "جيد" },
                "phone": { "resultCount": 0 },
                "fatherGrandfather": { "resultCount": 1, "worstScore": 10, "worstRatingLevel": "Excellent", "worstRatingLabel": "ممتاز" }
              }]
            }
            """)!;

            var merged = SalesRequestCrossBranchEvaluationMerger.MergeSummaries([najaf, basra, baghdad]);
            var item = (merged["items"] as JsonArray)![0]!.AsObject();
            Assert.Equal(4, item["tripleName"]!["resultCount"]!.GetValue<int>());
            Assert.Equal(2, item["phone"]!["resultCount"]!.GetValue<int>());
            Assert.Equal(4, item["fatherGrandfather"]!["resultCount"]!.GetValue<int>());
            Assert.Equal(-10, item["overallScore"]!.GetValue<int>());
            Assert.Equal("قانونية", item["overallRatingLabel"]!.GetValue<string>());
        }

        [Fact]
        public void Merge_JsonObject_RoundTrip_Preserves_ResultCount_For_Fe()
        {
            var body = JsonNode.Parse("""
            {
              "items": [{
                "key": "1:10",
                "requestId": 10,
                "sourceCityValue": "1",
                "tripleName": { "resultCount": 3, "worstScore": 10, "worstRatingLevel": "Excellent", "worstRatingLabel": "ممتاز" },
                "phone": { "resultCount": 2, "worstScore": -10, "worstRatingLevel": "Legal", "worstRatingLabel": "قانونية" },
                "fatherGrandfather": { "resultCount": 4, "worstScore": 5, "worstRatingLevel": "Good", "worstRatingLabel": "جيد" }
              }]
            }
            """)!;
            var merged = SalesRequestCrossBranchEvaluationMerger.MergeSummaries([body]);
            // Simulate ASP.NET StatusCode(JsonObject) → HTTP → axios parse
            var wire = System.Text.Json.JsonSerializer.Serialize(merged);
            using var doc = System.Text.Json.JsonDocument.Parse(wire);
            Assert.True(doc.RootElement.TryGetProperty("items", out var items));
            Assert.Equal(JsonValueKind.Array, items.ValueKind);
            var first = items[0];
            Assert.Equal(3, first.GetProperty("tripleName").GetProperty("resultCount").GetInt32());
            Assert.Equal(2, first.GetProperty("phone").GetProperty("resultCount").GetInt32());
            Assert.Equal(4, first.GetProperty("fatherGrandfather").GetProperty("resultCount").GetInt32());
            Assert.Equal("1:10", first.GetProperty("key").GetString());
        }

        [Fact]
        public void Hits_Total_Matches_Unique_City_Customer_Profiles()
        {
            var najaf = JsonNode.Parse("""
            { "requestId": 1, "category": "tripleName", "total": 1,
              "items": [{ "customerId": 10, "score": 10 }] }
            """)!;
            var basra = JsonNode.Parse("""
            { "requestId": 1, "category": "tripleName", "total": 2,
              "items": [
                { "customerId": 10, "score": 5 },
                { "customerId": 11, "score": 0 }
              ] }
            """)!;
            var page = SalesRequestCrossBranchEvaluationMerger.MergeHitsPages(
            [
                ("najaf", "النجف", najaf),
                ("basra", "البصرة", basra)
            ], 1, 30);
            // CustomerId 10 in najaf and basra are different profiles
            Assert.Equal(3, page["total"]!.GetValue<int>());
            Assert.Equal(3, (page["items"] as JsonArray)!.Count);
        }
    }
}
