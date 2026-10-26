using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

[ApiController]
[Route("api/sales/price-board")]
[Authorize(Policy = SalesPolicies.SalesEmployee)]
public sealed class SalesPriceBoardController : ControllerBase
{
    private readonly IPriceBoardGatewayClient _client;
    private readonly IConfiguration _configuration;
    private readonly SalesIdentityService _identity;

    public SalesPriceBoardController(
        IPriceBoardGatewayClient client,
        IConfiguration configuration,
        SalesIdentityService identity)
    {
        _client = client;
        _configuration = configuration;
        _identity = identity;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var city = ResolveCityValue();
        var (status, body) = await _client.ListByCityAsync(city, ct);
        return new ContentResult
        {
            StatusCode = status,
            Content = body ?? "[]",
            ContentType = "application/json; charset=utf-8",
        };
    }

    private string ResolveCityValue()
    {
        var identity = _identity.FromAuthenticatedUser();
        if (!string.IsNullOrWhiteSpace(identity?.BranchId))
            return identity.BranchId.Trim();

        var configured = _configuration["SalesManagement:BranchId"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        return string.Empty;
    }
}
