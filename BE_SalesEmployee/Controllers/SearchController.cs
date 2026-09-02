using System.Text.Json;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SearchController : ControllerBase
    {
        private readonly AdminCitiesService _cities;
        private readonly BranchProxyService _proxy;

        public SearchController(AdminCitiesService cities, BranchProxyService proxy)
        {
            _cities = cities;
            _proxy = proxy;
        }

        [HttpGet("Customers")]
        public async Task<IActionResult> Customers([FromQuery] string? q, [FromQuery] string? city, CancellationToken ct)
        {
            var user = TokenService.FromPrincipal(User);
            var query = string.IsNullOrWhiteSpace(q) ? "null" : q.Trim();
            var cities = await _cities.GetCitiesAsync(ct);

            IEnumerable<AdminCity> targets;
            if (!string.IsNullOrWhiteSpace(city) && city != "all")
            {
                var match = cities.FirstOrDefault(c =>
                    string.Equals(c.Value, city, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(c.Name, city, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    return BadRequest(new { message = "المحافظة غير موجودة" });
                }
                targets = [match];
            }
            else
            {
                // Lab and production search: الكل includes النجف DEMO + المحلي
                targets = cities;
            }

            var targetList = targets.ToList();
            if (targetList.Count == 0)
            {
                return BadRequest(new { message = "لا توجد محافظة بيع مربوطة بهذا الحساب" });
            }

            var results = new List<object>();
            string? searchError = null;
            var tasks = targetList.Select(async target =>
            {
                var assigned = IsAssigned(target, user);
                try
                {
                    var rows = await _proxy.SearchCustomersAsync(target, query, user.BranchToken, user.CityLink, ct);
                    lock (results)
                    {
                        foreach (var item in rows)
                        {
                            results.Add(new
                            {
                                customerId = Read(item, "customerID", "CustomerID", "customerId"),
                                customerName = Read(item, "customerName", "CustomerName"),
                                phoneNumber = Read(item, "phoneNumber", "PhoneNumber"),
                                address = Read(item, "address", "Address"),
                                shopName = Read(item, "shopName", "ShopName"),
                                nearestFunctionPoint = Read(item, "nearestFunctionPoint", "NearestFunctionPoint"),
                                saleName = Read(item, "saleName", "SaleName"),
                                receiptName = Read(item, "receiptName", "ReceiptName"),
                                delegateId = Read(item, "delegateID", "DelegateID", "delegateId"),
                                delegateName = Read(item, "delegateName", "DelegateName"),
                                cityName = target.Name,
                                cityValue = target.Value,
                                fromAssignedCity = assigned
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    searchError ??= $"{target.Name}: {ex.Message}";
                }
            });

            await Task.WhenAll(tasks);
            if (results.Count == 0 && !string.IsNullOrWhiteSpace(searchError))
            {
                return StatusCode(502, new { message = searchError });
            }
            return Ok(results);
        }

        [HttpGet("Cities")]
        public async Task<IActionResult> Cities(CancellationToken ct)
        {
            var cities = await _cities.GetCitiesAsync(ct);
            var user = TokenService.FromPrincipal(User);
            var list = new List<object> { new { value = "all", name = "الكل" } };
            list.AddRange(cities.Select(c => new
            {
                value = c.Value,
                name = c.Name,
                assigned = IsAssigned(c, user)
            }));
            return Ok(list);
        }

        private static bool IsAssigned(AdminCity city, GatewayUser user) =>
            string.Equals(city.Value, user.CityValue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(city.Link, user.CityLink, StringComparison.OrdinalIgnoreCase);

        private static string? Read(JsonElement item, params string[] names)
        {
            foreach (var name in names)
            {
                foreach (var prop in item.EnumerateObject())
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
