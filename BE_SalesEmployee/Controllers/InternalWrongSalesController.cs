using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Server-to-server WrongSale inbox for تطبيق إدارة الفرع (pending / reviewed + mandatory note).
/// </summary>
[Route("api/internal/wrong-sales")]
[ApiController]
public sealed class InternalWrongSalesController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly SalesExceptionService _exceptions;
    private readonly IConfiguration _configuration;

    public InternalWrongSalesController(SalesExceptionService exceptions, IConfiguration configuration)
    {
        _exceptions = exceptions;
        _configuration = configuration;
    }

    public sealed class ReviewBody
    {
        public string? Note { get; set; }
        public string? ReviewerUserName { get; set; }
        public string? ReviewerDisplayName { get; set; }
    }

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

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? cityValue,
        [FromQuery] bool reviewed = false,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken ct = default)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        var (normalizedPage, normalizedSize) = PagingDefaults.Normalize(page, pageSize);
        var result = await _exceptions.ListWrongSalesForBranchManagerAsync(
            cityValue, reviewed, normalizedPage, normalizedSize, ct);

        return Ok(new
        {
            page = result.Page,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages,
            reviewed,
            items = result.Items.Select(DelegatedManagerController.ToExceptionSummary)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        var (outcome, detail, error) = await _exceptions.GetEnrichedDetailAsync(id, ct);
        return outcome switch
        {
            SalesExceptionOutcome.Success when detail is not null => Ok(new
            {
                request = DelegatedManagerController.ToExceptionDetail(detail.Request),
                customerProfile = detail.CustomerProfile,
                review = detail.Review,
                reviewError = detail.ReviewError,
                decisionHistory = detail.Audit.Select(a => new
                {
                    a.ActorDisplayName,
                    a.ActorRole,
                    a.PreviousStatus,
                    a.NewStatus,
                    a.DecisionNote,
                    a.CreatedAtUtc
                })
            }),
            SalesExceptionOutcome.NotFound => NotFound(new { message = error ?? "المبيع الخاطئ غير موجود" }),
            _ => BadRequest(new { message = error ?? "تعذر تحميل التفاصيل" })
        };
    }

    [HttpPost("{id:guid}/review")]
    public async Task<IActionResult> Review(Guid id, [FromBody] ReviewBody? body, CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        body ??= new ReviewBody();
        var result = await _exceptions.MarkBranchManagerReviewedAsync(
            id,
            body.ReviewerUserName,
            body.ReviewerDisplayName,
            body.Note,
            ct);

        return result.Outcome switch
        {
            SalesExceptionOutcome.Success =>
                Ok(DelegatedManagerController.ToExceptionDetail(result.Request!)),
            SalesExceptionOutcome.NotFound =>
                NotFound(new { message = result.Error }),
            SalesExceptionOutcome.Conflict =>
                Conflict(new { message = result.Error }),
            _ => BadRequest(new { message = result.Error })
        };
    }
}
