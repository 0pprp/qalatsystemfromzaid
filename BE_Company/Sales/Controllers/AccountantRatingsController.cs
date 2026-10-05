using BE_Company.Sales.Authorization;
using BE_Company.Sales.Rating;
using BE_Company.Sales.Services;
using BE_Company.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

/// <summary>
/// Main accountant ratings — same BranchManagerStats as director/SM,
/// without sales-employee scope (موظف مبيعات).
/// </summary>
[ApiController]
[Route("api/accountant")]
[Authorize(Policy = SalesPolicies.MainAccountant)]
public sealed class AccountantRatingsController : ControllerBase
{
    private readonly SalesDevelopmentGuard _guard;
    private readonly IBranchManagerStatsService _stats;
    private readonly IBranchMonthStatsService _monthStats;
    private readonly ISalesShopProfileService _shops;
    private readonly ISalesCustomerDocumentService _customerDocs;
    private readonly ISharedCustomerNotesService _sharedNotes;
    private readonly IRatingEquationService _ratingEquation;

    public AccountantRatingsController(
        SalesDevelopmentGuard guard,
        IBranchManagerStatsService stats,
        IBranchMonthStatsService monthStats,
        ISalesShopProfileService shops,
        ISalesCustomerDocumentService customerDocs,
        ISharedCustomerNotesService sharedNotes,
        IRatingEquationService ratingEquation)
    {
        _guard = guard;
        _stats = stats;
        _monthStats = monthStats;
        _shops = shops;
        _customerDocs = customerDocs;
        _sharedNotes = sharedNotes;
        _ratingEquation = ratingEquation;
    }

    private async Task<IActionResult?> GateAsync(CancellationToken ct)
    {
        var check = await _guard.CanRunSalesModuleAsync(ct);
        if (!check.Ok)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = check.Reason });
        }

        return null;
    }

    private static bool IsEmployeeScope(string? scope) =>
        string.Equals(scope?.Trim(), "employee", StringComparison.OrdinalIgnoreCase);

    /// <summary>Catalog without موظف مبيعات options.</summary>
    [HttpGet("stats/catalog")]
    public async Task<IActionResult> StatsCatalog(CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;

        var catalog = await _stats.GetCatalogAsync(ct);
        catalog.Employees = Array.Empty<BranchManagerEntityOptionDto>();
        return Ok(catalog);
    }

    [HttpGet("stats/branch-month")]
    public async Task<IActionResult> StatsBranchMonth(CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        return Ok(await _monthStats.GetAsync(ct));
    }

    [HttpGet("stats/trend")]
    public async Task<IActionResult> StatsTrend(
        [FromQuery] string? scope,
        [FromQuery] int id,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool fullHistory = false,
        CancellationToken ct = default)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        if (IsEmployeeScope(scope))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "تقييم موظفي المبيعات غير متاح للمحاسب الرئيسي",
            });
        }

        var today = DateTime.UtcNow.AddHours(3).Date;
        var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var to = (toDate ?? today).Date;
        return Ok(await _stats.GetTrendAsync(scope ?? "branch", id, from, to, ct, fullHistory));
    }

    [HttpGet("stats/rating-customers")]
    public async Task<IActionResult> StatsRatingCustomers(
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
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        if (IsEmployeeScope(scope))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "تقييم موظفي المبيعات غير متاح للمحاسب الرئيسي",
            });
        }

        var today = DateTime.UtcNow.AddHours(3).Date;
        var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var to = (toDate ?? today).Date;
        return Ok(await _stats.GetRatingCustomersAsync(
            scope ?? "branch", id, level, from, to, ct, take, skip, q));
    }

    [HttpGet("customers/{customerId:int}/profile")]
    public async Task<IActionResult> CustomerProfile(int customerId, CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        if (customerId <= 0)
            return BadRequest(new { message = "حدد الزبون." });

        var profileTask = _shops.GetCustomerProfileAsync(customerId, null, null, ct);
        var docsTask = _customerDocs.ListForCustomerAsync(customerId, null, null, true, ct);
        await Task.WhenAll(profileTask, docsTask);
        var profile = await profileTask;
        profile.CustomerDocuments = await docsTask;
        return Ok(profile);
    }

    [HttpGet("customers/{customerId:int}/notes")]
    public async Task<IActionResult> CustomerNotes(int customerId, CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        return Ok(await _sharedNotes.ListAsync(customerId, ct));
    }

    [HttpGet("customers/{customerId:int}/rating-equation")]
    public async Task<IActionResult> GetRatingEquation(int customerId, CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        if (customerId <= 0)
            return BadRequest(new { message = "حدد الزبون." });

        var row = await _ratingEquation.GetAsync(customerId, ct);
        if (row is null)
            return NotFound(new { message = "الزبون غير موجود." });
        return Ok(row);
    }

    public sealed class RatingEquationBody
    {
        public double Factor { get; set; } = 1;
    }

    /// <summary>
    /// Sets virtual payment multiplier for rating only (ledger unchanged). Default 1.
    /// </summary>
    [HttpPut("customers/{customerId:int}/rating-equation")]
    public async Task<IActionResult> SetRatingEquation(
        int customerId,
        [FromBody] RatingEquationBody? body,
        CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        if (customerId <= 0)
            return BadRequest(new { message = "حدد الزبون." });

        var factor = body?.Factor ?? 1;
        if (factor <= 0)
            return BadRequest(new { message = "معادلة التقييم يجب أن تكون أكبر من صفر." });
        if (factor < RatingEquationService.MinFactor || factor > RatingEquationService.MaxFactor)
        {
            return BadRequest(new
            {
                message = $"معادلة التقييم بين {RatingEquationService.MinFactor} و {RatingEquationService.MaxFactor}.",
            });
        }

        int? userId = null;
        if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out var parsed))
            userId = parsed;

        var row = await _ratingEquation.SetAsync(customerId, factor, userId, ct);
        if (row is null)
            return NotFound(new { message = "الزبون غير موجود." });
        return Ok(row);
    }

    [HttpGet("sales/{saleId:int}/shop-image")]
    public async Task<IActionResult> SaleShopImage(int saleId, CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        var image = await _shops.ReadImageAsync(saleId, ct);
        if (image == null)
            return NotFound(new { message = "الصورة غير موجودة." });

        var ext = Path.GetExtension(image.Value.FileName).ToLowerInvariant();
        var type = ext == ".png" ? "image/png" : "image/jpeg";
        return File(image.Value.Bytes, type, image.Value.FileName);
    }

    [HttpGet("customer-documents/{documentId:int}/file")]
    public async Task<IActionResult> CustomerDocumentFile(int documentId, CancellationToken ct)
    {
        var gate = await GateAsync(ct);
        if (gate != null) return gate;
        var image = await _customerDocs.ReadFileAsync(documentId, ct);
        if (image == null)
            return NotFound(new { message = "الملف غير موجود على السيرفر." });

        var type = string.IsNullOrWhiteSpace(image.Value.ContentType)
                   || image.Value.ContentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase)
            ? GuessImageContentType(image.Value.FileName)
            : image.Value.ContentType;
        return File(image.Value.Bytes, type, image.Value.FileName);
    }

    private static string GuessImageContentType(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg",
        };
    }
}
