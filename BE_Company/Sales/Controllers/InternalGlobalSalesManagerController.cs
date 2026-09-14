using BE_Company.Sales.Authorization;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers;

/// <summary>
/// Service-to-service Global Sales Manager ops. Requires X-Sales-Gateway-Key only.
/// Forces UserType=مدير مبيعات. Never returns passwords. Never trusts client ActorUserId.
/// </summary>
[ApiController]
[Route("api/internal/sales-users/global-manager")]
[Authorize(AuthenticationSchemes = SalesGatewayKeyHandler.SchemeName)]
public sealed class InternalGlobalSalesManagerController : ControllerBase
{
    private readonly IGlobalSalesManagerRepository _repo;
    private readonly SalesDevelopmentGuard _guard;
    private readonly ILogger<InternalGlobalSalesManagerController> _logger;

    public InternalGlobalSalesManagerController(
        IGlobalSalesManagerRepository repo,
        SalesDevelopmentGuard guard,
        ILogger<InternalGlobalSalesManagerController> logger)
    {
        _repo = repo;
        _guard = guard;
        _logger = logger;
    }

    public sealed class PreflightBody
    {
        public string? UserName { get; set; }
        public Guid? GlobalAccountId { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
    }

    public sealed class UpsertBody
    {
        public Guid GlobalAccountId { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? UserImage { get; set; }
        /// <summary>Ignored — never trusted from the wire.</summary>
        public int? ActorUserId { get; set; }
    }

    [HttpPost("preflight")]
    public async Task<IActionResult> Preflight([FromBody] PreflightBody body, CancellationToken ct)
    {
        if (!TryAllowSalesModule(out var blocked))
        {
            return blocked!;
        }

        var result = await _repo.PreflightAsync(new GlobalManagerIdentityRequest
        {
            UserName = body.UserName,
            GlobalAccountId = body.GlobalAccountId,
            Email = body.Email,
            PhoneNumber = body.PhoneNumber,
            Password = body.Password
        }, ct);

        return Ok(new
        {
            status = result.Status.ToString(),
            // Success paths may include ids for orchestration; conflict/legacy paths stay coarse.
            userId = result.Status is GlobalManagerPreflightStatus.LegacyAdoptable
                or GlobalManagerPreflightStatus.ExistingSameGlobalAccount
                or GlobalManagerPreflightStatus.NotFound
                ? result.UserId
                : null,
            globalAccountId = result.Status is GlobalManagerPreflightStatus.ExistingSameGlobalAccount
                ? result.GlobalAccountId
                : null,
            userName = result.Status is GlobalManagerPreflightStatus.LegacyAdoptable
                or GlobalManagerPreflightStatus.ExistingSameGlobalAccount
                ? result.UserName
                : null,
            userStateActive = result.Status is GlobalManagerPreflightStatus.LegacyAdoptable
                or GlobalManagerPreflightStatus.ExistingSameGlobalAccount
                ? result.UserStateActive
                : null
        });
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertBody body, CancellationToken ct)
    {
        if (!TryAllowSalesModule(out var blocked))
        {
            return blocked!;
        }

        var result = await _repo.UpsertAsync(new GlobalManagerWriteRequest
        {
            GlobalAccountId = body.GlobalAccountId,
            UserName = body.UserName ?? "",
            Email = body.Email,
            Password = body.Password,
            PhoneNumber = body.PhoneNumber,
            Address = body.Address,
            UserImage = body.UserImage,
            ActorUserId = null
        }, ct);

        _logger.LogInformation(
            "GlobalSalesManager upsert code={Code} ok={Ok} global={Global} userId={UserId}",
            result.Code,
            result.Ok,
            result.GlobalAccountId,
            result.UserId);

        if (!result.Ok)
        {
            var status = result.Code is "USERNAMECONFLICT" or "DUPLICATEAMBIGUOUS" or "USERNAME_CONFLICT"
                or "LEGACY_CONFLICT" or "LEGACYCONFLICT"
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return StatusCode(status, new
            {
                ok = false,
                code = result.Code,
                message = result.Message
            });
        }

        return Ok(new
        {
            ok = true,
            code = result.Code,
            userId = result.UserId,
            globalAccountId = result.GlobalAccountId,
            userName = result.UserName,
            userType = GlobalSalesManagerRules.ForcedUserType
        });
    }

    [HttpPut("{globalAccountId:guid}")]
    public Task<IActionResult> Update(Guid globalAccountId, [FromBody] UpsertBody body, CancellationToken ct)
    {
        body.GlobalAccountId = globalAccountId;
        return Upsert(body, ct);
    }

    [HttpPost("{globalAccountId:guid}/disable")]
    public async Task<IActionResult> Disable(Guid globalAccountId, CancellationToken ct)
    {
        if (!TryAllowSalesModule(out var blocked))
        {
            return blocked!;
        }

        var result = await _repo.DisableAsync(globalAccountId, ct);
        _logger.LogInformation(
            "GlobalSalesManager disable code={Code} ok={Ok} global={Global}",
            result.Code,
            result.Ok,
            globalAccountId);

        if (!result.Ok)
        {
            return NotFound(new { ok = false, code = result.Code, message = result.Message });
        }

        return Ok(new { ok = true, code = result.Code, globalAccountId });
    }

    private bool TryAllowSalesModule(out IActionResult? blocked)
    {
        if (_guard.CanRunSalesModule(out var reason))
        {
            blocked = null;
            return true;
        }

        blocked = StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
        return false;
    }
}
