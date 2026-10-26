using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Mvc;

namespace BE_DelegateWebApplication.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DelegatesController : ControllerBase
    {
        private readonly IDelegateRepository _delegateRepository;
        private readonly IFollowerActionsRepository _followerActions;
        private readonly IWrongSaleExceptionForwarder _wrongSaleForwarder;
        private readonly IDelegateFirstCollectionEvalService _firstCollectionEval;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DelegatesController> _logger;

        public DelegatesController(
            IDelegateRepository delegateRepository,
            IFollowerActionsRepository followerActions,
            IWrongSaleExceptionForwarder wrongSaleForwarder,
            IDelegateFirstCollectionEvalService firstCollectionEval,
            IServiceScopeFactory scopeFactory,
            ILogger<DelegatesController> logger)
        {
            _delegateRepository = delegateRepository;
            _followerActions = followerActions;
            _wrongSaleForwarder = wrongSaleForwarder;
            _firstCollectionEval = firstCollectionEval;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpGet("GetDelegateLogin/asyncId={asyncID}")]
        public async Task<ActionResult<DelegateGetDTO?>> GetDelegateLogin(string? asyncID)
        {
            try
            {
                var result = await _delegateRepository.GetDelegateLogin(asyncID?.Trim().TrimEnd('/'));
                if (result != null)
                {
                    if (result.FollowerCollectionEnabled == true)
                    {
                        return Unauthorized(new { message = FollowerCollectionLoginGate.LockedMessage });
                    }
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
                    // Session heartbeat: kick when accountant switched collection to follower.
                    var login = await _delegateRepository.GetDelegateLogin(asyncID?.Trim().TrimEnd('/'));
                    if (login?.FollowerCollectionEnabled == true)
                    {
                        return Unauthorized(new { message = FollowerCollectionLoginGate.LockedMessage });
                    }
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

        /// <summary>
        /// Delegate inbox of sales requests they originated.
        /// tab: all | inProgress | rejected | completed
        /// </summary>
        [HttpGet("SalesRequests")]
        public async Task<IActionResult> ListSalesRequests(
            [FromQuery] string? asyncId,
            [FromQuery] string? tab,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(asyncId))
            {
                return BadRequest(new { message = "رمز المندوب مطلوب" });
            }

            var auth = await _delegateRepository.GetDelegateLogin(asyncId.Trim().TrimEnd('/'));
            if (auth == null || auth.DelegateId <= 0)
            {
                return Unauthorized(new { message = "رمز المندوب غير صحيح" });
            }

            try
            {
                var items = await _followerActions.ListSalesRequestsForDelegateAsync(auth.DelegateId, tab, ct);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Delegate sales request — same SalesRequests table/contract as Followers/SalesRequests.
        /// SaleRequestType: New (Home) or Old (customer card). Province from server only.
        /// </summary>
        [HttpPost("SalesRequests")]
        public async Task<IActionResult> SubmitSalesRequest([FromBody] DelegateSalesRequestCreateDTO body, CancellationToken ct)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.AsyncId))
            {
                return BadRequest(new { message = "رمز المندوب مطلوب" });
            }

            var auth = await _delegateRepository.GetDelegateLogin(body.AsyncId.Trim().TrimEnd('/'));
            if (auth == null || auth.DelegateId <= 0)
            {
                return Unauthorized(new { message = "رمز المندوب غير صحيح" });
            }

            var kind = SaleRequestTypes.Normalize(body.SaleRequestType, body.CustomerId is > 0);
            if (!SaleRequestTypes.IsNew(body.SaleRequestType) && !SaleRequestTypes.IsOld(body.SaleRequestType))
            {
                // Infer only when client omitted; if present must be New|Old.
                if (!string.IsNullOrWhiteSpace(body.SaleRequestType))
                {
                    return BadRequest(new { message = "SaleRequestType يجب أن يكون New أو Old" });
                }
            }

            var listId = body.ListId > 0 ? body.ListId : auth.DelegateId;

            // Delegate may only use own list id (or a SelectDelegate-linked child).
            if (listId != auth.DelegateId)
            {
                var linked = await _delegateRepository.IsFollowerListLinked(auth.DelegateId, listId);
                if (!linked)
                {
                    // IsFollowerListLinked also checks Users path; for pure delegates use SelectDelegate SP via GetDelegateSelect.
                    var children = await _delegateRepository.GetDelegateSelect(auth.DelegateId) ?? [];
                    if (!children.Any(c => c.DelegateChildId == listId || c.DelegateId == listId))
                    {
                        return StatusCode(StatusCodes.Status403Forbidden, new { message = "القائمة غير مرتبطة بحسابك" });
                    }
                }
            }

            string? province = null;
            if (auth.CityId is > 0)
            {
                province = await _followerActions.GetCityNameByIdAsync(auth.CityId.Value, ct);
            }
            if (string.IsNullOrWhiteSpace(province))
            {
                province = await _followerActions.GetFollowerCityNameAsync(auth.DelegateId, ct);
            }

            string name;
            string? phone;
            string? address;
            int? existingId = null;

            if (SaleRequestTypes.IsOld(kind))
            {
                if (body.CustomerId is not > 0)
                {
                    return BadRequest(new { message = "CustomerID مطلوب لطلب المبيع القديم" });
                }

                var scope = await _followerActions.GetCustomerScopeAsync(body.CustomerId.Value, ct);
                if (scope == null)
                {
                    return NotFound(new { message = "الزبون غير موجود" });
                }

                // Security: customer must belong to this delegate list (or linked child list).
                if (scope.Value.DelegateId != auth.DelegateId && scope.Value.DelegateId != listId)
                {
                    var children = await _delegateRepository.GetDelegateSelect(auth.DelegateId) ?? [];
                    var allowed = children.Any(c =>
                        c.DelegateChildId == scope.Value.DelegateId || c.DelegateId == scope.Value.DelegateId);
                    if (!allowed)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden,
                            new { message = "الزبون خارج قوائم المندوب — لا يمكن إرسال الطلب" });
                    }
                }

                existingId = body.CustomerId.Value;
                name = (scope.Value.CustomerName ?? string.Empty).Trim();
                phone = scope.Value.Phone?.Trim();
                address = string.IsNullOrWhiteSpace(scope.Value.Address)
                    ? "—"
                    : scope.Value.Address.Trim();
                if (!string.IsNullOrWhiteSpace(scope.Value.CityName))
                {
                    province = scope.Value.CityName;
                }
                else if (string.IsNullOrWhiteSpace(province) && auth.CityId is > 0)
                {
                    province = await _followerActions.GetCityNameByIdAsync(auth.CityId.Value, ct);
                }
            }
            else
            {
                name = (body.FullName ?? string.Empty).Trim();
                phone = body.Phone?.Trim().Replace(" ", string.Empty);
                address = body.Address?.Trim();

                if (!FollowerAuthorization.IsValidFollowerPhone(phone))
                {
                    return BadRequest(new { message = "رقم الهاتف يجب أن يكون 11 رقماً ويبدأ بـ 07" });
                }

                if (string.IsNullOrWhiteSpace(address))
                {
                    return BadRequest(new { message = "العنوان مطلوب" });
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "اسم الزبون مطلوب" });
            }

            if (string.IsNullOrWhiteSpace(province))
            {
                return BadRequest(new { message = "محافظة المندوب غير معرّفة في النظام" });
            }

            // Ignore any client province.
            _ = body.Province;

            int? shopRating = null;
            string? shopRatingNote = null;
            decimal? shopStock = null;
            decimal? dailyRevenue = null;
            if (SaleRequestTypes.IsNew(kind))
            {
                shopRating = body.ShopRating;
                shopRatingNote = body.ShopRatingNote;
                shopStock = body.ShopStockEstimatedValue;
                dailyRevenue = body.EstimatedDailyRevenue;

                if (shopRating is not int rating || !FollowerSaleAuthRules.IsValidRating(rating))
                {
                    return BadRequest(new { message = "تقييم المحل مطلوب (من 1 إلى 10) للمبيع الجديد" });
                }

                if (!FollowerSaleAuthRules.IsValidNoteForRating(rating, shopRatingNote))
                {
                    return BadRequest(new { message = "الملاحظة إجبارية عندما يكون تقييم المحل أقل من 7" });
                }

                if (shopStock is not decimal stock || !FollowerSaleAuthRules.IsValidShopMoney(stock))
                {
                    return BadRequest(new { message = "قيمة بضاعة المحل مطلوبة ويجب أن تكون أكبر من صفر" });
                }

                if (dailyRevenue is not decimal daily || !FollowerSaleAuthRules.IsValidShopMoney(daily))
                {
                    return BadRequest(new { message = "واردات المحل اليومية مطلوبة ويجب أن تكون أكبر من صفر" });
                }

                var evalBlock = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                    rating, shopRatingNote, stock, daily, name, phone,
                    evaluatorRole: "مندوب الجباية");
                body.Notes = string.IsNullOrWhiteSpace(body.Notes)
                    ? evalBlock
                    : $"{evalBlock}\n{body.Notes.Trim()}";
            }

            var now = DateTime.UtcNow;
            var listName = await _followerActions.GetDelegateNameAsync(listId, ct);
            var notes = string.IsNullOrWhiteSpace(body.Notes) ? null : body.Notes.Trim();
            if (!string.IsNullOrWhiteSpace(listName))
            {
                var listTag = $"القائمة: {listName} (#{listId})";
                notes = string.IsNullOrWhiteSpace(notes) ? listTag : $"{listTag}\n{notes}";
            }

            var saved = await _followerActions.InsertSalesRequestAsync(
                new FollowerSalesRequestResultDTO
                {
                    CreatedByUserId = auth.DelegateId,
                    CreatedByName = auth.DelegateName?.Trim() is { Length: > 0 } n ? n : "مندوب",
                    CreatedByUserType = "مندوب",
                    CreatedAtUtc = now
                },
                name,
                phone,
                province,
                address ?? "",
                notes,
                existingId,
                province,
                province,
                customerSourceType: "Delegate",
                saleRequestType: kind,
                sourceListId: listId,
                shopRating: shopRating,
                shopRatingNote: shopRatingNote,
                shopStockEstimatedValue: shopStock,
                estimatedDailyRevenue: dailyRevenue,
                ct: ct);

            // Best-effort: push WrongSale into DM exception inbox (لا يفشل طلب المبيع إن تعطّلت البوابة).
            if (saved.IsWrongSale && notes != null)
            {
                var exceptionReason = notes.Contains("مبيعات خاطئة", StringComparison.Ordinal)
                    ? notes
                    : $"مبيعات خاطئة\n{notes}";
                _ = await _wrongSaleForwarder.ForwardAsync(new WrongSaleExceptionForwardRequest
                {
                    CityValue = province,
                    CityName = province,
                    CustomerId = existingId,
                    CustomerName = name,
                    CustomerPhone = phone,
                    SalesRequestId = saved.Id,
                    Reason = exceptionReason,
                    RequesterUserId = auth.DelegateId.ToString(),
                    RequesterUserName = auth.DelegateName,
                    RequesterDisplayName = string.IsNullOrWhiteSpace(auth.DelegateName)
                        ? "مندوب"
                        : auth.DelegateName.Trim()
                }, ct);
            }

            return Ok(saved);
        }

        /// <summary>
        /// تقييم المحل عند أول تسديد للمبيعات الجديدة — يُحفظ في البروفايل؛
        /// تقييم ≤6 يُرسل كمبيع خاطئ لإشعارات المدير المفوض.
        /// </summary>
        [HttpPost("FirstCollectionEvaluation")]
        public async Task<IActionResult> FirstCollectionEvaluation(
            [FromBody] DelegateFirstCollectionEvalCreateDTO? body,
            CancellationToken ct)
        {
            try
            {
                if (body == null || string.IsNullOrWhiteSpace(body.AsyncId))
                {
                    return BadRequest(new { message = "رمز المندوب مطلوب" });
                }

                if (body.CustomerId <= 0)
                {
                    return BadRequest(new { message = "معرف الزبون مطلوب" });
                }

                var auth = await _delegateRepository.GetDelegateLogin(body.AsyncId.Trim().TrimEnd('/'));
                if (auth == null || auth.DelegateId <= 0)
                {
                    return Unauthorized(new { message = "رمز المندوب غير صحيح" });
                }

                var listId = body.ListId > 0 ? body.ListId : auth.DelegateId;
                if (listId != auth.DelegateId)
                {
                    bool linked;
                    try
                    {
                        linked = await _delegateRepository.IsFollowerListLinked(auth.DelegateId, listId);
                    }
                    catch (Exception ex)
                    {
                        // Missing Followers_IsLinked SP (or similar) must not block shop eval —
                        // fall through to SelectDelegate ownership check below.
                        _logger.LogWarning(ex,
                            "IsFollowerListLinked failed for delegate {DelegateId} list {ListId}; using GetDelegateSelect",
                            auth.DelegateId, listId);
                        linked = false;
                    }

                    if (!linked)
                    {
                        var children = await _delegateRepository.GetDelegateSelect(auth.DelegateId) ?? [];
                        if (!children.Any(c => c.DelegateChildId == listId || c.DelegateId == listId))
                        {
                            return StatusCode(StatusCodes.Status403Forbidden, new { message = "القائمة غير مرتبطة بحسابك" });
                        }
                    }
                }

                var scope = await _followerActions.GetCustomerScopeAsync(body.CustomerId, ct);
                if (scope == null)
                {
                    return NotFound(new { message = "الزبون غير موجود" });
                }

                if (scope.Value.DelegateId != auth.DelegateId && scope.Value.DelegateId != listId)
                {
                    var children = await _delegateRepository.GetDelegateSelect(auth.DelegateId) ?? [];
                    var allowed = children.Any(c =>
                        c.DelegateChildId == scope.Value.DelegateId || c.DelegateId == scope.Value.DelegateId);
                    if (!allowed)
                    {
                        return StatusCode(StatusCodes.Status403Forbidden,
                            new { message = "الزبون خارج قوائم المندوب" });
                    }
                }

                DelegateFirstCollectionEvalRow row;
                bool created;
                try
                {
                    (row, created) = await _firstCollectionEval.SubmitAsync(
                        auth.DelegateId,
                        auth.DelegateName,
                        body.CustomerId,
                        listId,
                        body.Rating,
                        body.Note,
                        body.ShopStockEstimatedValue,
                        body.EstimatedDailyRevenue,
                        body.SaleAmount,
                        ct);
                }
                catch (InvalidOperationException ex)
                {
                    return BadRequest(new { message = ex.Message });
                }

                var isWrong = FollowerSaleAuthRules.IsWrongSaleRating(row.Rating);
                // Never block the delegate APK on gateway latency — forward in a fresh DI scope.
                // Gateway dedupes pending WrongSale for the same customer (safe to re-queue).
                if (isWrong)
                {
                    string? province = scope.Value.CityName;
                    if (string.IsNullOrWhiteSpace(province) && auth.CityId is > 0)
                    {
                        province = await _followerActions.GetCityNameByIdAsync(auth.CityId.Value, ct);
                    }

                    var reason = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                        row.Rating,
                        row.Note,
                        row.ShopStockEstimatedValue,
                        row.EstimatedDailyRevenue,
                        scope.Value.CustomerName,
                        scope.Value.Phone,
                        row.SaleAmount,
                        evaluatorRole: "مندوب الجباية",
                        includeWrongSaleTag: true);

                    var forwardRequest = new WrongSaleExceptionForwardRequest
                    {
                        CityValue = province,
                        CityName = province,
                        CustomerId = body.CustomerId,
                        CustomerName = scope.Value.CustomerName,
                        CustomerPhone = scope.Value.Phone,
                        Reason = reason,
                        RequesterUserId = auth.DelegateId.ToString(),
                        RequesterUserName = auth.DelegateName,
                        RequesterDisplayName = string.IsNullOrWhiteSpace(auth.DelegateName)
                            ? "مندوب الجباية"
                            : auth.DelegateName.Trim()
                    };

                    _ = ForwardWrongSaleInBackgroundAsync(forwardRequest);
                }

                return Ok(new DelegateFirstCollectionEvalResultDTO
                {
                    Id = row.Id,
                    CustomerId = row.CustomerId,
                    Rating = row.Rating,
                    Note = row.Note,
                    ShopStockEstimatedValue = row.ShopStockEstimatedValue,
                    EstimatedDailyRevenue = row.EstimatedDailyRevenue,
                    SaleAmount = row.SaleAmount,
                    IsWrongSale = isWrong,
                    Created = created,
                    CreatedAtUtc = row.CreatedAtUtc
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "FirstCollectionEvaluation failed customer={CustomerId} list={ListId}",
                    body?.CustomerId, body?.ListId);
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    message = "تعذر حفظ تقييم المحل على السيرفر. أعد المحاولة أو راجع إدارة النظام.",
                    detail = ex.Message
                });
            }
        }

        private async Task ForwardWrongSaleInBackgroundAsync(WrongSaleExceptionForwardRequest request)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var forwarder = scope.ServiceProvider.GetRequiredService<IWrongSaleExceptionForwarder>();
                var result = await forwarder.ForwardAsync(request, CancellationToken.None);
                if (!result.Ok)
                {
                    _logger.LogWarning(
                        "Background WrongSale forward failed for customer {CustomerId}: {Status} {Error}",
                        request.CustomerId,
                        result.FailureStatusCode,
                        result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background WrongSale forward crashed for customer {CustomerId}", request.CustomerId);
            }
        }
    }
}
