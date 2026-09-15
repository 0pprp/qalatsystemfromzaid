using BE_Company.Sales.Authorization;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers
{
    /// <summary>
    /// Branch-facing Sales Manager exception API. Authenticates with the BE_Company JWT,
    /// resolves actor identity from the principal, then forwards server-to-server to the gateway.
    /// </summary>
    [Authorize(Policy = SalesPolicies.SalesManager)]
    [Route("api/sales-manager/exceptions")]
    [ApiController]
    public sealed class SalesManagerExceptionsController : ControllerBase
    {
        private readonly SalesIdentityService _identity;
        private readonly ISalesExceptionGatewayForwarder _forwarder;

        public SalesManagerExceptionsController(
            SalesIdentityService identity,
            ISalesExceptionGatewayForwarder forwarder)
        {
            _identity = identity;
            _forwarder = forwarder;
        }

        /// <summary>
        /// Browser may only supply business fields. Any requester* / role fields are ignored.
        /// </summary>
        public sealed class CreateExceptionBody
        {
            public string? CityValue { get; set; }
            public string? CityName { get; set; }
            public int? CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public string? CustomerPhone { get; set; }
            public int? SalesRequestId { get; set; }
            public string? Reason { get; set; }
            public string? TargetApproverType { get; set; }

            // Spoof surface — accepted by binder so tests can prove they are ignored.
            public string? RequesterUserId { get; set; }
            public string? RequesterUserName { get; set; }
            public string? RequesterDisplayName { get; set; }
            public string? Role { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateExceptionBody? body, CancellationToken ct)
        {
            var actor = _identity.FromAuthenticatedUser();
            if (actor is null)
            {
                return Unauthorized();
            }

            if (!SalesRoles.IsSalesManager(actor.UserType))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "طلب الاستثناء متاح لمدير المبيعات فقط" });
            }

            body ??= new CreateExceptionBody();
            if (!TryResolveCity(actor, body.CityValue, body.CityName, out var cityValue, out var cityName, out var cityError))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = cityError });
            }

            var forwarded = await _forwarder.CreateAsync(new SalesExceptionCreateForwardRequest
            {
                CityValue = cityValue,
                CityName = cityName,
                CustomerId = body.CustomerId,
                CustomerName = body.CustomerName,
                CustomerPhone = body.CustomerPhone,
                SalesRequestId = body.SalesRequestId,
                Reason = body.Reason,
                TargetApproverType = body.TargetApproverType,
                RequesterUserId = actor.EmployeeId > 0 ? actor.EmployeeId.ToString() : actor.ExternalUserId,
                RequesterUserName = actor.EmployeeName,
                RequesterDisplayName = actor.EmployeeName,
                Role = SalesRoles.UserTypeSalesManager
            }, ct);

            return ToActionResult(forwarded);
        }

        [HttpGet]
        public async Task<IActionResult> List(
            [FromQuery] string? status,
            [FromQuery] string? cityValue,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            CancellationToken ct)
        {
            var actor = _identity.FromAuthenticatedUser();
            if (actor is null)
            {
                return Unauthorized();
            }

            if (!SalesRoles.IsSalesManager(actor.UserType))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "عرض طلبات الاستثناء متاح لمدير المبيعات فقط" });
            }

            string? scopedCity = null;
            if (!string.IsNullOrWhiteSpace(cityValue))
            {
                if (!TryResolveCity(actor, cityValue, null, out scopedCity, out _, out var cityError))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = cityError });
                }
            }

            var forwarded = await _forwarder.ListAsync(new SalesExceptionListForwardRequest
            {
                Status = status,
                CityValue = scopedCity ?? cityValue,
                Page = page,
                PageSize = pageSize,
                RequesterUserName = actor.EmployeeName
            }, ct);

            return ToActionResult(forwarded);
        }

        private static IActionResult ToActionResult(SalesExceptionGatewayForwardResult forwarded)
        {
            if (!forwarded.Ok)
            {
                return new ObjectResult(new { message = forwarded.ErrorMessage })
                {
                    StatusCode = forwarded.FailureStatusCode
                };
            }

            return new ContentResult
            {
                Content = forwarded.ResponseBody ?? "{}",
                ContentType = forwarded.ContentType,
                StatusCode = StatusCodes.Status200OK
            };
        }

        /// <summary>
        /// Branch database is the tenancy boundary. Reject comparable foreign city keys.
        /// </summary>
        private static bool TryResolveCity(
            SalesIdentity actor,
            string? requestedCityValue,
            string? requestedCityName,
            out string cityValue,
            out string? cityName,
            out string error)
        {
            cityValue = string.IsNullOrWhiteSpace(requestedCityValue)
                ? actor.BranchId
                : requestedCityValue.Trim();
            cityName = string.IsNullOrWhiteSpace(requestedCityName)
                ? actor.BranchName
                : requestedCityName.Trim();
            error = "";

            if (string.IsNullOrWhiteSpace(cityValue))
            {
                error = "المحافظة مطلوبة";
                return false;
            }

            if (!SalesBranchScope.PassesOptionalCityFilter(cityValue, actor.BranchId, actor.BranchName))
            {
                error = "لا يمكن إنشاء طلب استثناء لمحافظة خارج صلاحياتك";
                return false;
            }

            return true;
        }
    }
}
