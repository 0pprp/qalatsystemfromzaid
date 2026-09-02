using System.Text.Json;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.Sales.Services
{
    public interface IGlobalCustomerSearchService
    {
        Task<(int StatusCode, object? Body, string? Error)> SearchAsync(
            string query,
            GatewayUser user,
            CancellationToken ct);
    }

    /// <summary>
    /// This phase searches Najaf Demo BE_Company only.
    /// Later, iterate every CityLink without changing the Flutter contract.
    /// </summary>
    public sealed class GatewayGlobalCustomerSearchService : IGlobalCustomerSearchService
    {
        private readonly AdminCitiesService _cities;
        private readonly BranchProxyService _proxy;
        private readonly SalesDevelopmentGuard _guard;

        public GatewayGlobalCustomerSearchService(
            AdminCitiesService cities,
            BranchProxyService proxy,
            SalesDevelopmentGuard guard)
        {
            _cities = cities;
            _proxy = proxy;
            _guard = guard;
        }

        public async Task<(int StatusCode, object? Body, string? Error)> SearchAsync(
            string query,
            GatewayUser user,
            CancellationToken ct)
        {
            var cities = await _cities.GetCitiesAsync(ct);
            var targets = cities.Where(c => _guard.IsAllowedDemo(c.Database)).ToList();
            if (targets.Count == 0)
            {
                return (503, null, $"Sales search is limited to {SalesDevelopmentGuard.AllowedDemoDatabase} in this phase.");
            }

            var merged = new List<object>();
            foreach (var city in targets)
            {
                var token = string.Equals(
                    AdminCitiesService.NormalizeLink(city.Link),
                    AdminCitiesService.NormalizeLink(user.CityLink),
                    StringComparison.OrdinalIgnoreCase)
                    ? user.BranchToken
                    : user.BranchToken;

                using var response = await _proxy.SendAuthorizedAsync(
                    city.Link,
                    $"sales/customers/search?q={Uri.EscapeDataString(query)}",
                    HttpMethod.Get,
                    token,
                    null,
                    ct);
                var raw = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    return ((int)response.StatusCode, BranchProxyService.TryParseJson(raw), null);
                }

                if (BranchProxyService.TryParseJson(raw) is JsonElement element && element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        merged.Add(item);
                    }
                }
            }

            return (200, merged, null);
        }
    }
}
