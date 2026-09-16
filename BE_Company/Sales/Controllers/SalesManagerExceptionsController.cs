using System.Text.Json;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
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
        private readonly ISalesRequestService _requests;

        public SalesManagerExceptionsController(
            SalesIdentityService identity,
            ISalesExceptionGatewayForwarder forwarder,
            ISalesRequestService requests)
        {
            _identity = identity;
            _forwarder = forwarder;
            _requests = requests;
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

        public sealed class AssignExceptionBody
        {
            public int EmployeeId { get; set; }
            public string? EmployeeName { get; set; }
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
            if (body.SalesRequestId is null or <= 0)
            {
                return BadRequest(new { message = "معرّف طلب البيع مطلوب لطلب الاستثناء." });
            }

            var salesRequestId = body.SalesRequestId.Value;
            var original = await _requests.GetForManagerAsync(salesRequestId, ct);
            if (original is null)
            {
                return NotFound(new { message = "طلب المبيع غير موجود." });
            }

            if (SalesExceptionHoldStatuses.IsHeld(original.ExceptionHoldStatus))
            {
                return Conflict(new { message = "يوجد طلب استثناء نشط لهذا الطلب مسبقاً." });
            }

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
                SalesRequestId = salesRequestId,
                Reason = body.Reason,
                TargetApproverType = body.TargetApproverType,
                RequesterUserId = actor.EmployeeId > 0 ? actor.EmployeeId.ToString() : actor.ExternalUserId,
                RequesterUserName = actor.EmployeeName,
                RequesterDisplayName = actor.EmployeeName,
                Role = SalesRoles.UserTypeSalesManager
            }, ct);

            if (!forwarded.Ok)
            {
                return ToActionResult(forwarded);
            }

            if (!TryReadExceptionId(forwarded.ResponseBody, out var exceptionId))
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "تم إنشاء الاستثناء مركزياً لكن تعذر قراءة معرّفه — لم يُطبَّق الحجز على طلب المبيع."
                });
            }

            try
            {
                await _requests.SetExceptionHoldAsync(
                    salesRequestId,
                    SalesExceptionHoldStatuses.Pending,
                    exceptionId,
                    ct);
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new
                {
                    message = $"تم إنشاء الاستثناء لكن فشل حجز طلب المبيع: {ex.Message}"
                });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new
                {
                    message = "تم إنشاء الاستثناء مركزياً لكن فشل حجز طلب المبيع في الفرع."
                });
            }

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

        [HttpPost("{id:guid}/assign")]
        public async Task<IActionResult> Assign(Guid id, [FromBody] AssignExceptionBody? body, CancellationToken ct)
        {
            var actor = _identity.FromAuthenticatedUser();
            if (actor is null)
            {
                return Unauthorized();
            }

            if (!SalesRoles.IsSalesManager(actor.UserType))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "إسناد الاستثناء متاح لمدير المبيعات فقط" });
            }

            body ??= new AssignExceptionBody();
            if (body.EmployeeId <= 0)
            {
                return BadRequest(new { message = "يجب اختيار موظف المبيعات" });
            }

            var got = await _forwarder.GetAsync(id, ct);
            if (!got.Ok)
            {
                return ToActionResult(got);
            }

            if (!TryReadExceptionSnapshot(got.ResponseBody, out var snapshot))
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "تعذر قراءة بيانات الاستثناء" });
            }

            if (!string.Equals(snapshot.Status, SalesExceptionHoldStatuses.Approved, StringComparison.OrdinalIgnoreCase)
                || snapshot.AssignmentConsumed)
            {
                return Conflict(new { message = "لا يمكن إسناد هذا الاستثناء" });
            }

            if (snapshot.SalesRequestId is null or <= 0)
            {
                return BadRequest(new { message = "طلب المبيع المرتبط بالاستثناء غير موجود" });
            }

            var original = await _requests.GetForManagerAsync(snapshot.SalesRequestId.Value, ct);
            if (original is null)
            {
                return NotFound(new { message = "طلب المبيع الأصلي غير موجود" });
            }

            if (!string.IsNullOrWhiteSpace(original.CityValue)
                && !SalesBranchScope.PassesOptionalCityFilter(original.CityValue, actor.BranchId, actor.BranchName))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "لا يمكن إسناد طلب خارج صلاحياتك" });
            }

            try
            {
                var assigned = await _requests.AssignAsync(
                    actor,
                    snapshot.SalesRequestId.Value,
                    new SalesRequestAssignDTO
                    {
                        EmployeeId = body.EmployeeId,
                        EmployeeName = body.EmployeeName
                    },
                    ct,
                    exceptionAssignId: id);

                var consumed = await _forwarder.ConsumeAsync(id, new SalesExceptionConsumeForwardRequest
                {
                    EmployeeId = body.EmployeeId,
                    EmployeeName = body.EmployeeName ?? assigned.TargetEmployeeName,
                    AssignedByManagerUserName = actor.EmployeeName
                }, ct);

                if (!consumed.Ok)
                {
                    return ToActionResult(consumed);
                }

                await _requests.SetExceptionHoldAsync(snapshot.SalesRequestId.Value, null, null, ct);
                return Ok(assigned);
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
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

        private static bool TryReadExceptionId(string? body, out Guid id)
        {
            id = Guid.Empty;
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("id", out var idProp)
                    && idProp.ValueKind == JsonValueKind.String
                    && Guid.TryParse(idProp.GetString(), out id))
                {
                    return true;
                }

                if (doc.RootElement.TryGetProperty("Id", out idProp)
                    && idProp.ValueKind == JsonValueKind.String
                    && Guid.TryParse(idProp.GetString(), out id))
                {
                    return true;
                }
            }
            catch (JsonException)
            {
            }

            return false;
        }

        private sealed class ExceptionSnapshot
        {
            public string? Status { get; init; }
            public bool AssignmentConsumed { get; init; }
            public int? SalesRequestId { get; init; }
            public string? CityValue { get; init; }
        }

        private static bool TryReadExceptionSnapshot(string? body, out ExceptionSnapshot snapshot)
        {
            snapshot = new ExceptionSnapshot();
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                snapshot = new ExceptionSnapshot
                {
                    Status = ReadString(root, "status", "Status"),
                    AssignmentConsumed = ReadBool(root, "assignmentConsumed", "AssignmentConsumed"),
                    SalesRequestId = ReadInt(root, "salesRequestId", "SalesRequestId"),
                    CityValue = ReadString(root, "cityValue", "CityValue")
                };
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string? ReadString(JsonElement root, params string[] names)
        {
            foreach (var name in names)
            {
                if (root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
                {
                    return prop.GetString();
                }
            }

            return null;
        }

        private static bool ReadBool(JsonElement root, params string[] names)
        {
            foreach (var name in names)
            {
                if (root.TryGetProperty(name, out var prop)
                    && (prop.ValueKind is JsonValueKind.True or JsonValueKind.False))
                {
                    return prop.GetBoolean();
                }
            }

            return false;
        }

        private static int? ReadInt(JsonElement root, params string[] names)
        {
            foreach (var name in names)
            {
                if (root.TryGetProperty(name, out var prop) && prop.TryGetInt32(out var value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
