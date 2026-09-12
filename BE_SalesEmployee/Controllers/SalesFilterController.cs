using System.Text;
using System.Text.Json;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers
{
    [Authorize(Policy = SalesPolicies.SalesFilterEmployee)]
    [Route("api/sales-filter")]
    [ApiController]
    public class SalesFilterController : ControllerBase
    {
        private readonly AdminCitiesService _cities;
        private readonly BranchProxyService _proxy;

        public SalesFilterController(AdminCitiesService cities, BranchProxyService proxy)
        {
            _cities = cities;
            _proxy = proxy;
        }

        [HttpGet("me/cities")]
        public async Task<IActionResult> MyCities(CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            if (!user.IsSalesFilterEmployee)
            {
                return Forbid();
            }

            var catalog = await _cities.GetCitiesAsync(ct);
            var byValue = catalog
                .Where(c => !string.IsNullOrWhiteSpace(c.Value))
                .ToDictionary(c => c.Value, c => c, StringComparer.OrdinalIgnoreCase);

            var rows = new List<object>();
            foreach (var value in user.AllowedFilterCities)
            {
                if (!byValue.TryGetValue(value, out var city))
                {
                    continue;
                }

                rows.Add(new
                {
                    cityValue = city.Value,
                    cityName = city.Name,
                    value = city.Value,
                    name = city.Name,
                });
            }

            return Ok(rows);
        }

        [HttpGet("requests")]
        public async Task<IActionResult> List(
            [FromQuery] string? cityValue,
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30,
            CancellationToken ct = default)
        {
            var user = TokenService.FromPrincipal(User);
            var (city, error) = await ResolveAllowedCityAsync(user, cityValue, requireValue: true, ct);
            if (error != null)
            {
                return error;
            }

            var qs = new StringBuilder("sales-filter/requests?");
            qs.Append("city=").Append(Uri.EscapeDataString(city!.Value));
            if (!string.IsNullOrWhiteSpace(status))
            {
                qs.Append("&status=").Append(Uri.EscapeDataString(status.Trim()));
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                qs.Append("&search=").Append(Uri.EscapeDataString(search.Trim()));
            }
            qs.Append("&page=").Append(Math.Max(1, page));
            qs.Append("&pageSize=").Append(Math.Clamp(pageSize <= 0 ? 30 : pageSize, 1, 100));

            return await ProxyAsync(user, city, qs.ToString(), HttpMethod.Get, null, ct);
        }

        [HttpGet("requests/{cityValue}/{id:int}")]
        public Task<IActionResult> Get(string cityValue, int id, CancellationToken ct) =>
            ProxyByCityAsync(cityValue, $"sales-filter/requests/{id}", HttpMethod.Get, null, ct);

        [HttpPost("requests/{cityValue}/{id:int}/hold")]
        public Task<IActionResult> Hold(string cityValue, int id, [FromBody] JsonElement? body, CancellationToken ct) =>
            ProxyByCityAsync(cityValue, $"sales-filter/requests/{id}/hold", HttpMethod.Post, BodyText(body), ct);

        [HttpPost("requests/{cityValue}/{id:int}/ready")]
        public Task<IActionResult> Ready(string cityValue, int id, [FromBody] JsonElement? body, CancellationToken ct) =>
            ProxyByCityAsync(cityValue, $"sales-filter/requests/{id}/ready", HttpMethod.Post, BodyText(body), ct);

        [HttpPost("requests/{cityValue}/{id:int}/reject")]
        public Task<IActionResult> Reject(string cityValue, int id, [FromBody] JsonElement? body, CancellationToken ct) =>
            ProxyByCityAsync(cityValue, $"sales-filter/requests/{id}/reject", HttpMethod.Post, BodyText(body), ct);

        private async Task<IActionResult> ProxyByCityAsync(
            string cityValue,
            string relativePath,
            HttpMethod method,
            string? jsonBody,
            CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (city, error) = await ResolveAllowedCityAsync(user, cityValue, requireValue: true, ct);
            if (error != null)
            {
                return error;
            }

            return await ProxyAsync(user, city!, relativePath, method, jsonBody, ct);
        }

        private async Task<IActionResult> ProxyAsync(
            GatewayUser user,
            AdminCity city,
            string relativePath,
            HttpMethod method,
            string? jsonBody,
            CancellationToken ct)
        {
            using var response = await _proxy.SendFilterAsync(
                city.Link,
                relativePath,
                method,
                jsonBody,
                user.UserName,
                user.UserID,
                ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            return StatusCode(
                (int)response.StatusCode,
                string.IsNullOrWhiteSpace(raw) ? null : BranchProxyService.TryParseJson(raw));
        }

        private async Task<(AdminCity? City, IActionResult? Error)> ResolveAllowedCityAsync(
            GatewayUser user,
            string? cityValue,
            bool requireValue,
            CancellationToken ct)
        {
            if (!user.IsSalesFilterEmployee)
            {
                return (null, Forbid());
            }

            if (string.IsNullOrWhiteSpace(cityValue))
            {
                return requireValue
                    ? (null, BadRequest(new { message = "المحافظة مطلوبة" }))
                    : (null, null);
            }

            var trimmed = cityValue.Trim();
            if (!user.AllowsFilterCity(trimmed))
            {
                return (null, StatusCode(StatusCodes.Status403Forbidden, new { message = "المحافظة غير مسموحة لحسابك" }));
            }

            var catalog = await _cities.GetCitiesAsync(ct);
            var city = catalog.FirstOrDefault(c =>
                string.Equals(c.Value, trimmed, StringComparison.OrdinalIgnoreCase));
            if (city == null || string.IsNullOrWhiteSpace(city.Link))
            {
                return (null, NotFound(new { message = "المحافظة غير معروفة" }));
            }

            return (city, null);
        }

        private static string? BodyText(JsonElement? body)
        {
            if (body == null || body.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            {
                return "{}";
            }

            return body.Value.GetRawText();
        }
    }
}
