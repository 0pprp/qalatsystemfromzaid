using System.Text.Json;
using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>Branch sales-manager side of the exception flow: raise a request and follow its own list.</summary>
[Authorize(Policy = SalesPolicies.SalesManagerOrDirector)]
[DenyDirectorWrites]
[Route("api/sales-manager/exceptions")]
[ApiController]
public class SalesManagerExceptionsController : ControllerBase
{
    private readonly SalesExceptionService _exceptions;
    private readonly ISalesManagerBranchAggregator _aggregator;

    public SalesManagerExceptionsController(
        SalesExceptionService exceptions,
        ISalesManagerBranchAggregator aggregator)
    {
        _exceptions = exceptions;
        _aggregator = aggregator;
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

    public sealed class AssignExceptionRequest
    {
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
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
            SalesExceptionOutcome.Conflict =>
                Conflict(new
                {
                    message = result.Error,
                    existingExceptionId = result.Request?.Id,
                    status = result.Request?.Status,
                    salesRequestId = result.Request?.SalesRequestId
                }),
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

    /// <summary>
    /// Central sales-manager assign: proxy to the owning branch BE_Company assign endpoint.
    /// Without this route the gateway returns 404 and the UI shows «تعذر إسناد الاستثناء».
    /// </summary>
    [HttpPost("{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignExceptionRequest? body, CancellationToken ct)
    {
        var requester = TokenService.FromPrincipal(User);
        body ??= new AssignExceptionRequest();
        if (body.EmployeeId <= 0)
        {
            return BadRequest(new { message = "يجب اختيار موظف المبيعات" });
        }

        var detail = await _exceptions.GetAsync(id, ct);
        if (detail is null)
        {
            return NotFound(new { message = "طلب الاستثناء غير موجود" });
        }

        var row = detail.Request;
        if (!string.Equals(row.RequestingManagerUserName, requester.UserName?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "يمكن إسناد الطلبات الخاصة بك فقط" });
        }

        if (!string.Equals(row.Status, ExceptionStatuses.Approved, StringComparison.OrdinalIgnoreCase)
            || row.AssignmentConsumed)
        {
            return Conflict(new { message = "لا يمكن إسناد هذا الاستثناء" });
        }

        var city = (row.CityValue ?? "").Trim();
        if (string.IsNullOrWhiteSpace(city))
        {
            return BadRequest(new { message = "محافظة الاستثناء غير معروفة" });
        }

        if (row.SalesRequestId is null or <= 0)
        {
            return BadRequest(new { message = "طلب المبيع المرتبط بالاستثناء غير موجود" });
        }

        var payloadJson = JsonSerializer.Serialize(new
        {
            employeeId = body.EmployeeId,
            employeeName = body.EmployeeName
        });

        var (status, payload) = await _aggregator.PostAsync(
            requester,
            city,
            $"sales-manager/exceptions/{id:D}/assign",
            payloadJson,
            ct);

        return StatusCode(status, payload);
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
