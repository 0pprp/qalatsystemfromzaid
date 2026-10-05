using System.Text.Json;
using System.Text.Json.Nodes;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Branch profile/docs return sales-manager FileUrl paths that 404 on the MF JWT.
/// Rewrite to main-follower mission-scoped download URLs the app can call.
/// </summary>
internal static class MainFollowerProfileUrlRewriter
{
    public static object? RewriteCustomerProfile(object? payload, int missionId, string cityValue)
    {
        var node = ToNode(payload);
        if (node is not JsonObject root) return payload;

        var city = Uri.EscapeDataString(cityValue ?? "");
        RewriteDocArray(root["customerDocuments"] as JsonArray
                        ?? root["CustomerDocuments"] as JsonArray,
            id => $"/api/main-follower/missions/{missionId}/cities/{city}/customer-documents/{id}/file");

        var sales = root["sales"] as JsonArray ?? root["Sales"] as JsonArray;
        if (sales != null)
        {
            foreach (var sale in sales.OfType<JsonObject>())
            {
                var saleId = sale["saleId"]?.GetValue<int?>()
                             ?? sale["SaleId"]?.GetValue<int?>()
                             ?? 0;
                if (saleId <= 0) continue;
                RewriteDocArray(sale["documents"] as JsonArray ?? sale["Documents"] as JsonArray,
                    id => $"/api/main-follower/missions/{missionId}/cities/{city}/sales/{saleId}/documents/{id}/download");
            }
        }

        return JsonSerializer.Deserialize<object>(root.ToJsonString()) ?? payload;
    }

    public static object? RewriteSaleDocuments(object? payload, int missionId, string cityValue, int saleId)
    {
        var node = ToNode(payload);
        var city = Uri.EscapeDataString(cityValue ?? "");
        if (node is JsonArray arr)
        {
            RewriteDocArray(arr,
                id => $"/api/main-follower/missions/{missionId}/cities/{city}/sales/{saleId}/documents/{id}/download");
            return JsonSerializer.Deserialize<object>(arr.ToJsonString()) ?? payload;
        }

        if (node is JsonObject obj)
        {
            RewriteDocArray(obj["documents"] as JsonArray ?? obj["Documents"] as JsonArray,
                id => $"/api/main-follower/missions/{missionId}/cities/{city}/sales/{saleId}/documents/{id}/download");
            return JsonSerializer.Deserialize<object>(obj.ToJsonString()) ?? payload;
        }

        return payload;
    }

    private static void RewriteDocArray(JsonArray? docs, Func<int, string> urlForId)
    {
        if (docs == null) return;
        foreach (var item in docs.OfType<JsonObject>())
        {
            var id = item["id"]?.GetValue<int?>()
                     ?? item["Id"]?.GetValue<int?>()
                     ?? item["documentId"]?.GetValue<int?>()
                     ?? item["DocumentId"]?.GetValue<int?>();
            if (id is null or <= 0) continue;
            var url = urlForId(id.Value);
            item["fileUrl"] = url;
            item["FileUrl"] = url;
            item["downloadUrl"] = url;
            item["DownloadUrl"] = url;
        }
    }

    private static JsonNode? ToNode(object? payload)
    {
        if (payload == null) return null;
        if (payload is JsonElement el) return JsonNode.Parse(el.GetRawText());
        if (payload is JsonNode n) return n;
        try
        {
            return JsonNode.Parse(JsonSerializer.Serialize(payload));
        }
        catch
        {
            return null;
        }
    }
}
