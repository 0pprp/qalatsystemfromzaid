using BE_Company.IRepository;
using BE_Company.DTO;
using BE_Company.Services.LegalSearch;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using BE_Company.Repository;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersController : ControllerBase
    {
        private readonly ICustomersRepository _customersRepository;
        private readonly ILegalCustomerSearchService _legalSearch;

        public CustomersController(
            ICustomersRepository customersRepository,
            ILegalCustomerSearchService legalSearch)
        {
            _customersRepository = customersRepository;
            _legalSearch = legalSearch;
        }

        [HttpPost("CustomersSalesCustomer_Create")]
        public async Task<ActionResult<CustomersSalesGetDTO?>> CustomersSalesCustomer_Create([FromBody] CustomersSalesPostDTO customersSalesPostDTO)
        {
            try
            {
                int? userID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }

                customersSalesPostDTO.UserCreateID = userID;
                var result = await _customersRepository.CustomersSalesCustomer_Create(customersSalesPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }


        [HttpGet("Customers_GetAll/{delegateID}&&{textSearch}&&{showType}")]
        public async Task<ActionResult<IEnumerable<CustomersGetDTO>>> Customers_GetAll(int? delegateID, string? textSearch, string? showType)
        {
            try
            {
                string? text_Search = textSearch != "null" ? textSearch : null;
                int? delegate_ID = delegateID != 0 ? delegateID : null;
                var result = await _customersRepository.Customers_GetAll(delegate_ID, text_Search, showType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Customers_GetByLastPaymentDate/{fromDate}&&{toDate}")]
        public async Task<ActionResult<IEnumerable<CustomersGetDTO>>> Customers_GetByLastPaymentDate(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var result = await _customersRepository.Customers_GetByLastPaymentDate(fromDate, toDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Customers_GetAllZero/{fromDate}&&{toDate}")]
        public async Task<ActionResult<IEnumerable<CustomersGetDTO>>> Customers_GetAllZero(DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                var result = await _customersRepository.Customers_GetAllZero(fromDate, toDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Customers_GetWeekReceipt/{delegateID}&&{showType}")]
        public async Task<ActionResult<IEnumerable<CustomerWeekPaymentsGetDTO>>> Customers_GetWeekReceipt(int? delegateID, string? showType)
        {
            try
            {
                // FE sends 0 for "all delegates"; SP treats only NULL as all.
                int? delegate_ID = delegateID is > 0 ? delegateID : null;
                var result = await _customersRepository.Customers_GetWeekReceipt(delegate_ID, showType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Customers_GetMonthReceipt/{delegateID}&&{showType}")]
        public async Task<ActionResult<IEnumerable<CustomerMonthPaymentsGetDTO>>> Customers_GetMonthReceipt(int? delegateID, string? showType)
        {
            try
            {
                int? delegate_ID = delegateID is > 0 ? delegateID : null;
                var result = await _customersRepository.Customers_GetMonthReceipt(delegate_ID, showType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Customers_Follow/{delegateID}&&{paymentDate}&&{showType}")]
        public async Task<ActionResult<IEnumerable<CustomersFollowGetDTO>>> Customers_Follow(int? delegateID, DateTime? paymentDate, string? showType)
        {
            try
            {
                var result = await _customersRepository.Customers_Follow(delegateID, paymentDate, showType);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("Customers_InfoSimple/{customerID}")]
        public async Task<ActionResult<CustomersInfoSimpleGetDTO>> Customers_InfoSimple(int? customerID)
        {
            try
            {
                var result = await _customersRepository.Customers_InfoSimple(customerID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Customers_Update/{customerID}")]
        public async Task<ActionResult<CustomersSalesGetDTO?>> Customers_Update(int? customerID, [FromBody] CustomersPutDTO customersPutDTO)
        {
            try
            {
                int? userID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }

                customersPutDTO.UserUpdateID = userID;
                var result = await _customersRepository.Customers_Update(customerID, customersPutDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Customers_Move/{customerID}&&{delegateID}")]
        public async Task<ActionResult<CustomersSalesGetDTO?>> Customers_Move(int? customerID, int? delegateID)
        {
            try
            {
                int? userID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }

                var result = await _customersRepository.Customers_Move(customerID, delegateID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("Customers_MoveLegal/{customerID}")]
        public async Task<ActionResult<bool?>> Customers_MoveLegal(int? customerID)
        {
            try
            {
                int? userID = null;
                if (HttpContext.Items["UserID"] is string userIdStr && int.TryParse(userIdStr, out int parsedUserId))
                {
                    userID = parsedUserId;
                }
                if (!userID.HasValue)
                {
                    return Unauthorized("User is not authenticated.");
                }

                var result = await _customersRepository.Customers_MoveLegal(customerID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        /// <summary>
        /// بحث القانونية: استيراد أسماء/هواتف من إكسل العملاء → تطابق اسم ثلاثي كامل + هاتف (محافظة الفرع فقط).
        /// </summary>
        [HttpPost("LegalExcelSearch")]
        public async Task<IActionResult> LegalExcelSearch(
            [FromBody] LegalExcelSearchRequest? body,
            CancellationToken ct)
        {
            try
            {
                body ??= new LegalExcelSearchRequest();
                var queries = body.Queries ?? new List<LegalSearchQueryInput>();
                if (queries.Count == 0)
                {
                    return BadRequest(new { message = "لا توجد صفوف للبحث" });
                }

                var result = await _legalSearch.SearchAsync(queries, ct);
                return Ok(new
                {
                    nameHitCount = result.NameHitCount,
                    phoneHitCount = result.PhoneHitCount,
                    uniqueCustomerCount = result.UniqueCustomerCount,
                    queries = result.Queries.Select(q => new
                    {
                        index = q.Index,
                        requestedName = q.RequestedName,
                        requestedPhone = q.RequestedPhone,
                        nameMatches = q.NameMatches,
                        phoneMatches = q.PhoneMatches,
                        nameMatchCount = q.NameMatches.Count,
                        phoneMatchCount = q.PhoneMatches.Count
                    }),
                    exportRows = result.ExportRows.Select(r => new
                    {
                        matchKinds = r.MatchKinds,
                        sourceQueryIndex = r.SourceQueryIndex,
                        requestedName = r.RequestedName,
                        requestedPhone = r.RequestedPhone,
                        customer = r.Customer
                    })
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        public sealed class LegalExcelSearchRequest
        {
            public List<LegalSearchQueryInput>? Queries { get; set; }
        }
    }
}
