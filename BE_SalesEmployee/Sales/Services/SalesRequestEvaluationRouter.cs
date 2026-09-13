using System.Text.Json.Nodes;

namespace BE_SalesEmployee.Sales.Services;

/// <summary>
/// Routes SalesRequest evaluation to the request's home province only.
/// ACL decides whether the manager may call that branch; it does not expand search scope.
/// </summary>
public static class SalesRequestEvaluationRouter
{
    public static IReadOnlyDictionary<string, List<JsonObject>> GroupItemsBySourceCity(string? jsonBody)
    {
        var groups = new Dictionary<string, List<JsonObject>>(StringComparer.OrdinalIgnoreCase);
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody);
        }
        catch
        {
            return groups;
        }

        if (root is not JsonObject obj)
        {
            return groups;
        }

        var items = obj["items"] as JsonArray ?? obj["Items"] as JsonArray;
        if (items is null)
        {
            return groups;
        }

        foreach (var node in items)
        {
            if (node is not JsonObject item)
            {
                continue;
            }

            var city = ReadSourceCity(item);
            if (string.IsNullOrWhiteSpace(city))
            {
                continue;
            }

            if (!groups.TryGetValue(city, out var list))
            {
                list = [];
                groups[city] = list;
            }

            list.Add(item.DeepClone() as JsonObject ?? item);
        }

        return groups;
    }

    public static JsonObject BuildEvaluatePayload(IEnumerable<JsonObject> items)
    {
        var arr = new JsonArray();
        foreach (var item in items)
        {
            arr.Add(item.DeepClone());
        }

        return new JsonObject { ["items"] = arr };
    }

    /// <summary>
    /// Collects per-branch evaluate responses. Each chunk is trusted only for keys
    /// whose sourceCityValue matches the branch that produced it (home province).
    /// Foreign noise for the same key is ignored.
    /// </summary>
    public static JsonObject CollectHomeBranchSummaries(
        IEnumerable<(string BranchCityValue, JsonNode? Body)> chunks)
    {
        var byKey = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var (branchCity, body) in chunks)
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

                var source = ReadSourceCity(item);
                if (string.IsNullOrWhiteSpace(source))
                {
                    source = branchCity;
                    item["sourceCityValue"] = source;
                }

                // Only accept results from the request's home branch.
                if (!string.Equals(source, branchCity, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var key = ReadString(item, "key", "Key");
                if (string.IsNullOrWhiteSpace(key))
                {
                    key = $"{source}:{ReadRequestId(item)}";
                    item["key"] = key;
                }

                if (byKey.ContainsKey(key))
                {
                    continue;
                }

                var clone = item.DeepClone() as JsonObject ?? item;
                EnsureOverall(clone);
                byKey[key] = clone;
            }
        }

        var outItems = new JsonArray();
        foreach (var item in byKey.Values)
        {
            EnsureOverall(item);
            outItems.Add(item);
        }

        return new JsonObject { ["items"] = outItems };
    }

    public static JsonObject HitsFromHomeBranch(
        string cityValue,
        string cityName,
        JsonNode? body,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize <= 0 ? 30 : pageSize, 1, 100);
        var items = new List<JsonObject>();
        var requestId = 0;
        var category = "";
        var totalFromBranch = 0;

        if (body is JsonObject root)
        {
            requestId = ReadInt(root, "requestId", "RequestId");
            category = ReadString(root, "category", "Category");
            totalFromBranch = ReadInt(root, "total", "Total");
            var arr = root["items"] as JsonArray ?? root["Items"] as JsonArray;
            if (arr is not null)
            {
                foreach (var node in arr)
                {
                    if (node is not JsonObject item)
                    {
                        continue;
                    }

                    var clone = item.DeepClone() as JsonObject ?? item;
                    clone["cityValue"] = cityValue;
                    clone["cityName"] = cityName;
                    items.Add(clone);
                }
            }
        }

        items.Sort((a, b) => ReadInt(a, "score", "Score").CompareTo(ReadInt(b, "score", "Score")));
        var pageItems = new JsonArray();
        foreach (var item in items)
        {
            pageItems.Add(item);
        }

        return new JsonObject
        {
            ["requestId"] = requestId,
            ["category"] = category,
            ["total"] = totalFromBranch > 0 ? totalFromBranch : items.Count,
            ["page"] = page,
            ["pageSize"] = pageSize,
            ["items"] = pageItems
        };
    }

    public static bool TryReadHitsSourceCity(string? jsonBody, out string cityValue)
    {
        cityValue = "";
        try
        {
            var root = JsonNode.Parse(string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody) as JsonObject;
            cityValue = ReadString(root, "sourceCityValue", "SourceCityValue").Trim();
            return cityValue.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static string ReadSourceCity(JsonObject item) =>
        ReadString(item, "sourceCityValue", "SourceCityValue").Trim();

    public static int ReadRequestId(JsonObject item) =>
        ReadInt(item, "requestId", "RequestId");

    private static void EnsureOverall(JsonObject item)
    {
        // Prefer branch-provided overall; fill only when missing.
        if (item.ContainsKey("overallScore") || item.ContainsKey("OverallScore"))
        {
            return;
        }

        SalesRequestCrossBranchEvaluationMerger.ApplyOverall(item);
    }

    private static string ReadString(JsonObject? obj, params string[] names)
    {
        if (obj is null)
        {
            return "";
        }

        foreach (var name in names)
        {
            if (obj.TryGetPropertyValue(name, out var node) && node is not null)
            {
                return node.ToString() ?? "";
            }
        }

        return "";
    }

    private static int ReadInt(JsonObject obj, params string[] names)
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

        return 0;
    }
}
