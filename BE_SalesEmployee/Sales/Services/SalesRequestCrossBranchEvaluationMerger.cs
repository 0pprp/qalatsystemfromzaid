using System.Text.Json.Nodes;

namespace BE_SalesEmployee.Sales.Services
{
    /// <summary>
    /// Server-side merge of per-branch evaluation summaries/hits.
    /// Identity dedupe for hits: CityValue + CustomerId.
    /// Worst rating (lowest score) wins across all allowed branches.
    /// </summary>
    public static class SalesRequestCrossBranchEvaluationMerger
    {
        public static JsonObject MergeSummaries(IEnumerable<JsonNode?> branchBodies)
        {
            var byKey = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var body in branchBodies)
            {
                if (body is not JsonObject root)
                {
                    continue;
                }

                var items = root["items"] as JsonArray ?? root["Items"] as JsonArray;
                if (items is null)
                {
                    continue;
                }

                foreach (var node in items)
                {
                    if (node is not JsonObject item)
                    {
                        continue;
                    }

                    var key = ReadString(item, "key", "Key");
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        var requestId = ReadInt(item, "requestId", "RequestId");
                        var source = ReadString(item, "sourceCityValue", "SourceCityValue");
                        key = $"{source}:{requestId}";
                        item["key"] = key;
                    }

                    if (!byKey.TryGetValue(key, out var existing))
                    {
                        byKey[key] = item.DeepClone() as JsonObject ?? item;
                        ApplyOverall(byKey[key]);
                        continue;
                    }

                    MergeCategory(existing, item, "tripleName", "TripleName");
                    MergeCategory(existing, item, "phone", "Phone");
                    MergeCategory(existing, item, "fatherGrandfather", "FatherGrandfather");
                    ApplyOverall(existing);
                }
            }

            var outItems = new JsonArray();
            foreach (var item in byKey.Values)
            {
                ApplyOverall(item);
                outItems.Add(item);
            }

