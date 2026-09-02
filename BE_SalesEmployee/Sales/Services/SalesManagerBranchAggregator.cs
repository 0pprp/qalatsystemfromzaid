using System.Text;
using System.Text.Json;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.Services
{
    public interface ISalesManagerBranchAggregator
    {
        Task<(int Status, object? Body)> GetAsync(GatewayUser user, string relativePath, CancellationToken ct);
        Task<(int Status, object? Body)> PostAsync(GatewayUser user, string cityValue, string relativePath, string jsonBody, CancellationToken ct);
    }

    /// <summary>
    /// Fans out SalesManager reads to each allowed demo branch CityLink.
    /// New branches come from LabCities / GetAdmin without frontend changes.
    /// </summary>
    public sealed class SalesManagerBranchAggregator : ISalesManagerBranchAggregator
    {
        private readonly AdminCitiesService _cities;
        private readonly BranchProxyService _proxy;
        private readonly SalesDevelopmentGuard _guard;

        public SalesManagerBranchAggregator(
            AdminCitiesService cities,
            BranchProxyService proxy,
            SalesDevelopmentGuard guard)
        {
            _cities = cities;
            _proxy = proxy;
            _guard = guard;
        }

        public async Task<(int Status, object? Body)> GetAsync(GatewayUser user, string relativePath, CancellationToken ct)
        {
            var targets = await TargetsAsync(ct);
            if (targets.Count == 0)
            {
                return (503, new { message = $"SalesManager is limited to {SalesDevelopmentGuard.AllowedDemoDatabase}." });
            }

            if (relativePath.Contains("/employees/", StringComparison.OrdinalIgnoreCase)
                || relativePath.Contains("/sales/", StringComparison.OrdinalIgnoreCase)
                || relativePath.Contains("/sales-requests/", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var city in targets)
                {
                    using var one = await _proxy.SendAuthorizedAsync(city.Link, relativePath, HttpMethod.Get, user.BranchToken, null, ct);
                    if (one.IsSuccessStatusCode)
                    {
                        var raw = await one.Content.ReadAsStringAsync(ct);
                        return ((int)one.StatusCode, BranchProxyService.TryParseJson(raw));
                    }
                }
            }

            var merged = new List<JsonElement>();
            object? dashboard = null;
            foreach (var city in targets)
            {
                using var response = await _proxy.SendAuthorizedAsync(city.Link, relativePath, HttpMethod.Get, user.BranchToken, null, ct);
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var parsed = BranchProxyService.TryParseJson(raw);
                if (parsed is JsonElement el && el.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in el.EnumerateArray())
                    {
                        merged.Add(item.Clone());
                    }
                }
                else if (relativePath.Contains("dashboard", StringComparison.OrdinalIgnoreCase) && parsed != null)
                {
                    dashboard = parsed;
                }
            }

            if (dashboard != null)
            {
                return (200, dashboard);
            }

            return (200, merged);
        }

        public async Task<(int Status, object? Body)> PostAsync(GatewayUser user, string cityValue, string relativePath, string jsonBody, CancellationToken ct)
        {
            var targets = await TargetsAsync(ct);
            var city = targets.FirstOrDefault(c =>
                           string.Equals(c.Value, cityValue, StringComparison.OrdinalIgnoreCase)
                           || string.Equals(c.Name, cityValue, StringComparison.OrdinalIgnoreCase))
                       ?? targets.FirstOrDefault(c =>
                           string.Equals(AdminCitiesService.NormalizeLink(c.Link), AdminCitiesService.NormalizeLink(user.CityLink), StringComparison.OrdinalIgnoreCase))
                       ?? targets.FirstOrDefault();
            if (city == null)
            {
                return (503, new { message = "لا يوجد فرع Demo متاح." });
            }

            using var response = await _proxy.SendAuthorizedAsync(
                city.Link,
                relativePath,
                HttpMethod.Post,
                user.BranchToken,
                new StringContent(jsonBody, Encoding.UTF8, "application/json"),
                ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, string.IsNullOrWhiteSpace(raw) ? null : BranchProxyService.TryParseJson(raw));
        }

        private async Task<List<AdminCity>> TargetsAsync(CancellationToken ct)
        {
            var cities = await _cities.GetCitiesAsync(ct);
            return cities.Where(c => _guard.IsAllowedDemo(c.Database)).ToList();
        }
    }
}
