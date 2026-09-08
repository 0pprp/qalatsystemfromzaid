using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FollowersController : ControllerBase
    {
        private readonly IDelegateRepository _delegateRepository;
        private readonly ICustomersRepository _customersRepository;
        private readonly IFollowerActionsRepository _followerActions;

        public FollowersController(
            IDelegateRepository delegateRepository,
            ICustomersRepository customersRepository,
            IFollowerActionsRepository followerActions)
        {
            _delegateRepository = delegateRepository;
            _customersRepository = customersRepository;
            _followerActions = followerActions;
        }

        [HttpGet("Lists")]
        public async Task<ActionResult<IEnumerable<SelectDelegateGetDTO>>> Lists([FromQuery] string? asyncId)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null)
                {
                    return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                }

                var lists = await _delegateRepository.GetDelegateSelect(father.DelegateId) ?? Enumerable.Empty<SelectDelegateGetDTO>();
                return Ok(lists);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Daily")]
        public async Task<ActionResult<IEnumerable<CustomersFollowGetDTO>>> Daily(
            [FromQuery] string? asyncId,
            [FromQuery] int childId,
            [FromQuery] DateTime date,
            [FromQuery] string? showType)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null)
                {
                    return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                }

                if (childId <= 0)
                {
                    return BadRequest(new { message = "يجب اختيار قائمة" });
                }

                var linked = await _delegateRepository.IsFollowerListLinked(father.DelegateId, childId);
                if (!FollowerAuthorization.CanAccessAssignedList(linked))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذه القائمة غير مرتبطة بحساب المتابع" });
                }

                var type = string.IsNullOrWhiteSpace(showType) ? "المسددين" : showType;
                var result = await _customersRepository.Customers_Follow(childId, date, type);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Customers/{customerId:int}/profile")]
        public async Task<IActionResult> CustomerProfile(
            int customerId,
            [FromQuery] string? asyncId,
            [FromQuery] int listId,
            CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId);
            if (father == null)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            var denied = await EnsureCustomerInScope(father.DelegateId, listId, customerId, ct);
            if (denied != null)
            {
                return denied;
            }

            var info = await _customersRepository.GetCustomersDataInfo(customerId);
            if (info == null)
            {
                return NotFound(new { message = "الزبون غير موجود" });
            }

            var notes = await _followerActions.ListCustomerNotesAsync(customerId, father.DelegateId, ct);
            var imageUrl = FollowerAuthorization.BuildImageUrl(ImagesBaseUrl(), info.CustomerImage);
            var images = new List<FollowerProfileImageDTO>();
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                images.Add(new FollowerProfileImageDTO
                {
                    Kind = "Customer",
                    Label = "صورة الزبون",
                    Url = imageUrl
                });
            }

            var profile = new FollowerCustomerProfileDTO
            {
                CustomerId = info.CustomerId,
                CustomerName = info.CustomerName,
                PhoneNumber = info.PhoneNumber,
                Address = info.Address,
                CityName = info.CityName,
                ShopName = info.ShopName,
                StoreAddress = info.StoreAddress,
                StorePhoneNumber = info.StorePhoneNumber,
                NearestFunctionPoint = info.NearestFunctionPoint,
                Neighborhood = info.Neighborhood,
                Latitude = info.Latitude,
                Longitude = info.Longitude,
                CustomerImage = info.CustomerImage,
                CustomerImageUrl = imageUrl,
                DelegateName = info.DelegateName,
                ListDelegateId = listId,
                CostTotalSales = info.CostTotalSales,
                AmountTotalSales = info.AmountTotalSales,
                AmountDaySales = info.AmountDaySales,
                AmountRemaining = info.AmountRemaining,
                ReceiptsTotal = info.ReceiptsTotal,
                AmountReceverDay = info.AmountReceverDay,
                ItemsNames = info.ItemsNames,
                DateSaleDevice = info.DateSaleDevice,
                CustomerSystemNotes = info.Notes,
                Notes = notes.ToList(),
                Images = images
            };
            return Ok(profile);
        }

        [HttpGet("Customers/{customerId:int}/notes")]
        public async Task<IActionResult> ListCustomerNotes(
            int customerId,
            [FromQuery] string? asyncId,
            [FromQuery] int listId,
            CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId);
            if (father == null)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            var denied = await EnsureCustomerInScope(father.DelegateId, listId, customerId, ct);
            if (denied != null)
            {
                return denied;
            }

            var notes = await _followerActions.ListCustomerNotesAsync(customerId, father.DelegateId, ct);
            return Ok(notes);
        }

        [HttpPost("Customers/{customerId:int}/notes")]
        public async Task<IActionResult> AddCustomerNote(
            int customerId,
            [FromBody] FollowerNoteCreateDTO body,
            CancellationToken ct)
        {
            var father = await AuthenticateFollower(body.AsyncId);
            if (father == null)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            if (string.IsNullOrWhiteSpace(body.NoteText))
            {
                return BadRequest(new { message = "نص الملاحظة مطلوب" });
            }

            var denied = await EnsureCustomerInScope(father.DelegateId, body.ListId, customerId, ct);
            if (denied != null)
            {
                return denied;
            }

            _ = FollowerAuthorization.AcceptClientCreatedByUserId(body.CreatedByUserId, father.DelegateId);

            var saved = await _followerActions.AddCustomerNoteAsync(new FollowerCustomerNoteDTO
            {
                CustomerId = customerId,
                NoteText = body.NoteText.Trim(),
                CreatedByUserId = FollowerAuthorization.ResolveCreatedByUserId(father.DelegateId, body.CreatedByUserId),
                CreatedByName = father.DelegateName?.Trim() is { Length: > 0 } n ? n : "متابع",
                CreatedByRole = FollowerAuthorization.RoleFollower,
                CreatedAtUtc = DateTime.UtcNow
            }, ct);
            return Ok(saved);
        }

        [HttpGet("Delegates/{delegateId:int}/notes")]
        public async Task<IActionResult> ListDelegateNotes(
            int delegateId,
            [FromQuery] string? asyncId,
            [FromQuery] int listId,
            CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId);
            if (father == null)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            var linked = await _delegateRepository.IsFollowerListLinked(father.DelegateId, listId);
            if (!FollowerAuthorization.CanNoteListDelegate(linked, listId, delegateId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "لا يمكنك عرض ملاحظات مندوب هذه القائمة" });
            }

            var notes = await _followerActions.ListDelegateNotesForFollowerAsync(delegateId, father.DelegateId, ct);
            return Ok(notes);
        }

        [HttpPost("Delegates/{delegateId:int}/notes")]
        public async Task<IActionResult> AddDelegateNote(
            int delegateId,
            [FromBody] FollowerNoteCreateDTO body,
            CancellationToken ct)
        {
            var father = await AuthenticateFollower(body.AsyncId);
            if (father == null)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            if (string.IsNullOrWhiteSpace(body.NoteText))
            {
                return BadRequest(new { message = "نص الملاحظة مطلوب" });
            }

            var linked = await _delegateRepository.IsFollowerListLinked(father.DelegateId, body.ListId);
            // Force target = assigned list delegate (ignore spoofed path id mismatch).
            if (!FollowerAuthorization.CanNoteListDelegate(linked, body.ListId, delegateId)
                || delegateId != body.ListId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "لا يمكنك إضافة ملاحظة على مندوب قائمة غير مسندة" });
            }

            _ = FollowerAuthorization.AcceptClientCreatedByUserId(body.CreatedByUserId, father.DelegateId);

            var saved = await _followerActions.AddDelegateNoteAsync(new FollowerDelegateNoteDTO
            {
                DelegateId = body.ListId,
                ListId = body.ListId,
                NoteText = body.NoteText.Trim(),
                CreatedByUserId = FollowerAuthorization.ResolveCreatedByUserId(father.DelegateId, body.CreatedByUserId),
                CreatedByName = father.DelegateName?.Trim() is { Length: > 0 } n ? n : "متابع",
                CreatedByRole = FollowerAuthorization.RoleFollower,
                CreatedAtUtc = DateTime.UtcNow
            }, ct);
            return Ok(saved);
        }

        [HttpPut("notes/{id:int}")]
        public IActionResult NotePutForbidden() =>
            StatusCode(StatusCodes.Status403Forbidden, new { message = "لا يمكن تعديل أو حذف الملاحظة بعد الحفظ" });

        [HttpDelete("notes/{id:int}")]
        public IActionResult NoteDeleteForbidden() =>
            StatusCode(StatusCodes.Status403Forbidden, new { message = "لا يمكن تعديل أو حذف الملاحظة بعد الحفظ" });

        [HttpPost("SalesRequests")]
        public async Task<IActionResult> SubmitSalesRequest([FromBody] FollowerSalesRequestCreateDTO body, CancellationToken ct)
        {
            var father = await AuthenticateFollower(body.AsyncId);
            if (father == null)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            if (body.ListId <= 0)
            {
                return BadRequest(new { message = "يجب اختيار القائمة المسندة" });
            }

            var linked = await _delegateRepository.IsFollowerListLinked(father.DelegateId, body.ListId);
            var existingId = body.CustomerId is > 0 ? body.CustomerId : null;

            string? name;
            string? phone;
            string? address;
            string? province;
            string? cityValue;
            string? cityName;

            if (existingId is > 0)
            {
                var denied = await EnsureCustomerInScope(father.DelegateId, body.ListId, existingId.Value, ct);
                if (denied != null)
                {
                    return denied;
                }

                var scope = await _followerActions.GetCustomerScopeAsync(existingId.Value, ct);
                if (scope == null)
                {
                    return NotFound(new { message = "الزبون غير موجود" });
                }

                name = (body.FullName ?? scope.Value.CustomerName ?? string.Empty).Trim();
                phone = string.IsNullOrWhiteSpace(body.Phone) ? scope.Value.Phone : body.Phone.Trim();
                address = string.IsNullOrWhiteSpace(body.Address) ? scope.Value.Address : body.Address.Trim();
                province = string.IsNullOrWhiteSpace(body.Province) ? scope.Value.CityName : body.Province.Trim();
                cityValue = scope.Value.CityName;
                cityName = scope.Value.CityName;
            }
            else
            {
                if (!FollowerAuthorization.CanSubmitNewCustomerRequest(linked, body.ListId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "القائمة غير مسندة" });
                }

                name = (body.FullName ?? string.Empty).Trim();
                phone = body.Phone?.Trim();
                address = body.Address?.Trim();
                province = body.Province?.Trim();
                cityValue = province;
                cityName = province;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "اسم الزبون مطلوب" });
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest(new { message = "رقم الهاتف مطلوب" });
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                return BadRequest(new { message = "العنوان مطلوب" });
            }

            _ = FollowerAuthorization.AcceptClientCreatedByUserId(body.CreatedByUserId, father.DelegateId);
            var createdBy = FollowerAuthorization.ResolveCreatedByUserId(father.DelegateId, body.CreatedByUserId);
            var now = DateTime.UtcNow;

            var saved = await _followerActions.InsertSalesRequestAsync(
                new FollowerSalesRequestResultDTO
                {
                    CreatedByUserId = createdBy,
                    CreatedByName = father.DelegateName?.Trim() is { Length: > 0 } n ? n : "متابع",
                    CreatedByUserType = FollowerAuthorization.RoleFollower,
                    CreatedAtUtc = now
                },
                name,
                phone,
                province,
                address,
                string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim(),
                existingId,
                cityValue,
                cityName,
                ct);

            return Ok(saved);
        }

        private string ImagesBaseUrl()
        {
            var request = HttpContext.Request;
            var root = $"{request.Scheme}://{request.Host}";
            return $"{root}/Images";
        }

        private async Task<IActionResult?> EnsureCustomerInScope(int followerId, int listId, int customerId, CancellationToken ct)
        {
            if (listId <= 0 || customerId <= 0)
            {
                return BadRequest(new { message = "القائمة والزبون مطلوبان" });
            }

            var linked = await _delegateRepository.IsFollowerListLinked(followerId, listId);
            var scope = await _followerActions.GetCustomerScopeAsync(customerId, ct);
            if (scope == null)
            {
                return NotFound(new { message = "الزبون غير موجود" });
            }

            if (!FollowerAuthorization.CanNoteCustomer(linked, scope.Value.DelegateId, listId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "الزبون خارج نطاق القوائم المسندة لك" });
            }

            return null;
        }

        private async Task<DelegateGetDTO?> AuthenticateFollower(string? asyncId)
        {
            if (string.IsNullOrWhiteSpace(asyncId))
            {
                return null;
            }

            var login = await _delegateRepository.GetDelegateLogin(asyncId);
            if (login == null || login.DelegateId <= 0)
            {
                return null;
            }

            return login;
        }
    }
}
