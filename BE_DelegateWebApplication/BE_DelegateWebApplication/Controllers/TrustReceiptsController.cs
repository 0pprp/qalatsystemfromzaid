using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrustReceiptsController : ControllerBase
    {
        private readonly ITrustReceiptRepository _repository;
        private readonly IDelegateRepository _delegates;

        public TrustReceiptsController(ITrustReceiptRepository repository, IDelegateRepository delegates)
        {
            _repository = repository;
            _delegates = delegates;
        }

        private async Task<(DelegateGetDTO? auth, IActionResult? error)> AuthenticateAsync(string? bodyAsyncId = null)
        {
            var credential = DelegateComplaintAuth.ReadAsyncIdCredential(Request, bodyAsyncId);
            var expected = DelegateComplaintAuth.ReadExpectedDelegateId(Request);
            var result = await DelegateComplaintAuth.AuthenticateAsync(_delegates, credential, expected);
            if (!result.Ok)
            {
                return (null, StatusCode(result.StatusCode, new { message = result.ErrorMessage }));
            }

            return (result.Delegate, null);
        }

        private IActionResult ForbiddenOwnership() =>
            StatusCode(StatusCodes.Status403Forbidden, new { message = "الوصول محظور خارج نطاق المندوب" });

        private static bool OwnsReceipt(DelegateGetDTO auth, TrustReceiptDTO? receipt) =>
            receipt is not null &&
            receipt.DelegateID is int owner &&
            owner > 0 &&
            owner == auth.DelegateId;

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] TrustReceiptDTO dto)
        {
            var (auth, error) = await AuthenticateAsync();
            if (error != null) return error!;

            try
            {
                // Actor and ownership from session — never from client body.
                dto.DelegateID = auth!.DelegateId;
                dto.CreatedByUserID = auth.UserId is > 0 ? auth.UserId : auth.DelegateId;
                dto.UpdatedByUserID = null;

                var id = await _repository.CreateAsync(dto);
                return Ok(id);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ أثناء معالجة الطلب" });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] TrustReceiptDTO dto)
        {
            var (auth, error) = await AuthenticateAsync();
            if (error != null) return error!;

            try
            {
                var existing = await _repository.GetByIdAsync(id);
                if (existing == null) return NotFound();
                if (!OwnsReceipt(auth!, existing)) return ForbiddenOwnership();

                dto.TrustReceiptID = id;
                dto.DelegateID = auth!.DelegateId;
                dto.UpdatedByUserID = auth.UserId is > 0 ? auth.UserId : auth.DelegateId;
                dto.CreatedByUserID = existing.CreatedByUserID;

                var success = await _repository.UpdateAsync(dto);
                return success ? Ok() : NotFound();
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ أثناء معالجة الطلب" });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var (auth, error) = await AuthenticateAsync();
            if (error != null) return error!;

            try
            {
                var existing = await _repository.GetByIdAsync(id);
                if (existing == null) return NotFound();
                if (!OwnsReceipt(auth!, existing)) return ForbiddenOwnership();

                var actorUserId = auth!.UserId is > 0 ? auth.UserId : auth.DelegateId;
                var success = await _repository.DeleteAsync(id, actorUserId);
                return success ? Ok() : NotFound();
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ أثناء معالجة الطلب" });
            }
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var (auth, error) = await AuthenticateAsync();
            if (error != null) return error!;

            try
            {
                var result = await _repository.GetByIdAsync(id);
                if (result == null) return NotFound();
                if (!OwnsReceipt(auth!, result)) return ForbiddenOwnership();
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ أثناء معالجة الطلب" });
            }
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetPaged(
            [FromQuery] string? searchTerm = null,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            var (auth, error) = await AuthenticateAsync();
            if (error != null) return error!;

            try
            {
                // Always scope to authenticated delegate — ignore client delegateId.
                var result = await _repository.GetPagedAsync(searchTerm, pageNumber, pageSize, auth!.DelegateId);
                return Ok(result);
            }
            catch (Exception)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "حدث خطأ أثناء معالجة الطلب" });
            }
        }
    }
}
