using System.Text.Json;
using System.Text.Json.Nodes;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.Services
{
    public interface ISalesManagerBranchAggregator
    {
        Task<IReadOnlyList<AdminCity>> BranchesAsync(CancellationToken ct);
        Task<(int Status, object? Body)> GetAsync(GatewayUser user, string? cityValue, string companyPath, CancellationToken ct);
        Task<(int Status, object? Body)> GetOneAsync(GatewayUser user, string cityValue, string companyPath, CancellationToken ct);
        Task<(int Status, object? Body)> GetFileAsync(GatewayUser user, string cityValue, string companyPath, CancellationToken ct);
        Task<(int Status, object? Body)> PostAsync(GatewayUser user, string cityValue, string companyPath, string jsonBody, CancellationToken ct);
        Task<(int Status, object? Body)> SearchCustomersAsync(GatewayUser user, string? query, CancellationToken ct);
        Task<(int Status, object? Body)> DashboardAsync(GatewayUser user, string? cityValue, CancellationToken ct);
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

            var chunks = await Task.WhenAll(targets.Select(city => FetchArrayAsync(user, city, companyPath, ct)));
            var merged = new List<JsonNode>();
            foreach (var chunk in chunks)
            {
                merged.AddRange(chunk);
            }

            return (200, merged);
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

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(BranchTimeout);
            using var response = await _proxy.SendManagerAsync(
                city.Link, companyPath, HttpMethod.Get, null, user.UserName, cts.Token);
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, string.IsNullOrWhiteSpace(raw) ? null : BranchProxyService.TryParseJson(raw));
            }

            return ((int)response.StatusCode, Stamp(raw, city));
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
            cts.CancelAfter(BranchTimeout);
            using var response = await _proxy.SendManagerAsync(
                city.Link, companyPath, HttpMethod.Get, null, user.UserName, cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(ct);
                return ((int)response.StatusCode, string.IsNullOrWhiteSpace(raw) ? null : BranchProxyService.TryParseJson(raw));
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
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
            cts.CancelAfter(BranchTimeout);
            using var response = await _proxy.SendManagerAsync(
                city.Link, companyPath, HttpMethod.Post, jsonBody, user.UserName, cts.Token);
            var raw = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, string.IsNullOrWhiteSpace(raw) ? null : Stamp(raw, city) ?? BranchProxyService.TryParseJson(raw));
        }

        public async Task<(int Status, object? Body)> SearchCustomersAsync(
            GatewayUser user,
            string? query,
            CancellationToken ct)
        {
            var q = NormalizeArabic(query);
            if (q.Length < 2)
            {
                return (400, new { message = "اكتب حرفين على الأقل للبحث" });
            }

            var targets = await GetTargetsAsync(null, ct);
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

        public async Task<(int Status, object? Body)> DashboardAsync(
            GatewayUser user,
            string? cityValue,
            CancellationToken ct)
        {
            var targets = await GetTargetsAsync(cityValue, ct);
            var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["employeesOnShift"] = 0,
                ["employeesOffShift"] = 0,
                ["liveLocations"] = 0,
                ["salesToday"] = 0,
                ["pendingSales"] = 0,
                ["newSalesRequests"] = 0
            };

            await Task.WhenAll(targets.Select(async city =>
            {
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(BranchTimeout);
                    using var response = await _proxy.SendManagerAsync(
                        city.Link, "sales-manager/dashboard", HttpMethod.Get, null, user.UserName, cts.Token);
                    if (!response.IsSuccessStatusCode)
                    {
                        return;
                    }

                    var raw = await response.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
                    lock (totals)
                    {
                        foreach (var key in totals.Keys.ToList())
                        {
                            totals[key] += ReadInt(doc.RootElement, key);
                        }
                    }
                }
                catch
                {
                    // one branch must not fail the dashboard
                }
            }));

            return (200, totals);
        }

        private async Task<List<JsonNode>> FetchArrayAsync(
            GatewayUser user,
            AdminCity city,
            string companyPath,
            CancellationToken ct)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(BranchTimeout);
                using var response = await _proxy.SendManagerAsync(
                    city.Link, companyPath, HttpMethod.Get, null, user.UserName, cts.Token);
                if (!response.IsSuccessStatusCode)
                {
                    return [];
                }

                var raw = await response.Content.ReadAsStringAsync(ct);
                var node = JsonNode.Parse(string.IsNullOrWhiteSpace(raw) ? "[]" : raw);
                if (node is not JsonArray array)
                {
                    return [];
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

                return rows;
            }
            catch
            {
                return [];
            }
        }

        private async Task<IReadOnlyList<AdminCity>> GetTargetsAsync(string? cityValue, CancellationToken ct)
        {
            var cities = await _cities.GetSalesBranchesAsync(ct);
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                return cities;
            }

            return cities.Where(c =>
                string.Equals(c.Value, cityValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Name, cityValue, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private async Task<AdminCity?> FindAsync(string cityValue, CancellationToken ct)
        {
            var matches = await GetTargetsAsync(cityValue, ct);
            return matches.FirstOrDefault();
        }

        private static object? Stamp(string raw, AdminCity city)
        {
            try
            {
                var node = JsonNode.Parse(raw);
                if (node is JsonObject obj)
                {
                    StampObject(obj, city);
                    return obj;
                }

                return BranchProxyService.TryParseJson(raw);
            }
            catch (JsonException)
            {
                return raw;
            }
        }

        private static void StampObject(JsonObject obj, AdminCity city)
        {
            obj["cityValue"] = city.Value;
            obj["cityName"] = city.Name;
            obj["branchName"] = city.Name;
            obj["branchDatabase"] = city.Database;
            obj["branchKey"] = $"{city.Value}:{ReadId(obj)}";
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

                if (int.TryParse(prop.Value.ToString(), out var parsed))
                {
                    return parsed;
                }
            }

            return 0;
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

            var cityValue = ReadAny(obj, "cityValue", "CityValue", "sourceCityValue", "SourceCityValue");
            if (!string.IsNullOrWhiteSpace(cityValue))
            {
                obj["cityValue"] = cityValue;
            }

            obj["branchKey"] = $"{ReadAny(obj, "cityValue")}:{ReadAny(obj, "customerId")}";
        }

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
