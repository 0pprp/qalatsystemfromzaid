using System.Text.Json;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BE_SalesEmployee.Controllers
{
    [Authorize(Policy = SalesPolicies.SalesManager)]
    [Route("api/sales-manager")]
    [ApiController]
    public class SalesManagerController : ControllerBase
    {
        private readonly ISalesManagerBranchAggregator _aggregator;

        public SalesManagerController(ISalesManagerBranchAggregator aggregator)
        {
            _aggregator = aggregator;
        }

        [HttpGet("dashboard")]
        public Task<IActionResult> Dashboard(CancellationToken ct) =>
            ForwardGet("sales-manager/dashboard", ct);

        [HttpGet("employees")]
        public Task<IActionResult> Employees([FromQuery] string? cityValue, [FromQuery] string? shiftStatus, [FromQuery] string? locationStatus, CancellationToken ct) =>
            ForwardGet($"sales-manager/employees{Query(cityValue, shiftStatus, locationStatus)}", ct);

        [HttpGet("employees/{employeeId:int}")]
        public Task<IActionResult> Employee(int employeeId, CancellationToken ct) =>
            ForwardGet($"sales-manager/employees/{employeeId}", ct);

        [HttpGet("employees/{employeeId:int}/route")]
        public Task<IActionResult> Route(int employeeId, [FromQuery] DateTime? date, CancellationToken ct) =>
            ForwardGet($"sales-manager/employees/{employeeId}/route{(date == null ? "" : $"?date={date:yyyy-MM-dd}")}", ct);

        [HttpGet("employees/{employeeId:int}/tracking-events")]
        public Task<IActionResult> Events(int employeeId, [FromQuery] DateTime? date, CancellationToken ct) =>
            ForwardGet($"sales-manager/employees/{employeeId}/tracking-events{(date == null ? "" : $"?date={date:yyyy-MM-dd}")}", ct);

        [HttpGet("sales")]
        public Task<IActionResult> Sales([FromQuery] string? cityValue, [FromQuery] int? employeeId, [FromQuery] string? status, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var q = new List<string>();
            if (!string.IsNullOrWhiteSpace(cityValue)) q.Add($"cityValue={Uri.EscapeDataString(cityValue)}");
            if (employeeId != null) q.Add($"employeeId={employeeId}");
            if (!string.IsNullOrWhiteSpace(status)) q.Add($"status={Uri.EscapeDataString(status)}");
            if (date != null) q.Add($"date={date:yyyy-MM-dd}");
            var suffix = q.Count == 0 ? "" : "?" + string.Join("&", q);
            return ForwardGet("sales-manager/sales" + suffix, ct);
        }

        [HttpGet("sales/{saleId:int}")]
        public Task<IActionResult> Sale(int saleId, CancellationToken ct) =>
            ForwardGet($"sales-manager/sales/{saleId}", ct);

        [HttpGet("customers/search")]
        public Task<IActionResult> Search([FromQuery] string? q, CancellationToken ct) =>
            ForwardGet($"sales-manager/customers/search?q={Uri.EscapeDataString(q ?? "")}", ct);

        [HttpGet("sales-requests")]
        public Task<IActionResult> Requests([FromQuery] string? cityValue, [FromQuery] int? employeeId, [FromQuery] string? status, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var q = new List<string>();
            if (!string.IsNullOrWhiteSpace(cityValue)) q.Add($"cityValue={Uri.EscapeDataString(cityValue)}");
            if (employeeId != null) q.Add($"employeeId={employeeId}");
            if (!string.IsNullOrWhiteSpace(status)) q.Add($"status={Uri.EscapeDataString(status)}");
            if (date != null) q.Add($"date={date:yyyy-MM-dd}");
            var suffix = q.Count == 0 ? "" : "?" + string.Join("&", q);
            return ForwardGet("sales-manager/sales-requests" + suffix, ct);
        }

        [HttpGet("sales-requests/{id:int}")]
        public Task<IActionResult> RequestDetails(int id, CancellationToken ct) =>
            ForwardGet($"sales-manager/sales-requests/{id}", ct);

        [HttpPost("sales-requests")]
        public async Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var city = Read(body, "cityValue", "CityValue") ?? user.CityValue;
            var (status, payload) = await _aggregator.PostAsync(user, city ?? "", "sales-manager/sales-requests", body.GetRawText(), ct);
            return StatusCode(status, payload);
        }

        private async Task<IActionResult> ForwardGet(string path, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.GetAsync(user, path, ct);
            return StatusCode(status, payload);
        }

        private static string Query(string? city, string? shift, string? location)
        {
            var q = new List<string>();
            if (!string.IsNullOrWhiteSpace(city)) q.Add($"cityValue={Uri.EscapeDataString(city)}");
            if (!string.IsNullOrWhiteSpace(shift)) q.Add($"shiftStatus={Uri.EscapeDataString(shift)}");
            if (!string.IsNullOrWhiteSpace(location)) q.Add($"locationStatus={Uri.EscapeDataString(location)}");
            return q.Count == 0 ? "" : "?" + string.Join("&", q);
        }

        private static string? Read(JsonElement body, params string[] names)
        {
            if (body.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in names)
            {
                foreach (var prop in body.EnumerateObject())
                {
                    if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        return prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.ToString();
                    }
                }
            }
            return null;
        }
    }
}
