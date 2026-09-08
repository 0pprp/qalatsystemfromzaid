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

            // Reject client spoof of CreatedBy*
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

        [HttpGet("Employees/{employeeId:int}/notes")]
        public async Task<IActionResult> ListEmployeeNotes(
            int employeeId,
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
            var onList = await _followerActions.EmployeeAppearsOnListAsync(listId, employeeId, ct);
            if (!FollowerAuthorization.CanNoteEmployee(linked, onList))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "لا يمكنك عرض ملاحظات هذا المندوب" });
            }

            // Author-only read path for followers; never exposed to salesman APIs.
            var notes = await _followerActions.ListEmployeeNotesForFollowerAsync(employeeId, father.DelegateId, ct);
            return Ok(notes);
        }

        [HttpPost("Employees/{employeeId:int}/notes")]
        public async Task<IActionResult> AddEmployeeNote(
            int employeeId,
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

            if (employeeId <= 0)
            {
                return BadRequest(new { message = "المندوب غير معروف" });
            }

            var linked = await _delegateRepository.IsFollowerListLinked(father.DelegateId, body.ListId);
            var onList = await _followerActions.EmployeeAppearsOnListAsync(body.ListId, employeeId, ct);
            if (!FollowerAuthorization.CanNoteEmployee(linked, onList))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "لا يمكنك إضافة ملاحظة على مندوب خارج قوائمك" });
            }

            _ = FollowerAuthorization.AcceptClientCreatedByUserId(body.CreatedByUserId, father.DelegateId);

            var saved = await _followerActions.AddEmployeeNoteAsync(new FollowerEmployeeNoteDTO
            {
                EmployeeId = employeeId,
                ListId = body.ListId > 0 ? body.ListId : null,
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

            if (body.CustomerId <= 0)
            {
                return BadRequest(new { message = "الزبون مطلوب" });
            }

            var denied = await EnsureCustomerInScope(father.DelegateId, body.ListId, body.CustomerId, ct);
            if (denied != null)
            {
                return denied;
            }

            var scope = await _followerActions.GetCustomerScopeAsync(body.CustomerId, ct);
            if (scope == null)
            {
                return NotFound(new { message = "الزبون غير موجود" });
            }

            var name = (body.FullName ?? scope.Value.CustomerName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "اسم الزبون مطلوب" });
            }

            var phone = string.IsNullOrWhiteSpace(body.Phone) ? scope.Value.Phone : body.Phone.Trim();
            var address = string.IsNullOrWhiteSpace(body.Address) ? scope.Value.Address : body.Address.Trim();
            var province = string.IsNullOrWhiteSpace(body.Province) ? scope.Value.CityName : body.Province.Trim();

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
                body.CustomerId,
                scope.Value.CityName,
                scope.Value.CityName,
                ct);

            return Ok(saved);
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
