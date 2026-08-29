using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DelegatesController : ControllerBase
    {
        private readonly IDelegateRepository _delegateRepository;

        public DelegatesController(IDelegateRepository delegateRepository)
        {
            _delegateRepository = delegateRepository;
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
    }
}
