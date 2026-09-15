using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Branch sales-manager side of the exception flow: raise a request and follow its own list.</summary>
[Authorize(Policy = SalesPolicies.SalesManager)]
[Route("api/sales-manager/exceptions")]
[ApiController]
public class SalesManagerExceptionsController : ControllerBase
{
    private readonly SalesExceptionService _exceptions;

    public SalesManagerExceptionsController(SalesExceptionService exceptions)
    {
        _exceptions = exceptions;
    }

    public sealed class CreateExceptionRequest
    {
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public int? SalesRequestId { get; set; }
        public string? Reason { get; set; }
        public string? TargetApproverType { get; set; }
    }

    public sealed class CancelExceptionRequest
    {
        public string? Note { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExceptionRequest request, CancellationToken ct)
    {
        var requester = TokenService.FromPrincipal(User);
        var result = await _exceptions.CreateAsync(requester, new CreateSalesExceptionInput
        {
            CityValue = request.CityValue,
            CityName = request.CityName,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            SalesRequestId = request.SalesRequestId,
            Reason = request.Reason,
            TargetApproverType = request.TargetApproverType
        }, ct);

        return result.Outcome switch
        {
            SalesExceptionOutcome.Success =>
                Ok(DelegatedManagerController.ToExceptionDetail(result.Request!)),
            SalesExceptionOutcome.Forbidden =>
                StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }

    [HttpGet]
    public async Task<IActionResult> Mine(
        [FromQuery] string? status,
        [FromQuery] string? cityValue,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var requester = TokenService.FromPrincipal(User);
        var (normalizedPage, normalizedSize) = PagingDefaults.Normalize(page, pageSize);
        var result = await _exceptions.ListAsync(new SalesExceptionQuery
        {
            Page = normalizedPage,
            PageSize = normalizedSize,
            Status = status,
            CityValue = cityValue,
            RequestingManagerUserName = requester.UserName
        }, ct);

        return Ok(new
        {
            page = result.Page,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages,
            items = result.Items.Select(DelegatedManagerController.ToExceptionSummary)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var requester = TokenService.FromPrincipal(User);
        var detail = await _exceptions.GetAsync(id, ct);
        if (detail is null)
        {
            return NotFound(new { message = "الطلب غير موجود" });
        }

        if (!string.Equals(detail.Request.RequestingManagerUserName, requester.UserName?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "يمكن عرض الطلبات الخاصة بك فقط" });
        }

        return Ok(new
        {
            request = DelegatedManagerController.ToExceptionDetail(detail.Request),
            audit = detail.Audit.Select(a => new
            {
                a.Id,
                a.ActorDisplayName,
                a.ActorRole,
                a.PreviousStatus,
                a.NewStatus,
                a.DecisionNote,
                a.CreatedAtUtc
            })
        });
    }

    [HttpPut("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelExceptionRequest? request, CancellationToken ct)
    {
        var requester = TokenService.FromPrincipal(User);
        var result = await _exceptions.DecideAsync(requester, id, "cancel", request?.Note, ct);
        return result.Outcome switch
        {
            SalesExceptionOutcome.Success => Ok(DelegatedManagerController.ToExceptionDetail(result.Request!)),
            SalesExceptionOutcome.NotFound => NotFound(new { message = result.Error }),
            SalesExceptionOutcome.Conflict => Conflict(new { message = result.Error }),
            SalesExceptionOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }
}
