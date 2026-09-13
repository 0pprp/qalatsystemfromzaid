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
        public void Merge_Sums_ResultCounts_Across_Branches()
        {
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

            // Totals from branch responses still sum; item identity is deduped.
            Assert.Equal(2, page["total"]!.GetValue<int>());
            Assert.Single((page["items"] as JsonArray)!);
        }

        [Fact]
        public void Acl_City_Must_Be_In_Allowed_List()
        {
            Assert.True(SalesRequestCrossBranchEvaluationMerger.IsCityAllowed("basra", ["najaf", "basra"]));
            Assert.False(SalesRequestCrossBranchEvaluationMerger.IsCityAllowed("karbala", ["najaf", "basra"]));
        }
    }
}
