using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomersRepository _customersRepository;
        private readonly IDelegateRepository _delegates;
        private readonly IFollowerActionsRepository _followerActions;

        public CustomersController(
            ICustomersRepository customersRepository,
            IDelegateRepository delegates,
            IFollowerActionsRepository followerActions)
        {
            _customersRepository = customersRepository;
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
        /// Aqeel/منهاج Sync app historically called customer GETs without X-Async-Id.
        /// When credential is present → enforce auth + list scope (Haider path).
        /// When absent → allow read-only list/week (legacy Sync) so customers appear again.
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
                return BadRequest(new { message = "?????? ?????" });
            }

            var scope = await _followerActions.GetCustomerScopeAsync(customerId, ct);
            if (scope == null)
            {
                return NotFound(new { message = "?????? ??? ?????" });
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

            return StatusCode(StatusCodes.Status403Forbidden, new { message = "?????? ???? ???? ????? ???????" });
        }

        private async Task<IActionResult?> EnsureSameDelegateAsync(DelegateGetDTO auth, int? requestedDelegateId)
        {
            if (requestedDelegateId is null or <= 0)
            {
                return BadRequest(new { message = "??????? ?????" });
            }

            if (requestedDelegateId == auth.DelegateId)
            {
                return null;
            }

            var children = await _delegates.GetDelegateSelect(auth.DelegateId) ?? [];
            if (children.Any(c =>
                    c.DelegateChildId == requestedDelegateId || c.DelegateId == requestedDelegateId))
            {
                return null;
            }

            return StatusCode(StatusCodes.Status403Forbidden, new { message = "?????? ????? ???? ???? ???????" });
        }

        /// <summary>
        /// Customer file/profile. Aqeel offline Sync app calls this without X-Async-Id.
        /// </summary>
        public const string AqeelLegacyCustomerFileMarker = "AQEEL_LEGACY_CUSTOMER_FILE_V1";

        [HttpGet("GetCustomersDataInfo/{customerId:int}")]
        public async Task<IActionResult> GetCustomersDataInfo(int customerId, CancellationToken ct)
        {
            var (auth, error, legacy) = await AuthenticateOrLegacyReadAsync();
            if (error != null) return error!;

            if (!legacy)
            {
                var denied = await EnsureOwnsCustomerAsync(auth!.DelegateId, customerId, ct);
                if (denied != null) return denied;
            }
            else
            {
                // Keep deploy marker string alive in release binaries (Aqeel offline Sync).
                Response.Headers["X-Qalaa-Compat"] = AqeelLegacyCustomerFileMarker;
            }

            try
            {
                var result = await _customersRepository.GetCustomersDataInfo(customerId);
                if (result != null)
                {
                    return Ok(result);
                }

                return NotFound(new { message = "?????? ??? ?????" });
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "??? ??? ????? ?????? ?????" });
            }
        }

        [HttpGet("GetCustomersDelegateAll/{delegateId:int}")]
        public async Task<IActionResult> GetCustomersDelegateAll(int delegateId)
        {
            var (auth, error, legacy) = await AuthenticateOrLegacyReadAsync();
            if (error != null) return error!;

            if (!legacy)
            {
                var denied = await EnsureSameDelegateAsync(auth!, delegateId);
                if (denied != null) return denied;
            }

            try
            {
                var result = await _customersRepository.GetCustomersDelegateAll(delegateId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "??? ??? ????? ?????? ?????" });
            }
        }

        [HttpGet("GetCustomersByDelegatePermissions/{delegateId:int}")]
        public async Task<IActionResult> GetCustomersByDelegatePermissions(int delegateId)
        {
            var (auth, error, legacy) = await AuthenticateOrLegacyReadAsync();
            if (error != null) return error!;

            if (!legacy)
            {
                var denied = await EnsureSameDelegateAsync(auth!, delegateId);
                if (denied != null) return denied;
            }

            try
            {
                var result = await _customersRepository.GetCustomersByDelegatePermissions(delegateId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "??? ??? ????? ?????? ?????" });
            }
        }

        [HttpGet("GetDateWeek")]
        public async Task<IActionResult> GetDateWeek()
        {
            var (_, error, _) = await AuthenticateOrLegacyReadAsync();
            if (error != null) return error!;

            try
            {
                var result = await _customersRepository.GetDateWeek();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "??? ??? ????? ?????? ?????" });
            }
        }
    }
}
