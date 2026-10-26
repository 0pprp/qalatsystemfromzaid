using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

/// <summary>Branch APIs for director «عمل المتابعين» (proxied via gateway).</summary>
[Authorize(Policy = SalesPolicies.SalesManager)]
[Route("api/sales-manager/director-follower-ops")]
[ApiController]
public sealed class DirectorFollowerOpsController : ControllerBase
{
    private readonly IDirectorFollowerOpsService _ops;
    private readonly SalesDevelopmentGuard _guard;

    public DirectorFollowerOpsController(IDirectorFollowerOpsService ops, SalesDevelopmentGuard guard)
    {
        _ops = ops;
        _guard = guard;
    }

    [HttpGet("summaries")]
    public async Task<IActionResult> Summaries(CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetSummariesAsync(ct));
    }

    [HttpGet("follower-cards")]
    public async Task<IActionResult> FollowerCards(CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetFollowerCardsAsync(ct));
    }

    [HttpGet("followers/{followerId:int}/work-bundle")]
    public async Task<IActionResult> WorkBundle(int followerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetWorkBundleAsync(followerId, ct));
    }

    [HttpGet("followers/{followerId:int}/sales-workspace")]
    public async Task<IActionResult> SalesWorkspace(int followerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetSalesWorkspaceAsync(followerId, ct));
    }

    [HttpGet("followers/{followerId:int}/remaining-customers")]
    public async Task<IActionResult> RemainingCustomers(
        int followerId,
        [FromQuery] string? bucket,
        CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetRemainingCustomersAsync(followerId, bucket ?? "rated", ct));
    }

    [HttpGet("followers/{followerId:int}/sales-auth")]
    public async Task<IActionResult> SalesAuth(int followerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetSalesAuthAsync(followerId, ct));
    }

    [HttpGet("followers/{followerId:int}/declining")]
    public async Task<IActionResult> Declining(int followerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetDecliningAsync(followerId, ct));
    }

    [HttpGet("followers/{followerId:int}/notes")]
    public async Task<IActionResult> Notes(int followerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetNotesAsync(followerId, ct));
    }

    [HttpGet("followers/{followerId:int}/field-followups")]
    public async Task<IActionResult> FieldFollowups(int followerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetFieldFollowupsAsync(followerId, ct));
    }

    /// <summary>الزبائن القديمة — قضايا قانونية مرسلة من مدير المبيعات لهذا المتابع.</summary>
    [HttpGet("followers/{followerId:int}/legal-followups")]
    public async Task<IActionResult> LegalFollowups(int followerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetLegalFollowupsAsync(followerId, ct));
    }

    [HttpGet("followers/{followerId:int}/delegate-notes")]
    public async Task<IActionResult> DelegateNotes(int followerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetDelegateNotesAsync(followerId, ct));
    }

    [HttpGet("followers/{followerId:int}/collection")]
    public async Task<IActionResult> Collection(int followerId, CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        return Ok(await _ops.GetCollectionAsync(followerId, ct));
    }

    [HttpPut("lists/{listId:int}/collection-enabled")]
    public async Task<IActionResult> SetCollectionEnabled(
        int listId,
        [FromBody] CollectionEnabledBody? body,
        CancellationToken ct)
    {
        var gate = Gate();
        if (gate != null)
        {
            return gate;
        }

        if (listId <= 0 || body is null)
        {
            return BadRequest(new { message = "معرّف القائمة والحالة مطلوبان" });
        }

        int? userId = null;
        if (HttpContext.Items["UserID"] is string s && int.TryParse(s, out var parsed))
        {
            userId = parsed;
        }

        var ok = await _ops.SetCollectionEnabledAsync(listId, body.Enabled, userId, ct);
        if (!ok)
        {
            return BadRequest(new { message = "تعذر تحديث حالة الجباية" });
        }

        return Ok(new { listId, enabled = body.Enabled });
    }

    private IActionResult? Gate()
    {
        if (!_guard.CanRunSalesModule(out var reason))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
        }

        return null;
    }

    public sealed class CollectionEnabledBody
    {
        public bool Enabled { get; set; }
    }
}
