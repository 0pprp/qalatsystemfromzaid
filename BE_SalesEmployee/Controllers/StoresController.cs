using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class StoresController : ControllerBase
    {
        private readonly BranchProxyService _proxy;

        public StoresController(BranchProxyService proxy)
        {
            _proxy = proxy;
        }

        [HttpGet("Items")]
        public async Task<IActionResult> Items([FromQuery] int? storeId, [FromQuery] string? itemName, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            using var storesResponse = await _proxy.SendAuthorizedAsync(
                user.CityLink,
                "Stores/Stores_GetAll/null",
                HttpMethod.Get,
                user.BranchToken,
                null,
                ct);
            var storesBody = await storesResponse.Content.ReadAsStringAsync(ct);
            var storesJson = storesResponse.IsSuccessStatusCode ? storesBody : "[]";

            object? items = null;
            if (storeId.HasValue && storeId.Value > 0)
            {
                var name = string.IsNullOrWhiteSpace(itemName) ? "null" : Uri.EscapeDataString(itemName);
                using var itemsResponse = await _proxy.SendAuthorizedAsync(
                    user.CityLink,
                    $"Items/Items_GetAll/{storeId}&&{name}&&{Uri.EscapeDataString("الجميع")}",
                    HttpMethod.Get,
                    user.BranchToken,
                    null,
                    ct);
                var itemsBody = await itemsResponse.Content.ReadAsStringAsync(ct);
                items = itemsResponse.IsSuccessStatusCode
                    ? BranchProxyService.TryParseJson(itemsBody)
                    : Array.Empty<object>();
            }

            using var delegatesResponse = await _proxy.SendAuthorizedAsync(
                user.CityLink,
                "Delegates/Delegates_GetAll/null",
                HttpMethod.Get,
                user.BranchToken,
                null,
                ct);
            var delegatesBody = await delegatesResponse.Content.ReadAsStringAsync(ct);

            return Ok(new
            {
                cityName = user.CityName,
                stores = BranchProxyService.TryParseJson(storesJson),
                items,
                delegates = delegatesResponse.IsSuccessStatusCode
                    ? BranchProxyService.TryParseJson(delegatesBody)
                    : null
            });
        }
    }
}
