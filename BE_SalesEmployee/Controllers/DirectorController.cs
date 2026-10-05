using BE_SalesEmployee.DelegatedManager;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Central director: province ratings + customer drill-down (reuses branch sales-manager APIs).</summary>
[Route("api/director")]
[ApiController]
[Authorize(Policy = SalesPolicies.Director)]
public sealed class DirectorController : ControllerBase
{
    private readonly ISalesManagerBranchAggregator _aggregator;

    public DirectorController(ISalesManagerBranchAggregator aggregator)
    {
        _aggregator = aggregator;
    }

    [HttpGet("cities")]
    public async Task<IActionResult> Cities(CancellationToken ct)
    {
        var branches = await _aggregator.BranchesAsync(ct);
        return Ok(branches.Select(b => new
        {
            value = b.Value,
            name = b.Name,
            database = b.Database
        }));
    }

    [HttpGet("stats/catalog")]
    public async Task<IActionResult> StatsCatalog([FromQuery] string? cityValue, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyCatalog,
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>
    /// New/zeroed/net month stats per province. Omit cityValue to fan-out all branches.
    /// </summary>
    [HttpGet("stats/branch-month")]
    public async Task<IActionResult> StatsBranchMonth([FromQuery] string? cityValue, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            var (statusAll, payloadAll) = await _aggregator.GetObjectFanoutAsync(
                actor,
                DelegatedManagerStatsPaths.CompanyBranchMonth,
                ct);
            return StatusCode(statusAll, payloadAll);
        }

        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyBranchMonth,
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("stats/trend")]
    public async Task<IActionResult> StatsTrend(
        [FromQuery] string? cityValue,
        [FromQuery] string? scope,
        [FromQuery] int id,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var today = DateTime.UtcNow.AddHours(3).Date;
        var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var to = (toDate ?? today).Date;
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyTrend(scope, id, from, to),
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>زبائن مستوى تقييم واحد (ممتاز/جيد/…) — page-capped for production.</summary>
    [HttpGet("stats/rating-customers")]
    public async Task<IActionResult> StatsRatingCustomers(
        [FromQuery] string? cityValue,
        [FromQuery] string? scope,
        [FromQuery] int id,
        [FromQuery] string? level,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int take = 500,
        [FromQuery] int skip = 0,
        [FromQuery] string? q = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        if (string.IsNullOrWhiteSpace(level))
        {
            return BadRequest(new { message = "يجب تحديد مستوى التقييم." });
        }

        var today = DateTime.UtcNow.AddHours(3).Date;
        var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var to = (toDate ?? today).Date;
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyRatingCustomers(scope, id, level, from, to, take, skip, q),
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("customers/{cityValue}/{customerId:int}/profile")]
    public async Task<IActionResult> CustomerProfileById(string cityValue, int customerId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/customers/{customerId}/profile",
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("customers/{cityValue}/{customerId:int}/notes")]
    public async Task<IActionResult> CustomerNotes(string cityValue, int customerId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/customers/{customerId}/notes",
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("sales/{cityValue}/{saleId:int}/shop-image")]
    public async Task<IActionResult> SaleShopImage(string cityValue, int saleId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetFileAsync(
            actor,
            cityValue,
            $"sales-manager/sales/{saleId}/shop-image",
            ct);
        if (payload is ValueTuple<byte[], string> file)
        {
            return File(file.Item1, string.IsNullOrWhiteSpace(file.Item2) ? "image/jpeg" : file.Item2);
        }

        return StatusCode(status, payload ?? new { message = "الصورة غير موجودة." });
    }

    [HttpGet("customer-documents/{cityValue}/{documentId:int}/file")]
    public async Task<IActionResult> CustomerDocumentFile(string cityValue, int documentId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetFileAsync(
            actor,
            cityValue,
            $"sales-manager/customer-documents/{documentId}/file",
            ct);
        if (payload is ValueTuple<byte[], string> file)
        {
            return File(file.Item1, string.IsNullOrWhiteSpace(file.Item2) ? "image/jpeg" : file.Item2);
        }

        return StatusCode(status, payload ?? new { message = "الملف غير موجود على السيرفر." });
    }
}
