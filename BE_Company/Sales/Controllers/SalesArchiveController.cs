using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

[Authorize(Policy = SalesPolicies.SalesEmployee)]
[Route("api/sales/archive")]
[ApiController]
public sealed class SalesArchiveController : ControllerBase
{
    private readonly SalesDevelopmentGuard _guard;
    private readonly SalesIdentityService _identity;
    private readonly ISalesArchiveService _archive;

    public SalesArchiveController(
        SalesDevelopmentGuard guard,
        SalesIdentityService identity,
        ISalesArchiveService archive)
    {
        _guard = guard;
        _identity = identity;
        _archive = archive;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var blocked = await GateAsync(ct);
        if (blocked != null)
        {
            return blocked;
        }

        var identity = _identity.FromAuthenticatedUser();
        if (identity == null)
        {
            return Unauthorized();
        }

        try
        {
            var bundles = await _archive.ListForEmployeeAsync(identity.EmployeeId, ct);
            return Ok(new { bundles });
        }
        catch (SalesArchiveException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("{bundleId:int}")]
    public async Task<IActionResult> Get(int bundleId, CancellationToken ct)
    {
        var blocked = await GateAsync(ct);
        if (blocked != null)
        {
            return blocked;
        }

        var identity = _identity.FromAuthenticatedUser();
        if (identity == null)
        {
            return Unauthorized();
        }

        try
        {
            var bundle = await _archive.GetForEmployeeAsync(identity.EmployeeId, bundleId, ct);
            if (bundle == null)
            {
                return NotFound(new { message = "الحزمة غير موجودة." });
            }

            return Ok(bundle);
        }
        catch (SalesArchiveException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost("{bundleId:int}/send")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(64_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 64_000_000)]
    public async Task<IActionResult> Send(
        int bundleId,
        [FromForm] string? recipientName,
        [FromForm] string? intermediateName,
        [FromForm] string? intermediatePhone,
        [FromForm] string? note,
        CancellationToken ct)
    {
        var blocked = await GateAsync(ct);
        if (blocked != null)
        {
            return blocked;
        }

        var identity = _identity.FromAuthenticatedUser();
        if (identity == null)
        {
            return Unauthorized();
        }

        var files = Request.Form.Files.Where(f => f.Length > 0).ToList();
        try
        {
            var bundle = await _archive.SendAsync(
                identity.EmployeeId,
                bundleId,
                recipientName,
                intermediateName,
                intermediatePhone,
                note,
                files,
                ct);
            return Ok(bundle);
        }
        catch (SalesArchiveException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("{bundleId:int}/photo")]
    public async Task<IActionResult> Photo(int bundleId, CancellationToken ct)
    {
        var blocked = await GateAsync(ct);
        if (blocked != null)
        {
            return blocked;
        }

        var identity = _identity.FromAuthenticatedUser();
        if (identity == null)
        {
            return Unauthorized();
        }

        try
        {
            var photo = await _archive.ReadPhotoForEmployeeAsync(identity.EmployeeId, bundleId, ct);
            if (photo == null)
            {
                return NotFound(new { message = "لا توجد صورة تسليم." });
            }

            return File(photo.Value.Bytes, photo.Value.ContentType);
        }
        catch (SalesArchiveException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpGet("{bundleId:int}/photos/{photoId:int}")]
    public async Task<IActionResult> PhotoById(int bundleId, int photoId, CancellationToken ct)
    {
        var blocked = await GateAsync(ct);
        if (blocked != null)
        {
            return blocked;
        }

        var identity = _identity.FromAuthenticatedUser();
        if (identity == null)
        {
            return Unauthorized();
        }

        try
        {
            var photo = await _archive.ReadPhotoForEmployeeAsync(identity.EmployeeId, bundleId, photoId, ct);
            if (photo == null)
            {
                return NotFound(new { message = "لا توجد صورة تسليم." });
            }

            return File(photo.Value.Bytes, photo.Value.ContentType);
        }
        catch (SalesArchiveException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
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
}
