using System.Text.Json;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.DTO;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BE_SalesEmployee.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SalesController : ControllerBase
    {
        private readonly BranchProxyService _proxy;
        private readonly AdminCitiesService _cities;
        private readonly SalesDevelopmentGuard _guard;
        private readonly IGlobalCustomerSearchService _search;
        private readonly IHubContext<BE_SalesEmployee.Hubs.SalesTrackingHub> _hub;

        public SalesController(
            BranchProxyService proxy,
            AdminCitiesService cities,
            SalesDevelopmentGuard guard,
            IGlobalCustomerSearchService search,
            IHubContext<BE_SalesEmployee.Hubs.SalesTrackingHub> hub)
        {
            _proxy = proxy;
            _cities = cities;
            _guard = guard;
            _search = search;
            _hub = hub;
        }

        [Authorize(Policy = SalesPolicies.AnySales)]
        [HttpGet("me")]
        public async Task<IActionResult> Me(CancellationToken ct)
        {
            return await ProxyAssigned("sales/me", HttpMethod.Get, null, ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("customers/search")]
        public async Task<IActionResult> SearchCustomers([FromQuery] string? q, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var query = (q ?? string.Empty).Trim();
            if (query.Length < 2)
            {
                return BadRequest(new { message = "اكتب حرفين على الأقل للبحث" });
            }

            var user = TokenService.FromPrincipal(User);
            var result = await _search.SearchAsync(query, user, ct);
            if (result.Error != null)
            {
                return StatusCode(result.StatusCode, new { message = result.Error });
            }

            return StatusCode(result.StatusCode, result.Body);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("inventory")]
        public async Task<IActionResult> Inventory(CancellationToken ct)
        {
            return await ProxyAssigned("sales/inventory", HttpMethod.Get, null, ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost]
        public async Task<IActionResult> CreateDraft([FromBody] JsonElement body, CancellationToken ct)
        {
            return await ProxyAssigned("sales", HttpMethod.Post, new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json"), ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("pending")]
        public async Task<IActionResult> Pending(CancellationToken ct)
        {
            return await ProxyAssigned("sales/pending", HttpMethod.Get, null, ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            return await ProxyAssigned($"sales/{id}", HttpMethod.Get, null, ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("{id:int}/complete")]
        public async Task<IActionResult> Complete(int id, CancellationToken ct)
        {
            return await ProxyAssigned($"sales/{id}/complete", HttpMethod.Post, null, ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("{id:int}/documents")]
        public async Task<IActionResult> Documents(int id, CancellationToken ct)
        {
            return await ProxyAssigned($"sales/{id}/documents", HttpMethod.Get, null, ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("{id:int}/documents/{documentId:int}/download")]
        public async Task<IActionResult> Download(int id, int documentId, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var user = TokenService.FromPrincipal(User);
            using var response = await _proxy.SendAuthorizedAsync(
                user.CityLink,
                $"sales/{id}/documents/{documentId}/download",
                HttpMethod.Get,
                user.BranchToken,
                null,
                ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return StatusCode((int)response.StatusCode, string.IsNullOrWhiteSpace(error) ? null : BranchProxyService.TryParseJson(error));
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/pdf";
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                           ?? response.Content.Headers.ContentDisposition?.FileName
                           ?? $"Sale_{id}.pdf";
            return File(bytes, contentType, fileName.Trim('"'));
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("shifts/start")]
        public async Task<IActionResult> StartShift(CancellationToken ct)
        {
            return await ProxyAssigned("sales/shifts/start", HttpMethod.Post, null, ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("shifts/current")]
        public async Task<IActionResult> CurrentShift(CancellationToken ct)
        {
            return await ProxyAssigned("sales/shifts/current", HttpMethod.Get, null, ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("location/batch")]
        public async Task<IActionResult> LocationBatch([FromBody] JsonElement body, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var user = TokenService.FromPrincipal(User);
            using var response = await _proxy.SendAuthorizedAsync(
                user.CityLink,
                "sales/location/batch",
                HttpMethod.Post,
                user.BranchToken,
                new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json"),
                ct);
            var raw = await response.Content.ReadAsStringAsync(ct);
            var parsed = string.IsNullOrWhiteSpace(raw) ? (JsonElement?)null : BranchProxyService.TryParseJson(raw);
            if (response.IsSuccessStatusCode && parsed is JsonElement root)
            {
                JsonElement live = default;
                var hasLive = false;
                foreach (var prop in root.EnumerateObject())
                {
                    if (string.Equals(prop.Name, "liveUpdate", StringComparison.OrdinalIgnoreCase))
                    {
                        live = prop.Value.Clone();
                        hasLive = live.ValueKind == JsonValueKind.Object;
                        break;
                    }
                }

                if (hasLive)
                {
                    await _hub.Clients.Group(BE_SalesEmployee.Hubs.SalesTrackingHub.ManagersGroup)
                        .SendAsync(BE_SalesEmployee.Hubs.SalesTrackingHub.LocationUpdated, live, ct);
                }
            }

            return StatusCode((int)response.StatusCode, parsed);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("tracking/events")]
        public async Task<IActionResult> TrackingEvent([FromBody] JsonElement body, CancellationToken ct)
        {
            return await ProxyAssigned(
                "sales/tracking/events",
                HttpMethod.Post,
                new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json"),
                ct);
        }

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("requests")]
        public Task<IActionResult> Requests(CancellationToken ct) =>
            ProxyAssigned("sales/requests", HttpMethod.Get, null, ct);

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpGet("requests/{id:int}")]
        public Task<IActionResult> RequestDetails(int id, CancellationToken ct) =>
            ProxyAssigned($"sales/requests/{id}", HttpMethod.Get, null, ct);

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("requests/{id:int}/view")]
        public Task<IActionResult> ViewRequest(int id, CancellationToken ct) =>
            ProxyAssigned($"sales/requests/{id}/view", HttpMethod.Post, null, ct);

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("requests/{id:int}/start-processing")]
        public Task<IActionResult> StartProcessing(int id, CancellationToken ct) =>
            ProxyAssigned($"sales/requests/{id}/start-processing", HttpMethod.Post, null, ct);

        [Authorize(Policy = SalesPolicies.SalesEmployee)]
        [HttpPost("requests/{id:int}/reject")]
        public async Task<IActionResult> RejectRequest(int id, [FromBody] JsonElement body, CancellationToken ct) =>
            await ProxyAssigned(
                $"sales/requests/{id}/reject",
                HttpMethod.Post,
                new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json"),
                ct);

        [HttpPost("Create")]
        public async Task<IActionResult> Create([FromBody] JsonElement body, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var ratingLevel = ReadInt(body, "ratingLevel") ?? ReadInt(body, "RatingLevel");
            if (ratingLevel == 1)
            {
                return BadRequest(new { message = "لا يمكن إنشاء مبيع لتقييم مرفوض" });
            }

            using var saleResponse = await _proxy.SendAuthorizedAsync(
                user.CityLink,
                "CustomersSales/CustomersSales_Create",
                HttpMethod.Post,
                user.BranchToken,
                new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json"),
                ct);
            var saleBody = await saleResponse.Content.ReadAsStringAsync(ct);
            if (!saleResponse.IsSuccessStatusCode)
            {
                return StatusCode((int)saleResponse.StatusCode, saleBody);
            }

            object? rating = null;
            if (ratingLevel is >= 1 and <= 5)
            {
                var ratingPayload = new
                {
                    customerID = Read(body, "customerID", "CustomerID"),
                    customerName = Read(body, "customerName", "CustomerName"),
                    phoneNumber = Read(body, "phoneNumber", "PhoneNumber"),
                    ratingLevel,
                    notes = Read(body, "ratingNotes", "RatingNotes", "notes", "Notes"),
                    rejectionReason = Read(body, "rejectionReason", "RejectionReason")
                };
                using var ratingResponse = await _proxy.SendAuthorizedAsync(
                    user.CityLink,
                    "SalesEmployee/Ratings",
                    HttpMethod.Post,
                    user.BranchToken,
                    BranchProxyService.JsonContent(ratingPayload),
                    ct);
                if (ratingResponse.IsSuccessStatusCode)
                {
                    rating = BranchProxyService.TryParseJson(await ratingResponse.Content.ReadAsStringAsync(ct));
                }
            }

            return Ok(new
            {
                sale = BranchProxyService.TryParseJson(saleBody),
                rating,
                cityName = user.CityName
            });
        }

        private async Task<ActionResult?> BlockIfNotDemo(CancellationToken ct)
        {
            var cities = await _cities.GetCitiesAsync(ct);
            if (!_guard.CanRunSalesModule(cities, out var reason))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
            }

            return null;
        }

        private async Task<IActionResult> ProxyAssigned(string relativePath, HttpMethod method, HttpContent? content, CancellationToken ct)
        {
            var blocked = await BlockIfNotDemo(ct);
            if (blocked != null)
            {
                return blocked;
            }

            var user = TokenService.FromPrincipal(User);
            using var response = await _proxy.SendAuthorizedAsync(user.CityLink, relativePath, method, user.BranchToken, content, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return StatusCode((int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? null : BranchProxyService.TryParseJson(body));
        }

        private static string? Read(JsonElement body, params string[] names)
        {
            if (body.ValueKind != JsonValueKind.Object)
            {
                return null;
            }
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

        private static int? ReadInt(JsonElement body, string name)
        {
            var raw = Read(body, name);
            return int.TryParse(raw, out var value) ? value : null;
        }
    }
}
