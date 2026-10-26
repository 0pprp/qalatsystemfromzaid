using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.Services
{
    public interface ISalesManagerBranchAggregator
    {
        Task<IReadOnlyList<AdminCity>> BranchesAsync(CancellationToken ct);
        Task<(int Status, object? Body)> GetAsync(GatewayUser user, string? cityValue, string companyPath, CancellationToken ct);
        Task<(int Status, object? Body)> GetExactBranchArrayAsync(GatewayUser user, string cityValue, string companyPath, CancellationToken ct);
        Task<(int Status, object? Body)> GetOneAsync(GatewayUser user, string cityValue, string companyPath, CancellationToken ct);
        /// <summary>Fan-out object endpoints (e.g. branch-month) — wait all provinces, stamp cityValue.</summary>
        Task<(int Status, object? Body)> GetObjectFanoutAsync(GatewayUser user, string companyPath, CancellationToken ct);
        /// <summary>Paged sales-requests across all provinces (soft ~2s first paint).</summary>
        Task<(int Status, object? Body)> GetSalesRequestsPageFanoutAsync(
            GatewayUser user,
            string companyPath,
            int page,
            int pageSize,
            CancellationToken ct);
        Task<(int Status, object? Body)> SumCountAsync(GatewayUser user, string? cityValue, string companyPath, CancellationToken ct);
        Task<(int Status, object? Body)> PostFanoutAsync(GatewayUser user, string? cityValue, string companyPath, string jsonBody, CancellationToken ct);
        Task<(int Status, object? Body)> GetFileAsync(GatewayUser user, string cityValue, string companyPath, CancellationToken ct);
        Task<(int Status, object? Body)> PostAsync(GatewayUser user, string cityValue, string companyPath, string jsonBody, CancellationToken ct);
        Task<(int Status, object? Body)> SendContentAsync(GatewayUser user, string cityValue, string companyPath, HttpMethod method, HttpContent? content, CancellationToken ct, string? actorRole = null);
        Task<(int Status, object? Body)> SearchCustomersAsync(GatewayUser user, string? query, string? cityValue, CancellationToken ct);
        Task<(int Status, object? Body)> ExcelSearchAsync(GatewayUser user, string? cityValue, string jsonBody, CancellationToken ct);
        Task<(int Status, object? Body)> DashboardAsync(GatewayUser user, string? cityValue, CancellationToken ct);
        Task<(int Status, object? Body)> EvaluateAcrossBranchesAsync(GatewayUser user, string jsonBody, CancellationToken ct);
        Task<(int Status, object? Body)> EvaluationHitsAcrossBranchesAsync(GatewayUser user, string jsonBody, CancellationToken ct);
        Task<(int Status, object? Body)> TransferProvinceAsync(
            GatewayUser user,
            string fromCityValue,
            int requestId,
            string jsonBody,
            CancellationToken ct);
    }

    /// <summary>
    /// Fans out Sales Manager reads to GetAdmin sales branches using the
    /// internal gateway key. Never reuses a city employee JWT.
    /// </summary>
    public sealed class SalesManagerBranchAggregator : ISalesManagerBranchAggregator
    {
        public static readonly TimeSpan BranchTimeout = TimeSpan.FromSeconds(8);

        private readonly AdminCitiesService _cities;
        private readonly BranchProxyService _proxy;

        public SalesManagerBranchAggregator(AdminCitiesService cities, BranchProxyService proxy)
        {
            _cities = cities;
            _proxy = proxy;
        }

        public Task<IReadOnlyList<AdminCity>> BranchesAsync(CancellationToken ct) =>
            GetTargetsAsync(null, ct);

        public async Task<(int Status, object? Body)> GetAsync(
            GatewayUser user,
            string? cityValue,
            string companyPath,
            CancellationToken ct)
        {
            var targets = await GetTargetsAsync(cityValue, ct);
            if (targets.Count == 0)
            {
                return (200, Array.Empty<object>());
            }

            // Single branch: wait fully (normal BranchTimeout).
            if (targets.Count == 1)
            {
                var only = await FetchArrayAsync(user, targets[0], companyPath, BranchTimeout, ct);
                return (200, only);
            }

            // Multi-branch (الكل):
            // Cheap lists: soft first-paint (~1s).
            // Employees/followers: MUST wait for every province — soft-cut after the first
            // city with data previously dropped slow branches (incomplete follower list).
            var longList = NeedsLongBranchTimeout(companyPath);
            var perBranch = longList ? FanoutSoftDeadline.LongListBranchTimeout : FanoutSoftDeadline.MultiBranchTimeout;
            var tasks = targets
                .Select(city => FetchArrayAsync(user, city, companyPath, perBranch, ct))
                .ToList();
            if (longList)
            {
                var mergedLong = await FanoutSoftDeadline.CollectAsync(
                    tasks,
                    softWait: TimeSpan.Zero,
                    extendIfEmpty: TimeSpan.Zero,
                    hasData: static _ => false,
                    flatten: chunk => chunk,
                    ct);
                return (200, mergedLong);
            }

            var merged = await FanoutSoftDeadline.CollectAsync(
                tasks,
                FanoutSoftDeadline.SoftWait,
                FanoutSoftDeadline.ExtendIfEmpty,
                hasData: chunk => chunk.Count > 0,
                flatten: chunk => chunk,
                ct);
            return (200, merged);
        }

        public async Task<(int Status, object? Body)> SumCountAsync(
            GatewayUser user,
            string? cityValue,
            string companyPath,
            CancellationToken ct)
        {
            var targets = await GetTargetsAsync(cityValue, ct);
            var total = 0;
            var failed = new System.Collections.Concurrent.ConcurrentBag<string>();
            var succeeded = 0;
            await Task.WhenAll(targets.Select(async city =>
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(BranchTimeout);
                    using var response = await _proxy.SendManagerAsync(
                        city.Link, companyPath, HttpMethod.Get, null, user.UserName, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        failed.Add(city.Value ?? city.Name ?? city.Link ?? "?");
                        return;
                    }

                    var raw = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
                    var n = 0;
                    if (doc.RootElement.TryGetProperty("count", out var countProp)
                        || doc.RootElement.TryGetProperty("Count", out countProp))
                    {
                        n = countProp.ValueKind == JsonValueKind.Number ? countProp.GetInt32() : 0;
                    }

                    Interlocked.Add(ref total, n);
                    Interlocked.Increment(ref succeeded);
                }
                catch
                {
                    failed.Add(city.Value ?? city.Name ?? city.Link ?? "?");
                }
            }));

            var complete = failed.IsEmpty && succeeded == targets.Count;
            return (200, BranchAggregateCountResult.From(total, succeeded, targets.Count, failed));
        }

        public async Task<(int Status, object? Body)> PostFanoutAsync(
            GatewayUser user,
            string? cityValue,
            string companyPath,
            string jsonBody,
            CancellationToken ct)
        {
            var targets = await GetTargetsAsync(cityValue, ct);
            if (targets.Count == 0)
            {
                return (400, new { message = "لا توجد محافظة للإرسال." });
            }

            var marked = 0;
            foreach (var city in targets)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(BranchTimeout);
                using var response = await _proxy.SendManagerAsync(
                    city.Link, companyPath, HttpMethod.Post, jsonBody, user.UserName, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("marked", out var markedProp)
                    || doc.RootElement.TryGetProperty("Marked", out markedProp))
                {
                    marked += markedProp.ValueKind == JsonValueKind.Number ? markedProp.GetInt32() : 0;
                }
            }

            return (200, new { marked });
        }

        public async Task<(int Status, object? Body)> GetOneAsync(
            GatewayUser user,
            string cityValue,
            string companyPath,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                return (400, new { message = "يجب تحديد المحافظة." });
            }

            var city = await FindAsync(cityValue, ct);
            if (city == null)
            {
                return (404, new { message = "المحافظة غير موجودة." });
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                // Long-running branch calls: exception review, multi-month charts, employee rating enrich/lifetime.
                var timeout = NeedsLongBranchTimeout(companyPath)
                    ? ExcelSearchTimeout
                    : BranchTimeout;
                cts.CancelAfter(timeout);
                using var response = await _proxy.SendManagerAsync(
                    city.Link, companyPath, HttpMethod.Get, null, user.UserName, cts.Token);
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    return ((int)response.StatusCode, string.IsNullOrWhiteSpace(raw) ? null : BranchProxyService.TryParseJson(raw));
                }

                return ((int)response.StatusCode, Stamp(raw, city));
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // CancelAfter fired — do not leak TaskCanceledException as opaque HTTP 500.
                return (504, new { message = "انتهت مهلة الاتصال بمحافظة الفرع. أعد المحاولة." });
            }
        }

        public async Task<(int Status, object? Body)> GetObjectFanoutAsync(
            GatewayUser user,
            string companyPath,
            CancellationToken ct)
        {
            var targets = await GetTargetsAsync(null, ct);
            if (targets.Count == 0)
            {
                return (200, Array.Empty<object>());
            }

            var timeout = NeedsLongBranchTimeout(companyPath)
                ? FanoutSoftDeadline.LongListBranchTimeout
                : FanoutSoftDeadline.MultiBranchTimeout;
            var tasks = targets
                .Select(city => FetchObjectStampedAsync(user, city, companyPath, timeout, ct))
                .ToList();
            var merged = await FanoutSoftDeadline.CollectAsync(
                tasks,
                softWait: TimeSpan.Zero,
                extendIfEmpty: TimeSpan.Zero,
                hasData: static o => o != null,
                flatten: o => o == null ? Array.Empty<JsonObject>() : new[] { o },
                ct);
            return (200, merged);
        }

        /// <summary>
        /// الكل: parallel paged fetch per province, soft-merge under ~2s.
        /// Each branch returns a small page; gateway merges/sorts and returns one page + summed tabCounts.
        /// </summary>
        public async Task<(int Status, object? Body)> GetSalesRequestsPageFanoutAsync(
            GatewayUser user,
            string companyPath,
            int page,
            int pageSize,
            CancellationToken ct)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize <= 0 ? 40 : pageSize, 1, 100);
            var targets = await GetTargetsAsync(null, ct);
            if (targets.Count == 0)
            {
                return (200, new
                {
                    items = Array.Empty<object>(),
                    total = 0,
                    page,
                    pageSize,
                    hasMore = false,
                    tabCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                });
            }

            // Smaller per-branch page keeps الكل under 2s across ~17 WAN hops.
            var perBranchSize = Math.Clamp(Math.Max(pageSize, 12), 12, 24);
            var path = AppendOrReplaceQuery(companyPath, ("page", page.ToString()), ("pageSize", perBranchSize.ToString()));
            var perBranchTimeout = FanoutSoftDeadline.MultiBranchTimeout;
            var tasks = targets
                .Select(city => FetchPagedObjectAsync(user, city, path, perBranchTimeout, ct))
                .ToList();

            var pages = await FanoutSoftDeadline.CollectAsync(
                tasks,
                FanoutSoftDeadline.SoftWait,
                FanoutSoftDeadline.ExtendIfEmpty,
                hasData: p => p != null,
                flatten: p => p == null ? Array.Empty<JsonObject>() : new[] { p },
                ct);

            var items = new List<JsonObject>();
            var total = 0;
            var hasMore = false;
            var tabCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pageObj in pages)
            {
                total += ReadIntProp(pageObj, "total", "Total");
                if (ReadBoolProp(pageObj, "hasMore", "HasMore"))
                {
                    hasMore = true;
                }

                MergeTabCounts(tabCounts, pageObj["tabCounts"] as JsonObject ?? pageObj["TabCounts"] as JsonObject);
                var arr = pageObj["items"] as JsonArray ?? pageObj["Items"] as JsonArray;
                if (arr == null)
                {
                    continue;
                }

                foreach (var node in arr.OfType<JsonObject>())
                {
                    items.Add(node);
                }
            }

            var ordered = items
                .OrderByDescending(ReadCreatedAt)
                .Take(pageSize)
                .Cast<JsonNode>()
                .ToList();
            if (items.Count > pageSize)
            {
                hasMore = true;
            }

            var root = new JsonObject
            {
                ["items"] = new JsonArray(ordered.Select(n => n.DeepClone()).ToArray()),
                ["total"] = total,
                ["page"] = page,
                ["pageSize"] = pageSize,
                ["hasMore"] = hasMore || (page * pageSize < total),
                ["tabCounts"] = ToTabCountsObject(tabCounts)
            };
            return (200, root);
        }

        private async Task<JsonObject?> FetchPagedObjectAsync(
            GatewayUser user,
            AdminCity city,
            string companyPath,
            TimeSpan timeout,
            CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
                using var response = await _proxy.SendManagerAsync(
                    city.Link, companyPath, HttpMethod.Get, null, user.UserName, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                var node = JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
                if (node is not JsonObject obj)
                {
                    return null;
                }

                StampItemsArray(obj["items"] as JsonArray ?? obj["Items"] as JsonArray, city);
                obj["cityValue"] = city.Value;
                obj["cityName"] = city.Name;
                return obj;
            }
            catch
            {
                return null;
            }
        }

        private async Task<JsonObject?> FetchObjectStampedAsync(
            GatewayUser user,
            AdminCity city,
            string companyPath,
            TimeSpan timeout,
            CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
                using var response = await _proxy.SendManagerAsync(
                    city.Link, companyPath, HttpMethod.Get, null, user.UserName, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                var stamped = Stamp(raw, city);
                return stamped as JsonObject;
            }
            catch
            {
                return null;
            }
        }

        private static string AppendOrReplaceQuery(string companyPath, params (string Name, string Value)[] parts)
        {
            var path = companyPath ?? "";
            var qIndex = path.IndexOf('?', StringComparison.Ordinal);
            var basePath = qIndex >= 0 ? path[..qIndex] : path;
            var query = qIndex >= 0 ? path[(qIndex + 1)..] : "";
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(query))
            {
                foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split('=', 2);
                    if (kv.Length == 2)
                    {
                        map[Uri.UnescapeDataString(kv[0])] = Uri.UnescapeDataString(kv[1]);
                    }
                }
            }

            foreach (var (name, value) in parts)
            {
                map[name] = value;
            }

            var qs = string.Join("&", map.Select(kv =>
                $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
            return string.IsNullOrEmpty(qs) ? basePath : $"{basePath}?{qs}";
        }

        private static int ReadIntProp(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetPropertyValue(name, out var n) && n != null)
                {
                    try
                    {
                        return n.GetValue<int>();
                    }
                    catch
                    {
                        if (int.TryParse(n.ToString(), out var parsed))
                        {
                            return parsed;
                        }
                    }
                }
            }

            return 0;
        }

        private static bool ReadBoolProp(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetPropertyValue(name, out var n) && n != null)
                {
                    try
                    {
                        return n.GetValue<bool>();
                    }
                    catch
                    {
                        if (bool.TryParse(n.ToString(), out var parsed))
                        {
                            return parsed;
                        }
                    }
                }
            }

            return false;
        }

        private static DateTime ReadCreatedAt(JsonObject obj)
        {
            foreach (var name in new[] { "createdAtUtc", "CreatedAtUtc", "deletedAtUtc", "DeletedAtUtc" })
            {
                if (!obj.TryGetPropertyValue(name, out var n) || n == null)
                {
                    continue;
                }

                if (DateTime.TryParse(n.ToString(), out var dt))
                {
                    return dt;
                }
            }

            return DateTime.MinValue;
        }

        private static void MergeTabCounts(Dictionary<string, int> into, JsonObject? from)
        {
            if (from == null)
            {
                return;
            }

            foreach (var (key, value) in from)
            {
                if (value == null)
                {
                    continue;
                }

                var n = 0;
                try
                {
                    n = value.GetValue<int>();
                }
                catch
                {
                    int.TryParse(value.ToString(), out n);
                }

                into[key] = into.TryGetValue(key, out var prev) ? prev + n : n;
            }
        }

        private static JsonObject ToTabCountsObject(Dictionary<string, int> counts)
        {
            var obj = new JsonObject();
            foreach (var (k, v) in counts)
            {
                obj[k] = v;
            }

            return obj;
        }

        public async Task<(int Status, object? Body)> GetFileAsync(
            GatewayUser user,
            string cityValue,
            string companyPath,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                return (400, new { message = "يجب تحديد المحافظة." });
            }

            var city = await FindAsync(cityValue, ct);
            if (city == null)
            {
                return (404, new { message = "المحافظة غير موجودة." });
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // KYC / shop images can be multi‑MB over branch WAN — 8s BranchTimeout causes empty UI.
            var timeout = NeedsLongFileTimeout(companyPath) ? ExcelSearchTimeout : BranchTimeout;
            cts.CancelAfter(timeout);
            using var response = await _proxy.SendManagerAsync(
                city.Link, companyPath, HttpMethod.Get, null, user.UserName, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    return ((int)response.StatusCode, new { message = "الملف غير موجود على السيرفر." });
                }

                return ((int)response.StatusCode, BranchProxyService.TryParseJson(raw) ?? (object)new { message = raw });
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            if (string.Equals(contentType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                // Infer from magic bytes so FE image checks stay reliable.
                if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
                    contentType = "image/jpeg";
                else if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50)
                    contentType = "image/png";
            }
            return (200, (bytes, contentType));
        }

        public async Task<(int Status, object? Body)> PostAsync(
            GatewayUser user,
            string cityValue,
            string companyPath,
            string jsonBody,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                return (400, new { message = "يجب تحديد المحافظة المستهدفة للطلب." });
            }

            var city = await FindAsync(cityValue, ct);
            if (city == null)
            {
                return (404, new { message = "المحافظة غير موجودة." });
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // exception-hold is a quick write but must not share the aggressive 8s list timeout
            // when the branch is under load — failed sync left Pending exceptions visible in غير مسند.
            var timeout = companyPath.Contains("exception-hold", StringComparison.OrdinalIgnoreCase)
                ? ExcelSearchTimeout
                : BranchTimeout;
            cts.CancelAfter(timeout);
            using var response = await _proxy.SendManagerAsync(
                city.Link, companyPath, HttpMethod.Post, jsonBody, user.UserName, cts.Token);
            var raw = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, string.IsNullOrWhiteSpace(raw) ? null : Stamp(raw, city) ?? BranchProxyService.TryParseJson(raw));
        }

        public async Task<(int Status, object? Body)> SendContentAsync(
            GatewayUser user,
            string cityValue,
            string companyPath,
            HttpMethod method,
            HttpContent? content,
            CancellationToken ct,
            string? actorRole = null)
        {
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                return (400, new { message = "يجب تحديد المحافظة." });
            }

            var city = await FindAsync(cityValue, ct);
            if (city == null)
            {
                return (404, new { message = "المحافظة غير موجودة." });
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            using var response = await _proxy.SendManagerContentAsync(
                city.Link, companyPath, method, content, user.UserName, cts.Token, actorRole);
            if (method == HttpMethod.Get && response.IsSuccessStatusCode
                && response.Content.Headers.ContentType?.MediaType is string media
                && media.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                return (200, (bytes, media));
            }

            var raw = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, string.IsNullOrWhiteSpace(raw) ? null : Stamp(raw, city) ?? BranchProxyService.TryParseJson(raw));
        }

        public async Task<(int Status, object? Body)> SearchCustomersAsync(
            GatewayUser user,
            string? query,
            string? cityValue,
            CancellationToken ct)
        {
            var q = NormalizeArabic(query);
            if (q.Length < 2)
            {
                return (400, new { message = "اكتب حرفين على الأقل للبحث" });
            }

            if (string.IsNullOrWhiteSpace(cityValue))
            {
                return (400, new { message = "حدد المحافظة للبحث." });
            }

            var targets = await GetTargetsAsync(cityValue, ct);
            if (targets.Count == 0)
            {
                return (400, new { message = "المحافظة غير موجودة." });
            }
            var chunks = await Task.WhenAll(targets.Select(city =>
                FetchArrayAsync(user, city, $"sales-manager/customers/search?q={Uri.EscapeDataString(q)}", ct)));
            var merged = new List<JsonNode>();
            foreach (var chunk in chunks)
            {
                foreach (var item in chunk)
                {
                    if (item is not JsonObject obj)
                    {
                        continue;
                    }

                    ShapeCustomer(obj);
                    if (!CustomerMatches(obj, q))
                    {
                        continue;
                    }

                    merged.Add(obj);
                }
            }

            return (200, merged);
        }

        public async Task<(int Status, object? Body)> ExcelSearchAsync(
            GatewayUser user,
            string? cityValue,
            string jsonBody,
            CancellationToken ct)
        {
            var targets = await GetTargetsAsync(cityValue, ct);
            if (targets.Count == 0)
            {
                return (400, new { message = string.IsNullOrWhiteSpace(cityValue) ? "لا توجد محافظات للبحث." : "المحافظة غير موجودة." });
            }

            var chunks = await Task.WhenAll(targets.Select(city => FetchExcelSearchAsync(user, city, jsonBody, ct)));
            JsonArray? mergedQueries = null;
            var ok = 0;
            foreach (var (city, node) in chunks)
            {
                if (node is not JsonObject root || root["queries"] is not JsonArray queries)
                {
                    continue;
                }

                ok++;
                StampExcelMatches(queries, city);
                if (mergedQueries == null)
                {
                    mergedQueries = queries.DeepClone() as JsonArray ?? [];
                    continue;
                }

                for (var i = 0; i < queries.Count && i < mergedQueries.Count; i++)
                {
                    if (mergedQueries[i] is not JsonObject dest || queries[i] is not JsonObject src)
                    {
                        continue;
                    }

                    var destMatches = dest["matches"] as JsonArray ?? [];
                    if (src["matches"] is JsonArray srcMatches)
                    {
                        foreach (var match in srcMatches)
                        {
                            destMatches.Add(match is null ? null : match.DeepClone());
                        }
                    }

                    dest["matches"] = destMatches;
                    var destCount = dest["matchCount"]?.GetValue<int>() ?? 0;
                    var srcCount = src["matchCount"]?.GetValue<int>() ?? 0;
                    dest["matchCount"] = destCount + srcCount;
                    dest["found"] = destCount + srcCount > 0;
                    dest["truncated"] = (dest["truncated"]?.GetValue<bool>() ?? false)
                                        || (src["truncated"]?.GetValue<bool>() ?? false);
                    var warning = dest["warning"]?.GetValue<string>();
                    if (string.IsNullOrWhiteSpace(warning))
                    {
                        dest["warning"] = src["warning"]?.GetValue<string>();
                    }
                }
            }

            if (ok == 0)
            {
                return (502, new { message = "تعذر البحث في قواعد الفروع." });
            }

            var queriesOut = mergedQueries ?? [];
            var found = 0;
            var missing = 0;
            foreach (var item in queriesOut)
            {
                if (item is JsonObject q && (q["found"]?.GetValue<bool>() ?? false))
                {
                    found++;
                }
                else
                {
                    missing++;
                }
            }

            return (200, new JsonObject
            {
                ["readOnly"] = true,
                ["nameCount"] = queriesOut.Count,
                ["foundCount"] = found,
                ["missingCount"] = missing,
                ["queries"] = queriesOut
            });
        }

        public async Task<(int Status, object? Body)> DashboardAsync(
            GatewayUser user,
            string? cityValue,
            CancellationToken ct)
        {
            var targets = await GetTargetsAsync(cityValue, ct);
            if (targets.Count == 0 && !string.IsNullOrWhiteSpace(cityValue))
            {
                return (400, new { message = "المحافظة غير موجودة." });
            }

            var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["employeesOnShift"] = 0,
                ["employeesOffShift"] = 0,
                ["liveLocations"] = 0,
                ["salesToday"] = 0,
                ["pendingSales"] = 0,
                ["newSalesRequests"] = 0
            };

            var ok = 0;
            string? lastError = null;
            await Task.WhenAll(targets.Select(async city =>
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(BranchTimeout);
                    using var response = await _proxy.SendManagerAsync(
                        city.Link, "sales-manager/dashboard", HttpMethod.Get, null, user.UserName, cts.Token);
                    var raw = await response.Content.ReadAsStringAsync(ct);
                    if (!response.IsSuccessStatusCode)
                    {
                        lastError = $"تعذر تحميل نظرة عامة من {city.Name}.";
                        return;
                    }

                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
                    lock (totals)
                    {
                        foreach (var key in totals.Keys.ToList())
                        {
                            totals[key] += ReadInt(doc.RootElement, key);
                        }

                        ok++;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                }
            }));

            if (targets.Count > 0 && ok == 0)
            {
                return (502, new { message = string.IsNullOrWhiteSpace(lastError) ? "تعذر تحميل نظرة عامة." : lastError });
            }

            return (200, totals);
        }

        public async Task<(int Status, object? Body)> EvaluateAcrossBranchesAsync(
            GatewayUser user,
            string jsonBody,
            CancellationToken ct)
        {
            // Search scope = request home province only. ACL gates access to that branch;
            // it does not expand evaluation across every allowed city.
            var groups = SalesRequestEvaluationRouter.GroupItemsBySourceCity(jsonBody);
            if (groups.Count == 0)
            {
                return (400, new { message = "لا توجد عناصر تقييم مع محافظة مصدر صالحة." });
            }

            var work = new List<(AdminCity City, string Body)>();
            var denied = 0;
            foreach (var (cityValue, items) in groups)
            {
                var city = await FindAsync(cityValue, ct);
                if (city is null)
                {
                    denied++;
                    continue;
                }

                work.Add((city, SalesRequestEvaluationRouter.BuildEvaluatePayload(items).ToJsonString()));
            }

            if (work.Count == 0)
            {
                if (denied > 0)
                {
                    return (403, new { message = "غير مصرح بالوصول لمحافظة الطلب." });
                }

                return (400, new { message = "لا توجد محافظات مسموحة للتقييم." });
            }

            var chunks = await Task.WhenAll(work.Select(async w =>
            {
                var body = await FetchJsonPostAsync(
                    user, w.City, "sales-manager/sales-requests/evaluate", w.Body, ct);
                return (w.City.Value, body);
            }));

            var ok = chunks.Count(c => c.body is not null);
            if (ok == 0)
            {
                return (502, new { message = "تعذر تقييم الطلبات في محافظة المصدر." });
            }

            return (200, JsonSerializer.Deserialize<object>(
                SalesRequestEvaluationRouter.CollectHomeBranchSummaries(chunks).ToJsonString())!);
        }

        public async Task<(int Status, object? Body)> EvaluationHitsAcrossBranchesAsync(
            GatewayUser user,
            string jsonBody,
            CancellationToken ct)
        {
            if (!SalesRequestEvaluationRouter.TryReadHitsSourceCity(jsonBody, out var sourceCity))
            {
                return (400, new { message = "sourceCityValue مطلوب لتحميل نتائج التقييم." });
            }

            var city = await FindAsync(sourceCity, ct);
            if (city is null)
            {
                return (403, new { message = "غير مصرح بالوصول لمحافظة الطلب." });
            }

            var page = 1;
            var pageSize = 30;
            try
            {
                var node = JsonNode.Parse(string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody) as JsonObject;
                page = node?["page"]?.GetValue<int>()
                       ?? node?["Page"]?.GetValue<int>()
                       ?? 1;
                pageSize = node?["pageSize"]?.GetValue<int>()
                           ?? node?["PageSize"]?.GetValue<int>()
                           ?? 30;
            }
            catch
            {
                // keep defaults
            }

            var (ok, body, branchStatus, branchMessage) = await FetchJsonPostDetailedAsync(
                user, city, "sales-manager/sales-requests/evaluation-hits", jsonBody, ct);
            if (!ok || body is null)
            {
                var detail = string.IsNullOrWhiteSpace(branchMessage)
                    ? "تعذر تحميل نتائج التقييم من محافظة الطلب."
                    : branchMessage;
                // Prefer branch 4xx when present; otherwise 502 gateway failure.
                var status = branchStatus is >= 400 and < 500 ? branchStatus.Value : 502;
                return (status, new { message = detail });
            }

            return (200, JsonSerializer.Deserialize<object>(
                SalesRequestEvaluationRouter.HitsFromHomeBranch(
                    city.Value, city.Name, body, page, pageSize).ToJsonString())!);
        }

        public async Task<(int Status, object? Body)> TransferProvinceAsync(
            GatewayUser user,
            string fromCityValue,
            int requestId,
            string jsonBody,
            CancellationToken ct)
        {
            var fromCity = await FindAsync(fromCityValue, ct);
            if (fromCity == null)
            {
                return (404, new { message = "محافظة المصدر غير موجودة أو غير مسموحة." });
            }

            JsonObject? body;
            try
            {
                body = JsonNode.Parse(string.IsNullOrWhiteSpace(jsonBody) ? "{}" : jsonBody) as JsonObject;
            }
            catch
            {
                return (400, new { message = "جسم الطلب غير صالح." });
            }

            var toCityValue = body?["toCityValue"]?.ToString()
                              ?? body?["ToCityValue"]?.ToString()
                              ?? "";
            var toCityName = body?["toCityName"]?.ToString()
                             ?? body?["ToCityName"]?.ToString();
            var toEmployeeId = ReadNullableIntNode(body, "toEmployeeId", "ToEmployeeId");
            var toEmployeeName = body?["toEmployeeName"]?.ToString()
                                 ?? body?["ToEmployeeName"]?.ToString();

            if (string.IsNullOrWhiteSpace(toCityValue))
            {
                return (400, new { message = "المحافظة الهدف مطلوبة." });
            }

            if (string.Equals(fromCity.Value, toCityValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fromCity.Name, toCityValue, StringComparison.OrdinalIgnoreCase))
            {
                return (400, new { message = "المحافظة الهدف يجب أن تختلف عن المصدر." });
            }

            var toCity = await FindAsync(toCityValue, ct);
            if (toCity == null)
            {
                return (404, new { message = "المحافظة الهدف غير موجودة أو غير مسموحة." });
            }

            // Load source request (ACL: fromCity must be in GetSalesBranches).
            using (var getCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                getCts.CancelAfter(BranchTimeout);
                using var getResponse = await _proxy.SendManagerAsync(
                    fromCity.Link,
                    $"sales-manager/sales-requests/{requestId}",
                    HttpMethod.Get,
                    null,
                    user.UserName,
                    getCts.Token);
                var getRaw = await getResponse.Content.ReadAsStringAsync(ct);
                if (!getResponse.IsSuccessStatusCode)
                {
                    return ((int)getResponse.StatusCode,
                        string.IsNullOrWhiteSpace(getRaw) ? null : BranchProxyService.TryParseJson(getRaw));
                }

                var source = JsonNode.Parse(string.IsNullOrWhiteSpace(getRaw) ? "{}" : getRaw) as JsonObject;
                if (source is null)
                {
                    return (502, new { message = "تعذر قراءة الطلب المصدر." });
                }

                var status = source["status"]?.ToString() ?? source["Status"]?.ToString() ?? "";
                var converted = ReadNullableIntNode(source, "convertedToSaleId", "ConvertedToSaleId");
                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(status, "ConvertedToSale", StringComparison.OrdinalIgnoreCase)
                    || converted is > 0)
                {
                    return (409, new
                    {
                        message = "لا يمكن نقل طلب مكتمل أو مرتبط بمبيع. غيّر المحافظة مرفوض للحفاظ على سلامة البيانات المالية."
                    });
                }

                var accept = new JsonObject
                {
                    ["fromRequestId"] = requestId,
                    ["fromCityValue"] = fromCity.Value,
                    ["fromCityName"] = fromCity.Name,
                    ["customerName"] = body?["customerName"] ?? body?["CustomerName"]
                                       ?? source["customerName"] ?? source["CustomerName"],
                    ["customerPhone"] = body?["customerPhone"] ?? body?["CustomerPhone"]
                                        ?? source["customerPhone"] ?? source["CustomerPhone"],
                    ["customerProvince"] = toCityName ?? toCity.Name,
                    ["customerAddress"] = body?["customerAddress"] ?? body?["CustomerAddress"]
                                         ?? source["customerAddress"] ?? source["CustomerAddress"],
                    ["notes"] = body?["notes"] ?? body?["Notes"]
                                ?? source["notes"] ?? source["Notes"],
                    ["customerSourceType"] = source["customerSourceType"] ?? source["CustomerSourceType"],
                    ["existingCustomerId"] = source["existingCustomerId"] ?? source["ExistingCustomerId"],
                    ["customerSourceCityValue"] = source["customerSourceCityValue"] ?? source["CustomerSourceCityValue"],
                    ["saleRequestType"] = source["saleRequestType"] ?? source["SaleRequestType"],
                    ["sourceListId"] = source["sourceListId"] ?? source["SourceListId"],
                    ["createdByName"] = source["createdByName"] ?? source["CreatedByName"],
                    ["createdByUserType"] = source["createdByUserType"] ?? source["CreatedByUserType"],
                    ["cityName"] = toCityName ?? toCity.Name
                };

                // Old-branch assignee cannot remain on the destination branch.
                if (toEmployeeId is > 0)
                {
                    accept["toEmployeeId"] = toEmployeeId.Value;
                    accept["toEmployeeName"] = toEmployeeName;
                }

                using var acceptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                acceptCts.CancelAfter(BranchTimeout);
                using var acceptResponse = await _proxy.SendManagerAsync(
                    toCity.Link,
                    "sales-manager/sales-requests/accept-province-transfer",
                    HttpMethod.Post,
                    accept.ToJsonString(),
                    user.UserName,
                    acceptCts.Token);
                var acceptRaw = await acceptResponse.Content.ReadAsStringAsync(ct);
                if (!acceptResponse.IsSuccessStatusCode)
                {
                    return ((int)acceptResponse.StatusCode,
                        string.IsNullOrWhiteSpace(acceptRaw) ? null : BranchProxyService.TryParseJson(acceptRaw));
                }

                var created = JsonNode.Parse(string.IsNullOrWhiteSpace(acceptRaw) ? "{}" : acceptRaw) as JsonObject;
                var toRequestId = created?["id"]?.GetValue<int>()
                                  ?? created?["Id"]?.GetValue<int>()
                                  ?? 0;
                if (toRequestId <= 0)
                {
                    return (502, new
                    {
                        message = "تم إنشاء الطلب في الفرع الهدف لكن المعرّف غير معروف — راجع الفرع الهدف يدوياً. المصدر لم يُؤرشف.",
                        destination = Stamp(acceptRaw, toCity)
                    });
                }

                var mark = new JsonObject
                {
                    ["toCityValue"] = toCity.Value,
                    ["toCityName"] = toCity.Name,
                    ["toRequestId"] = toRequestId
                };
                using var markCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                markCts.CancelAfter(BranchTimeout);
                using var markResponse = await _proxy.SendManagerAsync(
                    fromCity.Link,
                    $"sales-manager/sales-requests/{requestId}/mark-province-transferred-out",
                    HttpMethod.Post,
                    mark.ToJsonString(),
                    user.UserName,
                    markCts.Token);
                if (!markResponse.IsSuccessStatusCode)
                {
                    var markRaw = await markResponse.Content.ReadAsStringAsync(ct);
                    return (502, new
                    {
                        message = "تم إنشاء الطلب في الفرع الهدف لكن أرشفة المصدر فشلت. المصدر ما زال موجوداً — لا تفقد البيانات. أعد المحاولة أو أرشف يدوياً.",
                        destinationRequestId = toRequestId,
                        destinationCityValue = toCity.Value,
                        sourceRequestId = requestId,
                        sourceCityValue = fromCity.Value,
                        archiveError = string.IsNullOrWhiteSpace(markRaw) ? null : BranchProxyService.TryParseJson(markRaw)
                    });
                }

                if (created is not null)
                {
                    StampObject(created, toCity);
                }

                return (200, new JsonObject
                {
                    ["fromCityValue"] = fromCity.Value,
                    ["fromRequestId"] = requestId,
                    ["toCityValue"] = toCity.Value,
                    ["toRequestId"] = toRequestId,
                    ["assigneeCleared"] = toEmployeeId is null or <= 0,
                    ["request"] = created
                });
            }
        }

        private async Task<JsonNode?> FetchJsonPostAsync(
            GatewayUser user,
            AdminCity city,
            string companyPath,
            string jsonBody,
            CancellationToken ct)
        {
            var (ok, body, _, _) = await FetchJsonPostDetailedAsync(user, city, companyPath, jsonBody, ct);
            return ok ? body : null;
        }

        private async Task<(bool Ok, JsonNode? Body, int? Status, string? Message)> FetchJsonPostDetailedAsync(
            GatewayUser user,
            AdminCity city,
            string companyPath,
            string jsonBody,
            CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(ExcelSearchTimeout);
                using var response = await _proxy.SendManagerAsync(
                    city.Link, companyPath, HttpMethod.Post, jsonBody, user.UserName, cts.Token);
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    var msg = TryReadMessage(raw)
                              ?? $"فشل الفرع ({(int)response.StatusCode}) أثناء تحميل نتائج التقييم.";
                    return (false, null, (int)response.StatusCode, msg);
                }

                return (true, JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw), (int)response.StatusCode, null);
            }
            catch (OperationCanceledException)
            {
                return (false, null, 504, "انتهت مهلة تحميل نتائج التقييم من محافظة الطلب.");
            }
            catch
            {
                return (false, null, null, null);
            }
        }

        private static string? TryReadMessage(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("message", out var m)
                    || doc.RootElement.TryGetProperty("Message", out m))
                {
                    var s = m.GetString();
                    return string.IsNullOrWhiteSpace(s) ? null : s;
                }
            }
            catch
            {
                // ignore
            }

            return null;
        }

        // Follower-ops / rating / profile: branch WAN can exceed 60s nginx defaults if slow;
        // keep gateway wait generous; BE endpoints are optimized to finish well under this.
        private static readonly TimeSpan ExcelSearchTimeout = TimeSpan.FromSeconds(120);

        private async Task<(AdminCity City, JsonNode? Node)> FetchExcelSearchAsync(
            GatewayUser user,
            AdminCity city,
            string jsonBody,
            CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(ExcelSearchTimeout);
                using var response = await _proxy.SendManagerAsync(
                    city.Link, "sales-manager/customers/excel-search", HttpMethod.Post, jsonBody, user.UserName, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    return (city, null);
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                return (city, JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw));
            }
            catch
            {
                return (city, null);
            }
        }

        private static void StampExcelMatches(JsonArray queries, AdminCity city)
        {
            foreach (var item in queries)
            {
                if (item is not JsonObject query || query["matches"] is not JsonArray matches)
                {
                    continue;
                }

                foreach (var matchNode in matches)
                {
                    if (matchNode is not JsonObject match)
                    {
                        continue;
                    }

                    match["cityValue"] = city.Value;
                    var customerId = match["customerId"]?.GetValue<int>()
                                     ?? match["CustomerId"]?.GetValue<int>()
                                     ?? 0;
                    match["resultKey"] = city.Value + ":" + customerId;
                    var display = FirstHumanCity(city.Name, match["province"]?.GetValue<string>() ?? match["Province"]?.GetValue<string>(), city.Value);
                    if (string.IsNullOrWhiteSpace(display))
                    {
                        display = FirstHumanCity(city.Name, null, city.Value);
                    }

                    if (!string.IsNullOrWhiteSpace(display))
                    {
                        match["cityName"] = display;
                        var province = match["province"]?.GetValue<string>();
                        if (string.IsNullOrWhiteSpace(province)
                            || StartsWithDatabase(province)
                            || string.Equals(province, city.Value, StringComparison.OrdinalIgnoreCase))
                        {
                            match["province"] = display;
                        }
                    }
                }
            }
        }

        private Task<List<JsonNode>> FetchArrayAsync(
            GatewayUser user,
            AdminCity city,
            string companyPath,
            CancellationToken ct) =>
            FetchArrayAsync(user, city, companyPath, BranchTimeout, ct);

        private async Task<List<JsonNode>> FetchArrayAsync(
            GatewayUser user,
            AdminCity city,
            string companyPath,
            TimeSpan timeout,
            CancellationToken ct)
        {
            var (_, rows) = await TryFetchArrayAsync(user, city, companyPath, timeout, ct);
            return rows;
        }

        /// <summary>
        /// Soft-fail for fan-out (skip dead branches). Exact routing uses the bool.
        /// </summary>
        private Task<(bool Ok, List<JsonNode> Rows)> TryFetchArrayAsync(
            GatewayUser user,
            AdminCity city,
            string companyPath,
            CancellationToken ct)
        {
            var timeout = NeedsLongBranchTimeout(companyPath) ? ExcelSearchTimeout : BranchTimeout;
            return TryFetchArrayAsync(user, city, companyPath, timeout, ct);
        }

        private async Task<(bool Ok, List<JsonNode> Rows)> TryFetchArrayAsync(
            GatewayUser user,
            AdminCity city,
            string companyPath,
            TimeSpan timeout,
            CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
                using var response = await _proxy.SendManagerAsync(
                    city.Link, companyPath, HttpMethod.Get, null, user.UserName, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    return (false, []);
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                var node = JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "[]" : raw);
                if (node is not JsonArray array)
                {
                    return (false, []);
                }

                var rows = new List<JsonNode>();
                foreach (var item in array)
                {
                    if (item is JsonObject obj)
                    {
                        StampObject(obj, city);
                        rows.Add(obj);
                    }
                }

                return (true, rows);
            }
            catch
            {
                return (false, []);
            }
        }

        private async Task<IReadOnlyList<AdminCity>> GetTargetsAsync(string? cityValue, CancellationToken ct)
        {
            var cities = await _cities.GetSalesBranchesAsync(ct);
            return SalesBranchResolver.ResolveTargets(cities, cityValue);
        }

        /// <summary>
        /// Exact one-branch array fetch. Unknown city → 404 (no Najaf fallback).
        /// Unreachable branch → 502 (never disguise as empty list / invalid credentials).
        /// </summary>
        public async Task<(int Status, object? Body)> GetExactBranchArrayAsync(
            GatewayUser user,
            string cityValue,
            string companyPath,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                return (400, new { message = "يجب تحديد المحافظة." });
            }

            var cities = await _cities.GetSalesBranchesAsync(ct);
            var city = SalesBranchResolver.ResolveExact(cities, cityValue);
            if (city is null)
            {
                return (404, new { message = "المحافظة غير موجودة أو غير مسموحة لمدير المبيعات." });
            }

            var timeout = NeedsLongBranchTimeout(companyPath) ? ExcelSearchTimeout : BranchTimeout;
            var (ok, rows) = await TryFetchArrayAsync(user, city, companyPath, timeout, ct);
            if (!ok)
            {
                return (502, new { message = "تعذر الاتصال بخادم المحافظة." });
            }

            return (200, rows);
        }

        private async Task<AdminCity?> FindAsync(string cityValue, CancellationToken ct)
        {
            var cities = await _cities.GetSalesBranchesAsync(ct);
            return SalesBranchResolver.ResolveExact(cities, cityValue);
        }

        private static bool NeedsLongFileTimeout(string companyPath)
        {
            if (string.IsNullOrWhiteSpace(companyPath))
            {
                return false;
            }

            return companyPath.Contains("customer-documents", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("shop-image", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("/documents/", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("download", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Paths that routinely exceed the default 8s single-branch CancelAfter
        /// (rating / summary / متابعة المندوبين).
        /// </summary>
        public static bool NeedsLongBranchTimeout(string companyPath)
        {
            if (string.IsNullOrWhiteSpace(companyPath))
            {
                return false;
            }

            if (companyPath.Contains("exception-review-context", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("stats/trend", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("stats/branch-month", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("director-summary", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("director-follower-ops", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("director-delegate-followup", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("rating-trend", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("rating-customers", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("/profile", StringComparison.OrdinalIgnoreCase)
                || companyPath.Contains("customers/profile", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // sales-manager/employees or sales-manager/followers list (with optional query) —
            // both attach latest ratings server-side.
            foreach (var prefix in new[] { "sales-manager/employees", "sales-manager/followers" })
            {
                var idx = companyPath.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    continue;
                }

                var after = companyPath[(idx + prefix.Length)..];
                if (after.Length == 0 || after[0] == '?')
                {
                    return true;
                }
            }

            return false;
        }

        private static object? Stamp(string raw, AdminCity city)
        {
            try
            {
                var node = JsonNode.Parse(raw);
                if (node is JsonObject obj)
                {
                    StampObject(obj, city);
                    // Paged sales-requests: stamp each item so FE branch keys stay correct.
                    StampItemsArray(obj["items"] as JsonArray ?? obj["Items"] as JsonArray, city);
                    return obj;
                }

                return BranchProxyService.TryParseJson(raw);
            }
            catch (JsonException)
            {
                return raw;
            }
        }

        private static void StampItemsArray(JsonArray? items, AdminCity city)
        {
            if (items == null)
            {
                return;
            }

            foreach (var item in items.OfType<JsonObject>())
            {
                StampObject(item, city);
            }
        }

        private static void StampObject(JsonObject obj, AdminCity city)
        {
            obj["cityValue"] = city.Value;
            obj["cityName"] = city.Name;
            obj["branchName"] = city.Name;
            ReplaceIfInternal(obj, "province", city.Name, city);
            ReplaceIfInternal(obj, "Province", city.Name, city);
            ReplaceIfInternal(obj, "customerProvince", city.Name, city);
            ReplaceIfInternal(obj, "CustomerProvince", city.Name, city);
            obj["sourceCityName"] = city.Name;
            obj["branchDatabase"] = city.Database;
            obj["branchKey"] = $"{city.Value}:{ReadId(obj)}";
        }

        private static void ReplaceIfInternal(JsonObject obj, string key, string display, AdminCity city)
        {
            if (string.IsNullOrWhiteSpace(display))
            {
                return;
            }

            obj.TryGetPropertyValue(key, out var current);
            var text = current?.ToString();
            if (string.IsNullOrWhiteSpace(text)
                || string.Equals(text, city.Value, StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, city.Database, StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Database", StringComparison.OrdinalIgnoreCase))
            {
                obj[key] = display;
            }
        }

        private static string ReadId(JsonObject obj)
        {
            foreach (var name in new[] { "customerId", "CustomerId", "employeeId", "EmployeeId", "saleId", "SaleId", "id", "Id" })
            {
                if (obj.TryGetPropertyValue(name, out var value) && value != null)
                {
                    return value.ToString();
                }
            }

            return "";
        }

        private static int ReadInt(JsonElement element, string name)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var n))
                {
                    return n;
                }

                if (prop.Value.ValueKind == JsonValueKind.String
                    && int.TryParse(prop.Value.GetString(), out var parsed))
                {
                    return parsed;
                }
            }

            return 0;
        }

        private static int? ReadNullableIntNode(JsonObject? obj, params string[] names)
        {
            if (obj is null)
            {
                return null;
            }

            foreach (var name in names)
            {
                if (!obj.TryGetPropertyValue(name, out var node) || node is null
                    || node.GetValueKind() == JsonValueKind.Null)
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

        private static void ShapeCustomer(JsonObject obj)
        {
            var name = ReadAny(obj, "customerName", "CustomerName", "fullName", "FullName");
            var phone = ReadAny(obj, "phone", "Phone");
            var id = ReadAny(obj, "customerId", "CustomerId");
            if (!string.IsNullOrWhiteSpace(name))
            {
                obj["customerName"] = name;
                obj["fullName"] = name;
            }

            if (!string.IsNullOrWhiteSpace(phone))
            {
                obj["phone"] = phone;
            }

            if (int.TryParse(id, out var customerId) && customerId > 0)
            {
                obj["customerId"] = customerId;
            }

            var cityName = ReadAny(obj, "cityName", "CityName", "sourceCityName", "SourceCityName");
            var province = ReadAny(obj, "province", "Province");
            var cityValue = ReadAny(obj, "cityValue", "CityValue", "sourceCityValue", "SourceCityValue");
            if (!string.IsNullOrWhiteSpace(cityValue))
            {
                obj["cityValue"] = cityValue;
            }

            var display = FirstHumanCity(cityName, province, cityValue);
            if (!string.IsNullOrWhiteSpace(display))
            {
                obj["cityName"] = display;
                obj["province"] = display;
            }

            var address = ReadAny(obj, "address", "Address");
            if (!string.IsNullOrWhiteSpace(address))
            {
                obj["address"] = address;
            }

            obj["branchKey"] = $"{ReadAny(obj, "cityValue")}:{ReadAny(obj, "customerId")}";
        }

        private static string FirstHumanCity(string? cityName, string? province, string? cityValue)
        {
            foreach (var item in new[] { cityName, province })
            {
                var text = item?.Trim() ?? string.Empty;
                if (text.Length == 0)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(cityValue)
                    && string.Equals(text, cityValue, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (text.StartsWith("Database", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return text;
            }

            return string.Empty;
        }

        private static bool StartsWithDatabase(string? value) =>
            (value ?? string.Empty).StartsWith("Database", StringComparison.OrdinalIgnoreCase);

        private static bool CustomerMatches(JsonObject obj, string normalizedQuery)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return true;
            }

            var hay = NormalizeArabic(string.Join(" ",
                ReadAny(obj, "customerName", "fullName"),
                ReadAny(obj, "phone"),
                ReadAny(obj, "cityName", "branchName")));
            return hay.Contains(normalizedQuery, StringComparison.Ordinal);
        }

        internal static string NormalizeArabic(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var chars = value.Trim()
                .Replace("أ", "ا", StringComparison.Ordinal)
                .Replace("إ", "ا", StringComparison.Ordinal)
                .Replace("آ", "ا", StringComparison.Ordinal)
                .Replace("ى", "ي", StringComparison.Ordinal)
                .ToCharArray();
            var sb = new System.Text.StringBuilder(chars.Length);
            var space = false;
            foreach (var c in chars)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!space && sb.Length > 0)
                    {
                        sb.Append(' ');
                    }

                    space = true;
                    continue;
                }

                space = false;
                sb.Append(c);
            }

            return sb.ToString();
        }

        private static string ReadAny(JsonObject obj, params string[] names)
        {
            foreach (var name in names)
            {
                if (obj.TryGetPropertyValue(name, out var value) && value != null)
                {
                    return value.ToString() ?? "";
                }
            }

            return "";
        }
    }
}
