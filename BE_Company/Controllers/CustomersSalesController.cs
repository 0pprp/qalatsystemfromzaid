using BE_Company.IRepository;
using BE_Company.DTO;
using BE_Company.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace BE_Company.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class CustomersSalesController : ControllerBase
    {
        private readonly ICustomersSalesRepository _customersSalesRepository;
        private readonly IConfiguration _configuration;

        public CustomersSalesController(ICustomersSalesRepository customersSalesRepository, IConfiguration configuration)
        {
            _customersSalesRepository = customersSalesRepository;
            _configuration = configuration;
        }


        [HttpPost("CustomersSales_Create")]
        public async Task<ActionResult<CustomersSalesGetDTO?>> CustomersSales_Create([FromBody] CustomersSalesPostDTO customersSalesPostDTO)
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
                var result = await _customersSalesRepository.CustomersSales_Create(customersSalesPostDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }


        [HttpDelete("CustomersSales_Delete/{customerSaleID}")]
        public async Task<ActionResult<bool?>> CustomersSales_Delete(int? customerSaleID)
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

                var result = await _customersSalesRepository.CustomersSales_Delete(customerSaleID, userID);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpPut("CustomersSales_UpdateDiscount/{customerSaleID}")]
        public async Task<ActionResult<bool?>> CustomersSales_UpdateDiscount(int? customerSaleID, DiscountDTO discountDTO)
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

                discountDTO.UserUpdateID = userID;
                var result = await _customersSalesRepository.CustomersSales_UpdateDiscount(customerSaleID, discountDTO);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }

        [HttpGet("CustomersSales_GetAll/{fromDate}&&{toDate}&&{delegateID}&&{customerName}&&{itemName}&&{saleName}")]
        public async Task<ActionResult<IEnumerable<CustomersSalesGetDTO>>> CustomersSales_GetAll(
          string? fromDate,
          string? toDate,
          int? delegateID,
          string? customerName,
          string? itemName,
          string? saleName)
        {
            try
            {
                DateTime? parsedFromDate = null;
                DateTime? parsedToDate = null;
                if (!string.IsNullOrWhiteSpace(fromDate) && !fromDate.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(fromDate, out DateTime dtFrom))
                        parsedFromDate = dtFrom;
                    else
                        return BadRequest("صيغة fromDate غير صحيحة");
                }

                if (!string.IsNullOrWhiteSpace(toDate) && !toDate.Equals("null", StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(toDate, out DateTime dtTo))
                        parsedToDate = dtTo;
                    else
                        return BadRequest("صيغة toDate غير صحيحة");
                }

                int? validDelegateID = (delegateID.HasValue && delegateID.Value != 0) ? delegateID : null;

                var result = await _customersSalesRepository.CustomersSales_GetAll(
                    parsedFromDate,
                    parsedToDate,
                    validDelegateID,
                    (customerName != "null") ? customerName : null,
                    (itemName != "null") ? itemName : null,
                    (saleName != "null") ? saleName : null);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
            }
        }


        [HttpGet("CustomersSales_GetMessage/{customerSaleID}")]
        public async Task<ActionResult<MessageDTO>> CustomersSales_GetMessage(int? customerSaleID)
        {
            try
            {
                if (!customerSaleID.HasValue)
                {
                    return BadRequest("معرف العميل غير صحيح");
                }

                var (message, portalUrl) = await SendMessageSelectMethod(customerSaleID);

                if (string.IsNullOrEmpty(message))
                {
                    return NotFound("لم يتم العثور على البيانات المطلوبة");
                }

                return new MessageDTO
                {
                    Message = message,
                    PortalUrl = portalUrl
                };
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, $"حدث خطأ أثناء معالجة الطلب: {ex.Message}");
            }
        }

        private async Task<(string message, string? portalUrl)> SendMessageSelectMethod(int? customerSaleID)
        {
            string message = string.Empty;
            var sale = await _customersSalesRepository.CustomersSales_GetByID((int?)customerSaleID);
            var data = await _customersSalesRepository.Customers_GetByCustomerID(sale?.CustomerID);

            if (data == null || string.IsNullOrEmpty(data.PhoneNumber))
            {
                return ("رقم الهاتف غير موجود أو غير صالح.", null);
            }

            // Generate encrypted token for customer portal (never expires)
            string encryptionKey = _configuration["PortalSettings:EncryptionKey"]!;
            string portalBaseUrl = _configuration["PortalSettings:PortalBaseUrl"] ?? "http://portal.alsaaeidy.com";
            int customerId = data.CustomerID ?? 0;
            string token = EncryptionHelper.CreateCustomerToken(customerId, encryptionKey, branchId: data.CityID);
            string portalUrl = $"{portalBaseUrl}/?token={token}&city={data.CityID}";

            string companyName = data.CityID == 6 || data.CityID == 18 ? "شركة المنهاج الذهبي" : "شركة قلعة الضمان";

            message = $"السلام عليكم ورحمة الله وبركاتة\nتهديكم {companyName} أطيب التحيات\nعزيزي الزبون ( {data.CustomerName} )\nنود إعلامكم بأن مشترياتكم من شركتنا بمبلغ ( {data.AmountTotalSales:#,##0} ) دينار تسدد على شكل أقساط يومية بواقع ( {data.AmountDaySales:#,##0} ) دينار.\nيمكنكم متابعة حسابكم ومبيعاتكم وتسديداتكم من خلال الرابط التالي:\n{portalUrl}\nمع التقدير";

            return (message, portalUrl);
        }
    }
}
