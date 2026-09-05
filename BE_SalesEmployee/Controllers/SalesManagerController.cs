using System.Text.Json;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        [HttpGet("branches")]
        public async Task<IActionResult> Branches(CancellationToken ct)
        {
            var rows = await _aggregator.BranchesAsync(ct);
            return Ok(rows.Select(c => new
            {
                value = c.Value,
                name = c.Name,
                database = c.Database
            }));
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard([FromQuery] string? cityValue, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.DashboardAsync(user, cityValue, ct);
            return StatusCode(status, payload);
        }

        [HttpGet("employees")]
        public Task<IActionResult> Employees(
            [FromQuery] string? cityValue,
            [FromQuery] string? shiftStatus,
            [FromQuery] string? locationStatus,
            CancellationToken ct)
        {
            var q = Query(("cityValue", null), ("shiftStatus", shiftStatus), ("locationStatus", locationStatus));
            return ListAsync(cityValue, "sales-manager/employees" + q, ct);
        }

        [HttpGet("employees/{cityValue}/{employeeId:int}")]
        public Task<IActionResult> Employee(string cityValue, int employeeId, CancellationToken ct) =>
            OneAsync(cityValue, $"sales-manager/employees/{employeeId}", ct);

        [HttpGet("employees/{cityValue}/{employeeId:int}/route")]
        public Task<IActionResult> Route(string cityValue, int employeeId, [FromQuery] DateTime? date, CancellationToken ct) =>
            OneAsync(cityValue, $"sales-manager/employees/{employeeId}/route{(date == null ? "" : $"?date={date:yyyy-MM-dd}")}", ct);

        [HttpGet("employees/{cityValue}/{employeeId:int}/tracking-events")]
        public Task<IActionResult> Events(string cityValue, int employeeId, [FromQuery] DateTime? date, CancellationToken ct) =>
            OneAsync(cityValue, $"sales-manager/employees/{employeeId}/tracking-events{(date == null ? "" : $"?date={date:yyyy-MM-dd}")}", ct);

        [HttpGet("sales")]
        public Task<IActionResult> Sales(
            [FromQuery] string? cityValue,
            [FromQuery] int? employeeId,
            [FromQuery] string? status,
            [FromQuery] DateTime? date,
            CancellationToken ct)
        {
            var q = Query(
                ("employeeId", employeeId?.ToString()),
                ("status", status),
                ("date", date?.ToString("yyyy-MM-dd")));
            return ListAsync(cityValue, "sales-manager/sales" + q, ct);
        }

        [HttpGet("sales/{cityValue}/{saleId:int}")]
        public Task<IActionResult> Sale(string cityValue, int saleId, CancellationToken ct) =>
            OneAsync(cityValue, $"sales-manager/sales/{saleId}", ct);

        [HttpGet("sales/{cityValue}/{saleId:int}/documents")]
        public Task<IActionResult> SaleDocuments(string cityValue, int saleId, CancellationToken ct) =>
            OneAsync(cityValue, $"sales-manager/sales/{saleId}/documents", ct);

        [HttpGet("sales/{cityValue}/{saleId:int}/documents/{documentId:int}/download")]
        public async Task<IActionResult> SaleDocumentDownload(
            string cityValue, int saleId, int documentId, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.GetFileAsync(
                user, cityValue, $"sales-manager/sales/{saleId}/documents/{documentId}/download", ct);
            if (payload is ValueTuple<byte[], string> file)
            {
                var type = string.IsNullOrWhiteSpace(file.Item2) || file.Item2 == "image/jpeg"
                    ? "application/pdf"
                    : file.Item2;
                return File(file.Item1, type);
            }

            return StatusCode(status, payload);
        }

        [HttpGet("customers/{cityValue}/profile")]
        public Task<IActionResult> CustomerProfile(
            string cityValue,
            [FromQuery] int? customerId,
            [FromQuery] string? name,
            [FromQuery] string? phone,
            CancellationToken ct)
        {
            var q = Query(
                ("customerId", customerId?.ToString()),
                ("name", name),
                ("phone", phone));
            return OneAsync(cityValue, "sales-manager/customers/profile" + q, ct);
        }

        [HttpPost("customers/{cityValue}/notes")]
        public async Task<IActionResult> AddCustomerNote(string cityValue, [FromBody] JsonElement body, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.PostAsync(
                user, cityValue, "sales-manager/customers/notes", body.GetRawText(), ct);
            return StatusCode(status, payload);
        }

        [HttpGet("sales/{cityValue}/{saleId:int}/shop-image")]
        public async Task<IActionResult> ShopImage(string cityValue, int saleId, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.GetFileAsync(
                user, cityValue, $"sales-manager/sales/{saleId}/shop-image", ct);
            if (payload is ValueTuple<byte[], string> file)
            {
                return File(file.Item1, file.Item2);
            }

            return StatusCode(status, payload);
        }

        [HttpGet("customers/search")]
        public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] string? cityValue, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.SearchCustomersAsync(user, q, cityValue, ct);
            return StatusCode(status, payload);
        }

        [HttpPost("sales-requests/import")]
        public async Task<IActionResult> Import([FromBody] JsonElement body, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var city = Read(body, "cityValue", "CityValue");
            if (string.IsNullOrWhiteSpace(city))
            {
                return BadRequest(new { message = "يجب تحديد المحافظة المستهدفة للطلب." });
            }

            var (status, payload) = await _aggregator.PostAsync(
                user, city, "sales-manager/sales-requests/import", body.GetRawText(), ct);
            return StatusCode(status, payload);
        }

        [HttpGet("sales-requests")]
        public Task<IActionResult> Requests(
            [FromQuery] string? cityValue,
            [FromQuery] int? employeeId,
            [FromQuery] string? status,
            [FromQuery] DateTime? date,
            CancellationToken ct)
        {
            var q = Query(
                ("employeeId", employeeId?.ToString()),
                ("status", status),
                ("date", date?.ToString("yyyy-MM-dd")));
            return ListAsync(cityValue, "sales-manager/sales-requests" + q, ct);
        }

        [HttpGet("sales-requests/{cityValue}/{id:int}")]
        public Task<IActionResult> RequestDetails(string cityValue, int id, CancellationToken ct) =>
            OneAsync(cityValue, $"sales-manager/sales-requests/{id}", ct);

        [HttpPost("sales-requests")]
        public async Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var city = Read(body, "cityValue", "CityValue");
            if (string.IsNullOrWhiteSpace(city))
            {
                return BadRequest(new { message = "يجب تحديد المحافظة المستهدفة للطلب." });
            }

            var (status, payload) = await _aggregator.PostAsync(
                user, city, "sales-manager/sales-requests", body.GetRawText(), ct);
            return StatusCode(status, payload);
        }

        [HttpPost("sales-requests/{cityValue}/{id:int}/assign")]
        public async Task<IActionResult> Assign(string cityValue, int id, [FromBody] JsonElement body, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.PostAsync(
                user, cityValue, $"sales-manager/sales-requests/{id}/assign", body.GetRawText(), ct);
            return StatusCode(status, payload);
        }

        [HttpPost("sales-requests/{cityValue}/{id:int}/return")]
        public async Task<IActionResult> Return(string cityValue, int id, [FromBody] JsonElement body, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.PostAsync(
                user, cityValue, $"sales-manager/sales-requests/{id}/return", body.GetRawText(), ct);
            return StatusCode(status, payload);
        }

        private async Task<IActionResult> ListAsync(string? cityValue, string companyPath, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.GetAsync(user, cityValue, companyPath, ct);
            return StatusCode(status, payload);
        }

        private async Task<IActionResult> OneAsync(string cityValue, string companyPath, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var (status, payload) = await _aggregator.GetOneAsync(user, cityValue, companyPath, ct);
            return StatusCode(status, payload);
        }

        private static string Query(params (string Name, string? Value)[] parts)
        {
            var q = parts
                .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                .Select(p => $"{p.Name}={Uri.EscapeDataString(p.Value!)}")
                .ToList();
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
