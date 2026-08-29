using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TrustReceiptsController : ControllerBase
    {
        private readonly ITrustReceiptRepository _repository;

        public TrustReceiptsController(ITrustReceiptRepository repository)
        {
            _repository = repository;
        }

        [HttpPost]
        public async Task<ActionResult<int>> Create([FromBody] TrustReceiptDTO dto)
        {
            try
            {
                var id = await _repository.CreateAsync(dto);
                return Ok(id);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> Update(int id, [FromBody] TrustReceiptDTO dto)
        {
            try
            {
                dto.TrustReceiptID = id;
                var success = await _repository.UpdateAsync(dto);
                if (success)
                    return Ok();
                return NotFound();
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id, [FromQuery] int? updatedByUserId)
        {
            try
            {
                var success = await _repository.DeleteAsync(id, updatedByUserId);
                if (success)
                    return Ok();
                return NotFound();
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<TrustReceiptDTO>> GetById(int id)
        {
            try
            {
                var result = await _repository.GetByIdAsync(id);
                if (result != null)
                    return Ok(result);
                return NotFound();
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("paged")]
        public async Task<ActionResult<PagedResultDTO<TrustReceiptDTO>>> GetPaged([FromQuery] string? searchTerm = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, [FromQuery] int? delegateId = null)
        {
            try
            {
                var result = await _repository.GetPagedAsync(searchTerm, pageNumber, pageSize, delegateId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
