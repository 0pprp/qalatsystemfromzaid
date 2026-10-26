using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersPaymentsController : ControllerBase
    {
        private readonly ICustomersPaymentsRepository _customersPaymentsRepository;
        private readonly IDelegateRepository _delegates;
        private readonly IFollowerActionsRepository _followerActions;

        public CustomersPaymentsController(
            ICustomersPaymentsRepository customersPaymentsRepository,
            IDelegateRepository delegates,
            IFollowerActionsRepository followerActions)
        {
            _customersPaymentsRepository = customersPaymentsRepository;
            _delegates = delegates;
            _followerActions = followerActions;
        }

        private async Task<(DelegateGetDTO? auth, IActionResult? error)> AuthenticateAsync()
        {
            var credential = DelegateComplaintAuth.ReadAsyncIdCredential(Request, bodyAsyncId: null);
            var expected = DelegateComplaintAuth.ReadExpectedDelegateId(Request);
            var result = await DelegateComplaintAuth.AuthenticateAsync(_delegates, credential, expected);
            if (!result.Ok)
            {
                return (null, StatusCode(result.StatusCode, new { message = result.ErrorMessage }));
            }

            return (result.Delegate, null);
        }

        /// <summary>
        /// Aqeel offline Sync historically reads payments without X-Async-Id.
        /// </summary>
        private async Task<(DelegateGetDTO? auth, IActionResult? error, bool legacyUnauthenticated)> AuthenticateOrLegacyReadAsync()
        {
            var credential = DelegateComplaintAuth.ReadAsyncIdCredential(Request, bodyAsyncId: null);
            if (string.IsNullOrWhiteSpace(credential))
            {
                return (null, null, true);
            }

            var (auth, error) = await AuthenticateAsync();
            return (auth, error, false);
        }

        private async Task<IActionResult?> EnsureOwnsCustomerAsync(int delegateId, int customerId, CancellationToken ct)
        {
            if (customerId <= 0)
            {
                return BadRequest(new { message = "الزبون مطلوب" });
            }

            var scope = await _followerActions.GetCustomerScopeAsync(customerId, ct);
            if (scope == null)
            {
                return NotFound(new { message = "الزبون غير موجود" });
            }

            if (scope.Value.DelegateId == delegateId)
            {
                return null;
            }

            var children = await _delegates.GetDelegateSelect(delegateId) ?? [];
            if (children.Any(c =>
                    c.DelegateChildId == scope.Value.DelegateId || c.DelegateId == scope.Value.DelegateId))
            {
                return null;
            }

            return StatusCode(StatusCodes.Status403Forbidden, new { message = "الزبون خارج نطاق قوائم المندوب" });
        }

        [HttpGet("GetCustomersPaymentsCustomerDate/id={customerId:int}&&date={dateCreate}")]
        public async Task<IActionResult> GetCustomersPaymentsCustomerDate(
            int customerId,
            string? dateCreate,
            CancellationToken ct)
        {
            var (auth, error, legacy) = await AuthenticateOrLegacyReadAsync();
            if (error != null) return error!;

            if (!legacy)
            {
                var denied = await EnsureOwnsCustomerAsync(auth!.DelegateId, customerId, ct);
                if (denied != null) return denied;
            }

            try
            {
                if (!DateTime.TryParse(dateCreate, out var parsed))
                {
                    return BadRequest(new { message = "التاريخ غير صالح" });
                }

                var result = await _customersPaymentsRepository.GetCustomersPaymentsCustomerDate(customerId, parsed);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ أثناء معالجة الطلب" });
            }
        }

        [HttpGet("GetCustomersPaymentsCustomer/{customerId:int}")]
        public async Task<IActionResult> GetCustomersPaymentsCustomer(
            int customerId,
            CancellationToken ct)
        {
            var (auth, error, legacy) = await AuthenticateOrLegacyReadAsync();
            if (error != null) return error!;

            if (!legacy)
            {
                var denied = await EnsureOwnsCustomerAsync(auth!.DelegateId, customerId, ct);
                if (denied != null) return denied;
            }

            try
            {
                var result = await _customersPaymentsRepository.GetCustomersPaymentsCustomer(customerId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ أثناء معالجة الطلب" });
            }
        }

        [HttpGet("GetCustomersPaymentsCustomerName/id={id:int}&&customerName={customerName}")]
        public async Task<IActionResult> GetCustomersPaymentsCustomerName(
            int id,
            string? customerName,
            CancellationToken ct)
        {
            var (auth, error, legacy) = await AuthenticateOrLegacyReadAsync();
            if (error != null) return error!;

            if (!legacy)
            {
                var denied = await EnsureOwnsCustomerAsync(auth!.DelegateId, id, ct);
                if (denied != null) return denied;
            }

            try
            {
                var result = await _customersPaymentsRepository.GetCustomersPaymentsCustomerName(id, customerName);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ أثناء معالجة الطلب" });
            }
        }
    }
}
