using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DelegatesController : ControllerBase
    {
        private readonly IDelegateRepository _delegateRepository;
        private readonly IFollowerActionsRepository _followerActions;

        public DelegatesController(
            IDelegateRepository delegateRepository,
            IFollowerActionsRepository followerActions)
        {
            _delegateRepository = delegateRepository;
            _followerActions = followerActions;
        }

        [HttpGet("GetDelegateLogin/asyncId={asyncID}")]
        public async Task<ActionResult<DelegateGetDTO?>> GetDelegateLogin(string? asyncID)
        {
            try
            {
                var result = await _delegateRepository.GetDelegateLogin(asyncID?.Trim().TrimEnd('/'));
                if (result != null)
                {
                    return Ok(result);
                }
                return Unauthorized("Invalid delegate login.");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetDelegateCheckLogout/asyncId={asyncID}")]
        public async Task<ActionResult<DelegateGetDTO?>> GetDelegateCheckLogout(string? asyncID)
        {
            try
            {
                var result = await _delegateRepository.GetDelegateCheckLogout(asyncID);
                if (result != null)
                {
                    return Ok(result);
                }
                return Unauthorized("Delegate already logged out.");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetDelegateTitle/{delegateId}")]
        public async Task<ActionResult<DelegateInfoGetDTO?>> GetDelegateTitle(int? delegateId)
        {
            try
            {
                var result = await _delegateRepository.GetDelegateTitle(delegateId);
                if (result != null)
                {
                    return Ok(result);
                }
                return NotFound("Delegate title not found.");
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("GetDelegateSelect/{delegateId}")]
        public async Task<ActionResult<IEnumerable<SelectDelegateGetDTO>>> GetDelegateSelect(int? delegateId)
        {
            try
            {
                var result = await _delegateRepository.GetDelegateSelect(delegateId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// Delegate sales request — same SalesRequests table/contract as Followers/SalesRequests.
        /// SaleRequestType: New (Home) or Old (customer card). Province from server only.
        /// </summary>
        [HttpPost("SalesRequests")]
        public async Task<IActionResult> SubmitSalesRequest([FromBody] DelegateSalesRequestCreateDTO body, CancellationToken ct)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.AsyncId))
            {
                return BadRequest(new { message = "رمز المندوب مطلوب" });
            }

            var auth = await _delegateRepository.GetDelegateLogin(body.AsyncId.Trim().TrimEnd('/'));
            if (auth == null || auth.DelegateId <= 0)
            {
                return Unauthorized(new { message = "رمز المندوب غير صحيح" });
            }

            var kind = SaleRequestTypes.Normalize(body.SaleRequestType, body.CustomerId is > 0);
            if (!SaleRequestTypes.IsNew(body.SaleRequestType) && !SaleRequestTypes.IsOld(body.SaleRequestType))
            {
                // Infer only when client omitted; if present must be New|Old.
                if (!string.IsNullOrWhiteSpace(body.SaleRequestType))
                {
                    return BadRequest(new { message = "SaleRequestType يجب أن يكون New أو Old" });
                }
            }

            var listId = body.ListId > 0 ? body.ListId : auth.DelegateId;

            // Delegate may only use own list id (or a SelectDelegate-linked child).
            if (listId != auth.DelegateId)
            {
                var linked = await _delegateRepository.IsFollowerListLinked(auth.DelegateId, listId);
                if (!linked)
                {
                    // IsFollowerListLinked also checks Users path; for pure delegates use SelectDelegate SP via GetDelegateSelect.
                    var children = await _delegateRepository.GetDelegateSelect(auth.DelegateId) ?? [];
                    if (!children.Any(c => c.DelegateChildId == listId || c.DelegateId == listId))
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new { message = "القائمة غير مرتبطة بحسابك" });
                    }
                }
            }

            string? province = null;
            if (auth.CityId is > 0)
            {
                province = await _followerActions.GetCityNameByIdAsync(auth.CityId.Value, ct);
            }
            if (string.IsNullOrWhiteSpace(province))
            {
                province = await _followerActions.GetFollowerCityNameAsync(auth.DelegateId, ct);
            }

            string name;
            string? phone;
            string? address;
            int? existingId = null;

            if (SaleRequestTypes.IsOld(kind))
            {
                if (body.CustomerId is not > 0)
                {
                    return BadRequest(new { message = "CustomerID مطلوب لطلب المبيع القديم" });
                }

                var scope = await _followerActions.GetCustomerScopeAsync(body.CustomerId.Value, ct);
                if (scope == null)
                {
                    return NotFound(new { message = "الزبون غير موجود" });
                }

                // Security: customer must belong to this delegate list (or linked child list).
                if (scope.Value.DelegateId != auth.DelegateId && scope.Value.DelegateId != listId)
                {
                    var children = await _delegateRepository.GetDelegateSelect(auth.DelegateId) ?? [];
                    var allowed = children.Any(c =>
                        c.DelegateChildId == scope.Value.DelegateId || c.DelegateId == scope.Value.DelegateId);
                    if (!allowed)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden,
                            new { message = "الزبون خارج قوائم المندوب — لا يمكن إرسال الطلب" });
                    }
                }

                existingId = body.CustomerId.Value;
                name = (scope.Value.CustomerName ?? string.Empty).Trim();
                phone = scope.Value.Phone?.Trim();
                address = string.IsNullOrWhiteSpace(scope.Value.Address)
                    ? "—"
                    : scope.Value.Address.Trim();
                if (!string.IsNullOrWhiteSpace(scope.Value.CityName))
                {
                    province = scope.Value.CityName;
                }
                else if (string.IsNullOrWhiteSpace(province) && auth.CityId is > 0)
                {
                    province = await _followerActions.GetCityNameByIdAsync(auth.CityId.Value, ct);
                }
            }
            else
            {
                name = (body.FullName ?? string.Empty).Trim();
                phone = body.Phone?.Trim().Replace(" ", string.Empty);
                address = body.Address?.Trim();

                if (!FollowerAuthorization.IsValidFollowerPhone(phone))
                {
                    return BadRequest(new { message = "رقم الهاتف يجب أن يكون 11 رقماً ويبدأ بـ 07" });
                }

                if (string.IsNullOrWhiteSpace(address))
                {
                    return BadRequest(new { message = "العنوان مطلوب" });
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "اسم الزبون مطلوب" });
            }

            if (string.IsNullOrWhiteSpace(province))
            {
                return BadRequest(new { message = "محافظة المندوب غير معرّفة في النظام" });
            }

            // Ignore any client province.
            _ = body.Province;

            var now = DateTime.UtcNow;
            var listName = await _followerActions.GetDelegateNameAsync(listId, ct);
            var notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim();
            if (!string.IsNullOrWhiteSpace(listName))
            {
                var listTag = $"القائمة: {listName} (#{listId})";
                notes = string.IsNullOrWhiteSpace(notes) ? listTag : $"{listTag}\n{notes}";
            }

            var saved = await _followerActions.InsertSalesRequestAsync(
                new FollowerSalesRequestResultDTO
                {
                    CreatedByUserId = auth.DelegateId,
                    CreatedByName = auth.DelegateName?.Trim() is { Length: > 0 } n ? n : "مندوب",
                    CreatedByUserType = "مندوب",
                    CreatedAtUtc = now
                },
                name,
                phone,
                province,
                address ?? "",
                notes,
                existingId,
                province,
                province,
                customerSourceType: "Delegate",
                saleRequestType: kind,
                sourceListId: listId,
                ct: ct);

            return Ok(saved);
        }
    }
}
