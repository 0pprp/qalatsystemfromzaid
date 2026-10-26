using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Route("api/internal/waivers")]
[ApiController]
public sealed class InternalWaiversController : ControllerBase
{
    public const string GatewayKeyHeader = "X-Sales-Gateway-Key";

    private readonly CustomerWaiverService _waivers;
    private readonly IConfiguration _configuration;

    public InternalWaiversController(CustomerWaiverService waivers, IConfiguration configuration)
    {
        _waivers = waivers;
        _configuration = configuration;
    }

    public sealed class TrustedWaiverRequest
    {
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? Note { get; set; }
        public string? RequesterUserId { get; set; }
        public string? RequesterUserName { get; set; }
        public string? RequesterDisplayName { get; set; }
        public string? Role { get; set; }
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TrustedWaiverRequest? request, CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        if (request is null)
        {
            return BadRequest(new { message = "بيانات طلب التنازل مطلوبة" });
        }

        var requester = new GatewayUser
        {
            UserID = request.RequesterUserId ?? "0",
            UserName = string.IsNullOrWhiteSpace(request.RequesterDisplayName)
                ? (request.RequesterUserName ?? "مدير فرع")
                : request.RequesterDisplayName!,
            UserType = string.IsNullOrWhiteSpace(request.Role) ? "مدير فرع" : request.Role!,
            CityValue = request.CityValue ?? "",
            CityName = request.CityName ?? "",
            IsCentral = false
        };

        var result = await _waivers.CreateAsync(requester, new CreateCustomerWaiverInput
        {
            CityValue = request.CityValue,
            CityName = request.CityName,
            CustomerId = request.CustomerId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            Note = request.Note
        }, ct);

        return MapResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] string? cityValue,
        [FromQuery] string? requesterUserName,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        var (normalizedPage, normalizedSize) = PagingDefaults.Normalize(page, pageSize);
        var result = await _waivers.ListAsync(new CustomerWaiverQuery
        {
            Page = normalizedPage,
            PageSize = normalizedSize,
            Status = status,
            CityValue = cityValue,
            BranchManagerUserName = requesterUserName
        }, ct);

        return Ok(new
        {
            page = result.Page,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages,
            items = result.Items.Select(DelegatedManagerController.ToWaiverSummary)
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        var (outcome, detail, error) = await _waivers.GetDetailAsync(id, ct);
        if (outcome == CustomerWaiverOutcome.NotFound || detail is null)
        {
            return NotFound(new { message = error ?? "الطلب غير موجود" });
        }

        return Ok(new
        {
            request = DelegatedManagerController.ToWaiverDetail(detail.Request),
            audit = detail.Audit,
            review = detail.Review,
            reviewError = detail.ReviewError
        });
    }

    /// <summary>Branch accountant applied discount — finalize gateway status.</summary>
    [HttpPost("{id:guid}/accountant-applied")]
    public async Task<IActionResult> AccountantApplied(Guid id, CancellationToken ct)
    {
        if (!HasValidGatewayKey(Request, _configuration))
        {
            return Unauthorized(new { message = "مفتاح البوابة الداخلي غير صالح" });
        }

        return MapResult(await _waivers.MarkAccountantAppliedAsync(id, ct));
    }

    private IActionResult MapResult(CustomerWaiverResult result) => result.Outcome switch
    {
        CustomerWaiverOutcome.Success => Ok(DelegatedManagerController.ToWaiverDetail(result.Request!)),
        CustomerWaiverOutcome.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new { message = result.Error }),
        CustomerWaiverOutcome.Conflict => Conflict(new { message = result.Error }),
        CustomerWaiverOutcome.NotFound => NotFound(new { message = result.Error }),
        _ => BadRequest(new { message = result.Error })
    };
}
