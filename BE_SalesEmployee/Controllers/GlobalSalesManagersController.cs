using BE_SalesEmployee.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Public Global Sales Manager API for FE. Actor must be محاسب رئيسي (company JWT).
/// </summary>
[ApiController]
[Route("api/sales-management/global-managers")]
[Authorize(AuthenticationSchemes = "CompanyJwt", Policy = "Company.MainAccountant")]
public sealed class GlobalSalesManagersController : ControllerBase
{
    private readonly IGlobalSalesManagerOrchestrator _orchestrator;

    public GlobalSalesManagersController(IGlobalSalesManagerOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public sealed class CreateBody
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? UserImage { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBody body, CancellationToken ct)
    {
        var actorType = User.FindFirst("UserType")?.Value;
        if (!string.Equals(actorType, "محاسب رئيسي", StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "غير مصرح" });
        }

        int? actorId = null;
        if (int.TryParse(User.FindFirst("UserID")?.Value, out var id))
        {
            actorId = id;
        }

        var result = await _orchestrator.CreateAsync(new GlobalManagerClientRequest
        {
            UserName = body.UserName ?? "",
            Email = body.Email,
            Password = body.Password,
            PhoneNumber = body.PhoneNumber,
            Address = body.Address,
            UserImage = body.UserImage,
            ActorUserId = actorId
        }, ct);

        return Map(result);
    }

    [HttpPut("{globalAccountId:guid}")]
    public async Task<IActionResult> Update(Guid globalAccountId, [FromBody] CreateBody body, CancellationToken ct)
    {
        var result = await _orchestrator.UpdateAsync(globalAccountId, new GlobalManagerClientRequest
        {
            UserName = body.UserName ?? "",
            Email = body.Email,
            Password = body.Password,
            PhoneNumber = body.PhoneNumber,
            Address = body.Address,
            UserImage = body.UserImage
        }, ct);
        return Map(result);
    }

    [HttpPost("{globalAccountId:guid}/disable")]
    public async Task<IActionResult> Disable(Guid globalAccountId, CancellationToken ct)
    {
        var result = await _orchestrator.DisableAsync(globalAccountId, ct);
        return Map(result);
    }

    private static IActionResult Map(GlobalOrchestrationResult result)
    {
        var payload = new
        {
            status = result.Status.ToString(),
            globalAccountId = result.GlobalAccountId,
            operationId = result.OperationId,
            message = result.Message,
            succeededBranches = result.SucceededBranches.Select(b => new
            {
                b.CityValue,
                b.CityName,
                b.Database,
                b.Code,
                b.HttpStatus
            }),
            failedBranches = result.FailedBranches.Select(b => new
            {
                b.CityValue,
                b.CityName,
                b.Database,
                b.Code,
                b.HttpStatus
            })
        };

        return result.Status switch
        {
            GlobalOrchestrationStatus.Success => new OkObjectResult(payload),
            GlobalOrchestrationStatus.PartialFailure => new ObjectResult(payload) { StatusCode = StatusCodes.Status207MultiStatus },
            GlobalOrchestrationStatus.Conflict => new ConflictObjectResult(payload),
            GlobalOrchestrationStatus.BadRequest => new BadRequestObjectResult(payload),
            GlobalOrchestrationStatus.BranchUnavailable => new ObjectResult(payload) { StatusCode = StatusCodes.Status503ServiceUnavailable },
            _ => new ObjectResult(payload) { StatusCode = StatusCodes.Status500InternalServerError }
        };
    }
}