            return new JsonObject { ["items"] = outItems };
        }

        public static JsonObject MergeHitsPages(
            IEnumerable<(string CityValue, string CityName, JsonNode? Body)> branches,
            int page,
            int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? 30 : pageSize, 1, 100);
            var merged = new List<JsonObject>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var requestId = 0;
            var category = "";

            foreach (var (cityValue, cityName, body) in branches)
            {
                if (body is not JsonObject root)
                {
                    continue;
                }

                requestId = requestId == 0 ? ReadInt(root, "requestId", "RequestId") : requestId;
                if (string.IsNullOrWhiteSpace(category))
                {
                    category = ReadString(root, "category", "Category");
                }

                var items = root["items"] as JsonArray ?? root["Items"] as JsonArray;
                if (items is null)
                {
                    continue;
                }

                foreach (var node in items)
                {
                    if (node is not JsonObject item)
                    {
                        continue;
                    }

                    item["cityValue"] = cityValue;
                    item["cityName"] = cityName;
                    var customerId = ReadInt(item, "customerId", "CustomerId");
                    var dedupe = $"{cityValue}:{customerId}";
                    if (!seen.Add(dedupe))
                    {
                        continue;
                    }

                    merged.Add(item.DeepClone() as JsonObject ?? item);
                }
            }

            merged.Sort((a, b) => ReadInt(a, "score", "Score").CompareTo(ReadInt(b, "score", "Score")));
            var pageItems = new JsonArray();
            foreach (var item in merged.Skip((page - 1) * pageSize).Take(pageSize))
            {
                pageItems.Add(item.DeepClone());
            }

            return new JsonObject
            {
                ["requestId"] = requestId,
                ["category"] = category,
                ["total"] = seen.Count,
                ["page"] = page,
                ["pageSize"] = pageSize,
                ["items"] = pageItems
            };
        }

        /// <summary>True when <paramref name="cityValue"/> is among allowed sales branches.</summary>
        public static bool IsCityAllowed(string? cityValue, IEnumerable<string> allowedCityValues)
        {
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                return false;
            }

            return allowedCityValues.Any(c =>
                string.Equals(c, cityValue.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static void MergeCategory(JsonObject dest, JsonObject src, string camel, string pascal)
        {
            var destCat = dest[camel] as JsonObject ?? dest[pascal] as JsonObject ?? new JsonObject();
            var srcCat = src[camel] as JsonObject ?? src[pascal] as JsonObject;
            if (srcCat is null)
            {
                dest[camel] = destCat;
                return;
            }

            var destCount = ReadInt(destCat, "resultCount", "ResultCount");
            var srcCount = ReadInt(srcCat, "resultCount", "ResultCount");
            destCat["resultCount"] = destCount + srcCount;

            var destScore = ReadNullableInt(destCat, "worstScore", "WorstScore");
            var srcScore = ReadNullableInt(srcCat, "worstScore", "WorstScore");
            if (srcCount > 0 && (destScore is null || (srcScore is not null && srcScore < destScore)))
            {
                destCat["worstScore"] = CloneOrNull(srcCat["worstScore"] ?? srcCat["WorstScore"]);
                destCat["worstRatingLevel"] = CloneOrNull(srcCat["worstRatingLevel"] ?? srcCat["WorstRatingLevel"]);
                destCat["worstRatingLabel"] = CloneOrNull(srcCat["worstRatingLabel"] ?? srcCat["WorstRatingLabel"]);
            }
            else if (destCount + srcCount == 0)
            {
                destCat["worstRatingLabel"] = "لا توجد نتائج";
                destCat["worstScore"] = null;
                destCat["worstRatingLevel"] = null;
            }

            dest[camel] = destCat;
        }

        private static JsonNode? CloneOrNull(JsonNode? node) =>
            node?.DeepClone();

        public static void ApplyOverall(JsonObject item)
        {
            int? worstScore = null;
            string? worstLevel = null;
            string? worstLabel = null;
            Consider(item, "tripleName", "TripleName", ref worstScore, ref worstLevel, ref worstLabel);
            Consider(item, "phone", "Phone", ref worstScore, ref worstLevel, ref worstLabel);
            Consider(item, "fatherGrandfather", "FatherGrandfather", ref worstScore, ref worstLevel, ref worstLabel);

            item["hasAnyMatch"] = worstScore is not null;
            item["overallScore"] = worstScore;
            item["overallRatingLevel"] = worstLevel;
            item["overallRatingLabel"] = worstScore is null ? "لا يوجد تطابق" : (worstLabel ?? LabelFromScore(worstScore.Value));
        }

        private static void Consider(
            JsonObject item,
            string camel,
            string pascal,
            ref int? worstScore,
            ref string? worstLevel,
            ref string? worstLabel)
        {
            var cat = item[camel] as JsonObject ?? item[pascal] as JsonObject;
            if (cat is null || ReadInt(cat, "resultCount", "ResultCount") <= 0)
            {
                return;
            }

            var score = ReadNullableInt(cat, "worstScore", "WorstScore");
            if (score is null)
            {
                return;
            }

            if (worstScore is null || score < worstScore)
            {
                worstScore = score;
                worstLevel = ReadString(cat, "worstRatingLevel", "WorstRatingLevel");
                worstLabel = ReadString(cat, "worstRatingLabel", "WorstRatingLabel");
            }
        }

        private static string LabelFromScore(int score) => score switch
        {
            <= -10 => "قانونية",
            <= -5 => "مرفوض",
            <= 0 => "ضعيف",
            <= 5 => "جيد",
            _ => "ممتاز"
        };

        private static string ReadString(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetPropertyValue(name, out var node) && node is not null)
                {
                    return node.ToString() ?? "";
                }
            }

            return "";
        }

        private static int ReadInt(JsonObject obj, params string[] names) =>
            ReadNullableInt(obj, names) ?? 0;

        private static int? ReadNullableInt(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (!obj.TryGetPropertyValue(name, out var node) || node is null)
                {
                    continue;
                }

                if (node is JsonValue value)
                {
                    if (value.TryGetValue<int>(out var i))
                    {
                        return i;
                    }

                    if (int.TryParse(value.ToString(), out var parsed))
                    {
                        return parsed;
                    }
                }
            }

            return null;
        }
    }
}
