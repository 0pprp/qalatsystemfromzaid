using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class StatisticsController : ControllerBase
    {
        private readonly IStatisticsAppRepository _statisticsAppRepository;

        public StatisticsController(IStatisticsAppRepository statisticsAppRepository)
        {
            _statisticsAppRepository = statisticsAppRepository;
        }

        [HttpGet("StatisticsApp_GetAll")]
        public async Task<ActionResult<StatisticsAppGetDTO>> StatisticsApp_GetAll()
        {
            try
            {
                var result = await _statisticsAppRepository.StatisticsApp_GetAll();
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }
    }
}
