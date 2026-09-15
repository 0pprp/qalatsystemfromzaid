using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Server-to-server sales-exception intake for branch backends that authenticate managers locally
/// and have no Sales Gateway JWT. Callers prove themselves with the shared internal key and assert
/// an already-authenticated Sales Manager; browsers never reach this route.
/// </summary>
[Route("api/internal/sales-exceptions")]
[ApiController]
public sealed class InternalSalesExceptionsController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly SalesExceptionService _exceptions;
    private readonly IConfiguration _configuration;

    public InternalSalesExceptionsController(SalesExceptionService exceptions, IConfiguration configuration)
    {
        _exceptions = exceptions;
        _configuration = configuration;
    }

    public sealed class TrustedExceptionRequest
    {
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public int? SalesRequestId { get; set; }
        public string? Reason { get; set; }
        public string? TargetApproverType { get; set; }
        public string? RequesterUserId { get; set; }
        public string? RequesterUserName { get; set; }
        public string? RequesterDisplayName { get; set; }
        public string? Role { get; set; }
    }

    /// <summary>Fails closed: an unset <c>InternalApiKey</c> rejects every caller.</summary>
    public static bool HasValidGatewayKey(HttpRequest request, IConfiguration configuration)
    {
        if (!request.Headers.TryGetValue(GatewayKeyHeader, out var provided) ||
            string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        var expected = configuration["InternalApiKey"];
        return !string.IsNullOrWhiteSpace(expected)
               && string.Equals(provided.ToString(), expected, StringComparison.Ordinal);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TrustedExceptionRequest? request, CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        if (request is null)
        {
            return BadRequest(new { message = "بيانات طلب الاستثناء مطلوبة" });
        }

        var requester = ToTrustedManager(request);
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
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? cityValue,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? requesterUserName,
        CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        if (string.IsNullOrWhiteSpace(requesterUserName))
        {
            return BadRequest(new { message = "معرّف مدير المبيعات مطلوب" });
        }

        var (normalizedPage, normalizedSize) = PagingDefaults.Normalize(page, pageSize);
        var result = await _exceptions.ListAsync(new SalesExceptionQuery
        {
            Page = normalizedPage,
            PageSize = normalizedSize,
            Status = status,
            CityValue = cityValue,
            RequestingManagerUserName = requesterUserName.Trim()
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

    /// <summary>
    /// Builds the actor from the branch-asserted trusted fields. Role is forced to Sales Manager
    /// because this intake exists only for that path; CreateAsync still enforces the role check.
    /// </summary>
    private static GatewayUser ToTrustedManager(TrustedExceptionRequest request)
    {
        var userName = Trimmed(request.RequesterUserName)
                       ?? Trimmed(request.RequesterUserId)
                       ?? "";
        return new GatewayUser
        {
            UserID = Trimmed(request.RequesterUserId) ?? "",
            UserName = userName,
            UserType = SalesRoles.UserTypeSalesManager,
            CityValue = Trimmed(request.CityValue) ?? "",
            CityName = Trimmed(request.CityName) ?? ""
        };
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
