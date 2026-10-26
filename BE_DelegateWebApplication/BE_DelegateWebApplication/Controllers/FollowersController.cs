using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using BE_DelegateWebApplication.Services.FollowerIdentity;
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
        private readonly IFollowerIdentityService _followerIdentity;
        private readonly ISharedCustomerNotesService _sharedNotes;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FollowersController> _logger;
        private readonly ILegalFollowupFollowerService _legalFollowups;
        private readonly IFollowerFieldFollowupService _fieldFollowups;
        private readonly IFollowerSaleAuthService _saleAuth;
        private readonly IDelegateFirstCollectionEvalService _delegateShopEval;
        private readonly IFollowerListStatsService _listStats;
        private readonly IWrongSaleExceptionForwarder _wrongSaleForwarder;
        private readonly IFollowerListCardsService _listCards;
        private readonly IFollowerDeclineService _decline;
        private readonly IFollowerCollectionPaymentService _collectionPayments;
        private readonly IFollowerDelegateDayRatingService _dayRatings;

        public FollowersController(
            IDelegateRepository delegateRepository,
            ICustomersRepository customersRepository,
            IFollowerActionsRepository followerActions,
            IFollowerIdentityService followerIdentity,
            ISharedCustomerNotesService sharedNotes,
            IWebHostEnvironment env,
            IConfiguration configuration,
            ILogger<FollowersController> logger,
            ILegalFollowupFollowerService legalFollowups,
            IFollowerFieldFollowupService fieldFollowups,
            IFollowerSaleAuthService saleAuth,
            IDelegateFirstCollectionEvalService delegateShopEval,
            IFollowerListStatsService listStats,
            IWrongSaleExceptionForwarder wrongSaleForwarder,
            IFollowerListCardsService listCards,
            IFollowerDeclineService decline,
            IFollowerCollectionPaymentService collectionPayments,
            IFollowerDelegateDayRatingService dayRatings)
        {
            _delegateRepository = delegateRepository;
            _customersRepository = customersRepository;
            _followerActions = followerActions;
            _followerIdentity = followerIdentity;
            _sharedNotes = sharedNotes;
            _env = env;
            _configuration = configuration;
            _logger = logger;
            _legalFollowups = legalFollowups;
            _fieldFollowups = fieldFollowups;
            _saleAuth = saleAuth;
            _delegateShopEval = delegateShopEval;
            _listStats = listStats;
            _wrongSaleForwarder = wrongSaleForwarder;
            _listCards = listCards;
            _decline = decline;
            _collectionPayments = collectionPayments;
            _dayRatings = dayRatings;
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

                var lists = await _delegateRepository.GetFollowerCityLists(father.DelegateId)
                            ?? Enumerable.Empty<SelectDelegateGetDTO>();
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

        [HttpGet("Customers/{customerId:int}/profile")]
        public async Task<IActionResult> CustomerProfile(
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

            var info = await _customersRepository.GetCustomersDataInfo(customerId);
            if (info == null)
            {
                return NotFound(new { message = "الزبون غير موجود" });
            }

            var sharedNotes = await _sharedNotes.ListAsync(customerId, ct);
            var notes = sharedNotes.Select(n => new FollowerCustomerNoteDTO
            {
                Id = n.NoteId,
                CustomerId = n.CustomerId,
                NoteText = n.NoteText,
                CreatedByUserId = n.CreatedByUserId ?? 0,
                CreatedByName = n.CreatedByName,
                CreatedByRole = "Shared",
                CreatedAtUtc = n.CreatedAtUtc
            }).ToList();
            var webRoot = _env.WebRootPath;
            var imagesBase = ImagesBaseUrl();
            var apiRoot = PublicApiRoot();
            var images = new List<FollowerProfileImageDTO>();

            var portraitUrl = FollowerCustomerMedia.TryPublicImagesUrl(imagesBase, webRoot, info.CustomerImage)
                              ?? FollowerAuthorization.BuildImageUrl(imagesBase, info.CustomerImage);
            if (!string.IsNullOrWhiteSpace(portraitUrl))
            {
                images.Add(new FollowerProfileImageDTO
                {
                    Kind = "Customer",
                    Label = FollowerCustomerMedia.DocumentTypeLabel("Customer"),
                    FileName = FollowerCustomerMedia.LeafFileName(info.CustomerImage),
                    Url = portraitUrl
                });
            }

            var docs = await _followerActions.ListCustomerSalesDocumentsAsync(
                customerId, info.CustomerName, info.PhoneNumber, ct);
            foreach (var doc in docs)
            {
                // Always use Followers document proxy for KYC docs (never /Images/{id}.jpg collision).
                var url = FollowerCustomerMedia.BuildDocumentFileApiUrl(
                    apiRoot, customerId, doc.Id, asyncId ?? string.Empty, listId);
                images.Add(new FollowerProfileImageDTO
                {
                    DocumentId = doc.Id,
                    Kind = doc.DocumentType,
                    Label = FollowerCustomerMedia.DocumentTypeLabel(doc.DocumentType),
                    FileName = doc.FileName,
                    Url = url
                });
            }

            var shop = await _followerActions.GetCustomerShopImageAsync(customerId, info.CustomerName, info.PhoneNumber, ct);
            if (shop?.ShopImageKey is { Length: > 0 })
            {
                var shopUrl = FollowerCustomerMedia.BuildShopImageApiUrl(
                    apiRoot, customerId, asyncId ?? string.Empty, listId);
                images.Add(new FollowerProfileImageDTO
                {
                    Kind = "Shop",
                    Label = FollowerCustomerMedia.DocumentTypeLabel("Shop"),
                    FileName = FollowerCustomerMedia.LeafFileName(shop.Value.ShopImageKey),
                    Url = shopUrl
                });
            }

            IReadOnlyList<FollowerCustomerPaymentDTO> payments;
            try
            {
                payments = await _followerActions.ListCustomerPaymentsAsync(customerId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Follower profile payments failed for customer {CustomerId}", customerId);
                payments = [];
            }

            var profile = new FollowerCustomerProfileDTO
            {
                CustomerId = info.CustomerId,
                CustomerName = info.CustomerName,
                PhoneNumber = info.PhoneNumber,
                Address = info.Address,
                CityName = info.CityName,
                ShopName = info.ShopName,
                StoreAddress = info.StoreAddress,
                StorePhoneNumber = info.StorePhoneNumber,
                NearestFunctionPoint = info.NearestFunctionPoint,
                Neighborhood = info.Neighborhood,
                Latitude = info.Latitude,
                Longitude = info.Longitude,
                CustomerImage = info.CustomerImage,
                CustomerImageUrl = portraitUrl,
                DelegateName = info.DelegateName,
                SaleName = info.SaleName,
                ListDelegateId = listId,
                CostTotalSales = info.CostTotalSales,
                AmountTotalSales = info.AmountTotalSales,
                AmountDaySales = info.AmountDaySales,
                AmountRemaining = info.AmountRemaining,
                ReceiptsTotal = info.ReceiptsTotal,
                AmountReceverDay = info.AmountReceverDay,
                ItemsNames = info.ItemsNames,
                DateSaleDevice = info.DateSaleDevice,
                CustomerSystemNotes = info.Notes,
                Notes = notes,
                Images = images,
                Payments = payments.ToList()
            };

            var auth = await _saleAuth.GetForCustomerAsync(customerId, listId, ct);
            if (auth != null)
            {
                profile.FollowerAuth = new FollowerAuthBlockDTO
                {
                    Rating = auth.Rating,
                    Note = auth.Note,
                    FollowerName = auth.FollowerName,
                    FollowerUserId = auth.FollowerUserId,
                    ShopStockEstimatedValue = auth.ShopStockEstimatedValue,
                    EstimatedDailyRevenue = auth.EstimatedDailyRevenue,
                    AuthenticatedAtUtc = auth.AuthenticatedAtUtc
                };
            }

            var shopEval = await _delegateShopEval.GetForCustomerAsync(customerId, ct);
            if (shopEval != null)
            {
                profile.DelegateShopEval = new DelegateShopEvalBlockDTO
                {
                    Rating = shopEval.Rating,
                    Note = shopEval.Note,
                    DelegateName = shopEval.DelegateName,
                    DelegateId = shopEval.DelegateId,
                    ShopStockEstimatedValue = shopEval.ShopStockEstimatedValue,
                    EstimatedDailyRevenue = shopEval.EstimatedDailyRevenue,
                    SaleAmount = shopEval.SaleAmount,
                    CreatedAtUtc = shopEval.CreatedAtUtc
                };
            }

            return Ok(profile);
        }

        [HttpGet("Customers/{customerId:int}/documents/{documentId:int}/file")]
        public async Task<IActionResult> CustomerDocumentFile(
            int customerId,
            int documentId,
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

            var row = await _followerActions.GetSalesDocumentAsync(documentId, ct);
            if (row == null)
            {
                _logger.LogWarning(
                    "Follower document 404: document row missing. customerId={CustomerId} documentId={DocumentId}",
                    customerId, documentId);
                return NotFound(new { message = "المستند غير موجود" });
            }

            var info = await _customersRepository.GetCustomersDataInfo(customerId);
            var docs = await _followerActions.ListCustomerSalesDocumentsAsync(
                customerId, info?.CustomerName, info?.PhoneNumber, ct);
            if (docs.All(d => d.Id != documentId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "المستند خارج نطاق هذا الزبون" });
            }

            var file = await _followerActions.ReadSalesDocumentFileAsync(documentId, ct);
            if (file == null)
            {
                _logger.LogWarning(
                    "Follower document 404: file missing on disk. customerId={CustomerId} documentId={DocumentId} fileKey={FileKey} {PathInfo}",
                    customerId, documentId, row.FileKey, _followerActions.DescribeDocumentPathResolution(row.FileKey));
                return NotFound(new { message = "ملف المستند غير موجود على السيرفر" });
            }

            // Inline image bytes (no Content-Disposition:attachment) so Flutter Image.network can decode.
            return File(file.Value.Bytes, file.Value.ContentType);
        }

        [HttpGet("Customers/{customerId:int}/shop-image")]
        public async Task<IActionResult> CustomerShopImage(
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

            var info = await _customersRepository.GetCustomersDataInfo(customerId);
            var shop = await _followerActions.GetCustomerShopImageAsync(
                customerId, info?.CustomerName, info?.PhoneNumber, ct);
            if (shop?.ShopImageKey is not { Length: > 0 } key)
            {
                _logger.LogWarning("Follower shop-image 404: no ShopImageKey. customerId={CustomerId}", customerId);
                return NotFound(new { message = "لا توجد صورة محل" });
            }

            var file = await _followerActions.ReadShopImageFileAsync(key, ct);
            if (file == null)
            {
                _logger.LogWarning(
                    "Follower shop-image 404: file missing on disk. customerId={CustomerId} fileKey={FileKey} {PathInfo}",
                    customerId, key, _followerActions.DescribeDocumentPathResolution(key));
                return NotFound(new { message = "ملف صورة المحل غير موجود على السيرفر" });
            }

            return File(file.Value.Bytes, file.Value.ContentType);
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

            var notes = (await _sharedNotes.ListAsync(customerId, ct))
                .Select(SharedCustomerNotesMapper.ToApi);
            return Ok(notes);
        }

        [HttpPost("Customers/{customerId:int}/notes")]
        public async Task<IActionResult> AddCustomerNote(
            int customerId,
            [FromBody] FollowerNoteCreateDTO body,
            CancellationToken ct)
        {
            var father = await AuthenticateFollower(body?.AsyncId);
            if (father == null)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            if (body == null || body.ListId <= 0)
            {
                return BadRequest(new { message = "يجب اختيار قائمة" });
            }

            var denied = await EnsureCustomerInScope(father.DelegateId, body.ListId, customerId, ct);
            if (denied != null)
            {
                return denied;
            }

            try
            {
                var text = (body.NoteText ?? "").Trim();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return BadRequest(new { message = "نص الملاحظة مطلوب" });
                }

                // Tag declining follow-up notes for later audit without a separate table.
                if (!text.StartsWith("متابعة:", StringComparison.Ordinal)
                    && !text.StartsWith("[متابعة]", StringComparison.Ordinal))
                {
                    // Keep free-form notes allowed; UI prefixes declining actions.
                }

                var saved = await _sharedNotes.AddAsync(
                    customerId,
                    text,
                    father.UserId ?? father.DelegateId,
                    string.IsNullOrWhiteSpace(father.DelegateName) ? "متابع" : father.DelegateName,
                    ct);
                return Ok(SharedCustomerNotesMapper.ToApi(saved));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("Delegates/{delegateId:int}/notes")]
        public async Task<IActionResult> ListDelegateNotes(
            int delegateId,
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
            if (!FollowerAuthorization.CanNoteListDelegate(linked, listId, delegateId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "لا يمكنك عرض ملاحظات مندوب هذه القائمة" });
            }

            var notes = await _followerActions.ListDelegateNotesForFollowerAsync(delegateId, father.DelegateId, ct);
            return Ok(notes);
        }

        [HttpPost("Delegates/{delegateId:int}/notes")]
        public async Task<IActionResult> AddDelegateNote(
            int delegateId,
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

            var linked = await _delegateRepository.IsFollowerListLinked(father.DelegateId, body.ListId);
            // Force target = assigned list delegate (ignore spoofed path id mismatch).
            if (!FollowerAuthorization.CanNoteListDelegate(linked, body.ListId, delegateId)
                || delegateId != body.ListId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "لا يمكنك إضافة ملاحظة على مندوب قائمة غير مسندة" });
            }

            _ = FollowerAuthorization.AcceptClientCreatedByUserId(body.CreatedByUserId, father.DelegateId);

            var saved = await _followerActions.AddDelegateNoteAsync(new FollowerDelegateNoteDTO
            {
                DelegateId = body.ListId,
                ListId = body.ListId,
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

        /// <summary>
        /// Follower inbox of sales requests they originated.
        /// tab: all | inProgress | rejected | completed
        /// </summary>
        [HttpGet("SalesRequests")]
        public async Task<IActionResult> ListSalesRequests(
            [FromQuery] string? asyncId,
            [FromQuery] string? tab,
            CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId);
            if (father == null)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            try
            {
                // Same id stamped on create (CreatedByUserId = father.DelegateId).
                var items = await _followerActions.ListSalesRequestsForFollowerAsync(father.DelegateId, tab, ct);
                return Ok(items);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpPost("SalesRequests")]
        public async Task<IActionResult> SubmitSalesRequest([FromBody] FollowerSalesRequestCreateDTO body, CancellationToken ct)
        {
            var father = await AuthenticateFollower(body.AsyncId);
            if (father == null)
            {
                return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            }

            if (body.ListId <= 0)
            {
                return BadRequest(new { message = "يجب اختيار القائمة المسندة" });
            }

            var linked = await _delegateRepository.IsFollowerListLinked(father.DelegateId, body.ListId);
            var existingId = body.CustomerId is > 0 ? body.CustomerId : null;

            // Province from follower city, else assigned list city — never from client body.
            var followerCity = await _followerActions.GetFollowerCityNameAsync(father.UserId ?? father.DelegateId, ct);
            if (string.IsNullOrWhiteSpace(followerCity) && father.CityId is > 0)
            {
                followerCity = await _followerActions.GetCityNameByIdAsync(father.CityId.Value, ct);
            }

            string? listCity = null;
            if (string.IsNullOrWhiteSpace(followerCity) && linked)
            {
                listCity = await _followerActions.GetListCityNameAsync(body.ListId, ct);
            }

            var province = FollowerAuthorization.ResolveProvinceWithListFallback(
                followerCity, listCity, body.Province);
            if (string.IsNullOrWhiteSpace(province))
            {
                return BadRequest(new
                {
                    message =
                        "تعذر تحديد المحافظة: لا مدينة لحساب المتابع ولا للقائمة المسندة. حدّث UsersSelectedCities للمتابع أو CityID للقائمة في Delegates."
                });
            }

            string? name;
            string? phone;
            string? address;
            var cityValue = province;
            var cityName = province;

            if (existingId is > 0)
            {
                var denied = await EnsureCustomerInScope(father.DelegateId, body.ListId, existingId.Value, ct);
                if (denied != null)
                {
                    return denied;
                }

                var scope = await _followerActions.GetCustomerScopeAsync(existingId.Value, ct);
                if (scope == null)
                {
                    return NotFound(new { message = "الزبون غير موجود" });
                }

                name = (body.FullName ?? scope.Value.CustomerName ?? string.Empty).Trim();
                phone = string.IsNullOrWhiteSpace(body.Phone) ? scope.Value.Phone : body.Phone.Trim();
                address = string.IsNullOrWhiteSpace(body.Address) ? scope.Value.Address : body.Address.Trim();
            }
            else
            {
                if (!FollowerAuthorization.CanSubmitNewCustomerRequest(linked, body.ListId))
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "القائمة غير مسندة" });
                }

                name = (body.FullName ?? string.Empty).Trim();
                phone = body.Phone?.Trim();
                address = body.Address?.Trim();
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { message = "اسم الزبون مطلوب" });
            }

            phone = phone?.Trim().Replace(" ", string.Empty);
            if (!FollowerAuthorization.IsValidFollowerPhone(phone))
            {
                return BadRequest(new { message = "رقم الهاتف يجب أن يكون 11 رقماً ويبدأ بـ 07" });
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                return BadRequest(new { message = "العنوان مطلوب" });
            }

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
                existingId,
                cityValue,
                cityName,
                customerSourceType: "Follower",
                saleRequestType: SaleRequestTypes.Normalize(body.SaleRequestType, existingId is > 0),
                sourceListId: body.ListId,
                ct: ct);

            return Ok(saved);
        }

        private string PublicApiRoot()
        {
            var configured = _configuration["PublicApiBaseUrl"];
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.TrimEnd('/');
            }

            // Prefer forwarded host when behind nginx so Flutter gets a reachable absolute URL.
            var request = HttpContext.Request;
            var proto = request.Headers["X-Forwarded-Proto"].FirstOrDefault()
                        ?? request.Scheme;
            var host = request.Headers["X-Forwarded-Host"].FirstOrDefault()
                       ?? request.Host.Value;
            return $"{proto}://{host}/api";
        }

        private string ImagesBaseUrl()
        {
            var api = PublicApiRoot();
            if (api.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                return api[..^4] + "/Images";
            }

            return $"{Request.Scheme}://{Request.Host}/Images";
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

        [HttpGet("FieldFollowups")]
        public async Task<IActionResult> FieldFollowups([FromQuery] string? asyncId, CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var items = await _fieldFollowups.ListPendingAsync(father.UserId ?? father.DelegateId, ct);
                return Ok(new { items });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        public sealed class FieldFollowupAddBody
        {
            public string? AsyncId { get; set; }
            public int ListId { get; set; }
            public string? ListName { get; set; }
            public int CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public string? PhoneNumber { get; set; }
        }

        [HttpPost("FieldFollowups")]
        public async Task<IActionResult> AddFieldFollowup([FromBody] FieldFollowupAddBody? body, CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(body?.AsyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                body ??= new FieldFollowupAddBody();
                var denied = await EnsureCustomerInScope(father.DelegateId, body.ListId, body.CustomerId, ct);
                if (denied != null) return denied;

                var listName = body.ListName;
                if (string.IsNullOrWhiteSpace(listName))
                {
                    listName = await _followerActions.GetDelegateNameAsync(body.ListId, ct);
                }

                var row = await _fieldFollowups.AddAsync(
                    father.UserId ?? father.DelegateId,
                    body.ListId,
                    listName,
                    body.CustomerId,
                    body.CustomerName,
                    body.PhoneNumber,
                    ct);
                return Ok(row);
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

        public sealed class FieldFollowupCompleteBody
        {
            public string? AsyncId { get; set; }
            public string? Note { get; set; }
        }

        [HttpPost("FieldFollowups/{id:int}/complete")]
        public async Task<IActionResult> CompleteFieldFollowup(
            int id,
            [FromBody] FieldFollowupCompleteBody? body,
            CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(body?.AsyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var followerUserId = father.UserId ?? father.DelegateId;
                var row = await _fieldFollowups.CompleteAsync(
                    id,
                    followerUserId,
                    body?.Note,
                    ct);
                await TryWriteFieldFollowupNoteAsync(row, father, ct);
                return Ok(row);
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

        public sealed class FieldFollowupRecordCompleteBody
        {
            public string? AsyncId { get; set; }
            public int ListId { get; set; }
            public string? ListName { get; set; }
            public int CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public string? PhoneNumber { get; set; }
            public string? Note { get; set; }
        }

        [HttpPost("FieldFollowups/record-complete")]
        public async Task<IActionResult> RecordCompleteFieldFollowup(
            [FromBody] FieldFollowupRecordCompleteBody? body,
            CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(body?.AsyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                body ??= new FieldFollowupRecordCompleteBody();
                var denied = await EnsureCustomerInScope(father.DelegateId, body.ListId, body.CustomerId, ct);
                if (denied != null) return denied;

                var listName = body.ListName;
                if (string.IsNullOrWhiteSpace(listName))
                {
                    listName = await _followerActions.GetDelegateNameAsync(body.ListId, ct);
                }

                var row = await _fieldFollowups.RecordCompleteAsync(
                    father.UserId ?? father.DelegateId,
                    body.ListId,
                    listName,
                    body.CustomerId,
                    body.CustomerName,
                    body.PhoneNumber,
                    body.Note,
                    ct);
                await TryWriteFieldFollowupNoteAsync(row, father, ct);
                return Ok(row);
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

        [HttpGet("FieldFollowups/latest-notes")]
        public async Task<IActionResult> LatestFieldFollowupNotes(
            [FromQuery] string? asyncId,
            [FromQuery] int listId,
            CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                if (listId <= 0) return BadRequest(new { message = "listId مطلوب" });
                var items = await _fieldFollowups.ListLatestCompletedNotesAsync(
                    father.UserId ?? father.DelegateId,
                    listId,
                    ct);
                return Ok(new { items });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        private async Task TryWriteFieldFollowupNoteAsync(
            FollowerFieldFollowupRow row,
            DelegateGetDTO father,
            CancellationToken ct)
        {
            try
            {
                var note = (row.CompletionNote ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(note) || row.CustomerId <= 0) return;
                var text = note.StartsWith("متابعة ميدانية:", StringComparison.Ordinal)
                    ? note
                    : $"متابعة ميدانية: {note}";
                var author = string.IsNullOrWhiteSpace(father.DelegateName) ? "متابع" : father.DelegateName!.Trim();
                await _sharedNotes.AddAsync(row.CustomerId, text, father.UserId, author, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist field-followup note to CustomerNotes customerId={CustomerId}", row.CustomerId);
            }
        }

        [HttpGet("PendingSales")]
        public async Task<IActionResult> PendingSales([FromQuery] string? asyncId, CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var items = await _saleAuth.ListPendingAsync(father.UserId ?? father.DelegateId, ct);
                return Ok(new { items });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        public sealed class AuthenticateSaleBody
        {
            public string? AsyncId { get; set; }
            public int CustomerId { get; set; }
            public int ListId { get; set; }
            public int Rating { get; set; }
            public string? Note { get; set; }
            public decimal ShopStockEstimatedValue { get; set; }
            public decimal EstimatedDailyRevenue { get; set; }
        }

        [HttpPost("PendingSales/Authenticate")]
        public async Task<IActionResult> AuthenticateSale([FromBody] AuthenticateSaleBody? body, CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(body?.AsyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                body ??= new AuthenticateSaleBody();
                var denied = await EnsureCustomerInScope(father.DelegateId, body.ListId, body.CustomerId, ct);
                if (denied != null) return denied;

                var row = await _saleAuth.AuthenticateAsync(
                    father.UserId ?? father.DelegateId,
                    father.DelegateName,
                    body.CustomerId,
                    body.ListId,
                    body.Rating,
                    body.Note,
                    body.ShopStockEstimatedValue,
                    body.EstimatedDailyRevenue,
                    ct);

                // تقييم ≤6 → مبيع خاطئ لإشعار المدير المفوض (best-effort).
                if (FollowerSaleAuthRules.IsWrongSaleRating(row.Rating))
                {
                    var scope = await _followerActions.GetCustomerScopeAsync(body.CustomerId, ct);
                    string? province = scope?.CityName;
                    if (string.IsNullOrWhiteSpace(province) && father.CityId is > 0)
                    {
                        province = await _followerActions.GetCityNameByIdAsync(father.CityId.Value, ct);
                    }

                    var reason = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                        row.Rating,
                        row.Note,
                        row.ShopStockEstimatedValue,
                        row.EstimatedDailyRevenue,
                        scope?.CustomerName,
                        scope?.Phone,
                        evaluatorRole: "متابع",
                        includeWrongSaleTag: true);

                    _ = await _wrongSaleForwarder.ForwardAsync(new WrongSaleExceptionForwardRequest
                    {
                        CityValue = province,
                        CityName = province,
                        CustomerId = body.CustomerId,
                        CustomerName = scope?.CustomerName,
                        CustomerPhone = scope?.Phone,
                        Reason = reason,
                        RequesterUserId = (father.UserId ?? father.DelegateId).ToString(),
                        RequesterUserName = father.DelegateName,
                        RequesterDisplayName = string.IsNullOrWhiteSpace(father.DelegateName)
                            ? "متابع"
                            : father.DelegateName.Trim()
                    }, ct);
                }

                return Ok(new
                {
                    row.Id,
                    row.CustomerId,
                    row.ListId,
                    row.Rating,
                    row.Note,
                    row.ShopStockEstimatedValue,
                    row.EstimatedDailyRevenue,
                    row.AuthenticatedAtUtc,
                    IsWrongSale = FollowerSaleAuthRules.IsWrongSaleRating(row.Rating)
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

        [HttpGet("ListStats")]
        public async Task<IActionResult> ListStats([FromQuery] string? asyncId, CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                // Iraq local calendar month for freeze boundaries.
                var nowIraq = DateTime.UtcNow.AddHours(3);
                var items = await _listStats.GetStatsForFollowerAsync(
                    father.UserId ?? father.DelegateId,
                    nowIraq,
                    ct);
                return Ok(new { items, yearMonth = FollowerListStatsRules.ToYearMonth(nowIraq) });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        /// <summary>Single-list stats for list-detail tab (avoids loading every linked list).</summary>
        [HttpGet("ListStats/{listId:int}")]
        public async Task<IActionResult> ListStatsOne(
            int listId,
            [FromQuery] string? asyncId,
            CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var nowIraq = DateTime.UtcNow.AddHours(3);
                var item = await _listStats.GetStatsForListAsync(
                    father.UserId ?? father.DelegateId,
                    listId,
                    nowIraq,
                    ct);
                if (item == null) return NotFound(new { message = "القائمة غير مرتبطة" });
                return Ok(new { item, yearMonth = FollowerListStatsRules.ToYearMonth(nowIraq) });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("ListStats/{listId:int}/rating-customers")]
        public async Task<IActionResult> ListStatsRatingCustomers(
            int listId,
            [FromQuery] string? asyncId,
            [FromQuery] string? level,
            CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var nowIraq = DateTime.UtcNow.AddHours(3);
                // Empty / "all" = every rated customer in one round-trip.
                var items = await _listStats.GetRatingCustomersAsync(
                    father.UserId ?? father.DelegateId,
                    listId,
                    string.IsNullOrWhiteSpace(level) ? "all" : level,
                    nowIraq,
                    ct);
                return Ok(new { items, level });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("ListStats/{listId:int}/active-customers")]
        public async Task<IActionResult> ListStatsActiveCustomers(
            int listId,
            [FromQuery] string? asyncId,
            CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var nowIraq = DateTime.UtcNow.AddHours(3);
                var items = await _listStats.GetActiveCustomersAsync(
                    father.UserId ?? father.DelegateId,
                    listId,
                    nowIraq,
                    ct);
                return Ok(new { items });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("ListCards")]
        public async Task<IActionResult> ListCards([FromQuery] string? asyncId, CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var nowIraq = DateTime.UtcNow.AddHours(3);
                var items = await _listCards.GetListCardsAsync(father.UserId ?? father.DelegateId, nowIraq, ct);
                return Ok(new { items });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("CollectionLists")]
        public async Task<IActionResult> CollectionLists([FromQuery] string? asyncId, CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var items = await _listCards.GetCollectionListsAsync(father.UserId ?? father.DelegateId, ct);
                return Ok(new { items });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Lists collection-enabled delegates still missing today's 0–10 day rating.
        /// </summary>
        [HttpGet("DayRatings/Missing")]
        public async Task<IActionResult> DayRatingsMissing([FromQuery] string? asyncId, CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var city = _configuration["Branch:CityValue"];
                var items = await _dayRatings.ListMissingForTodayAsync(
                    father.UserId ?? father.DelegateId,
                    city,
                    ct);
                return Ok(new { items, required = items.Count > 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpPost("DayRatings")]
        public async Task<IActionResult> DayRatingsSubmit(
            [FromBody] FollowerDayRatingBody? body,
            [FromQuery] string? asyncId,
            CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                body ??= new FollowerDayRatingBody();
                var city = _configuration["Branch:CityValue"];
                var (ok, error, row) = await _dayRatings.SubmitAsync(
                    father.UserId ?? father.DelegateId,
                    body.ListId,
                    body.Score,
                    body.Note,
                    city,
                    ct);
                if (!ok) return BadRequest(new { message = error });
                return Ok(new
                {
                    id = row!.Id,
                    listId = row.ListId,
                    listName = row.ListName,
                    score = row.Score,
                    iraqDate = row.IraqDate,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        public sealed class FollowerDayRatingBody
        {
            public int ListId { get; set; }
            public int Score { get; set; }
            public string? Note { get; set; }
        }

        [HttpGet("ListStats/{listId:int}/remainder-customers")]
        public async Task<IActionResult> ListStatsRemainderCustomers(
            int listId,
            [FromQuery] string? asyncId,
            [FromQuery] decimal maxRemaining = 100000,
            [FromQuery] decimal minRemaining = 0,
            CancellationToken ct = default)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var nowIraq = DateTime.UtcNow.AddHours(3);
                var items = await _listStats.GetRemainderCustomersAsync(
                    father.UserId ?? father.DelegateId,
                    listId,
                    maxRemaining,
                    nowIraq,
                    ct,
                    minRemaining);
                return Ok(new { items, maxRemaining, minRemaining });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("DecliningCustomers")]
        public async Task<IActionResult> DecliningCustomers([FromQuery] string? asyncId, CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                var nowIraq = DateTime.UtcNow.AddHours(3);
                var items = await _decline.ListDecliningAsync(father.UserId ?? father.DelegateId, nowIraq, ct);
                return Ok(new { items });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DecliningCustomers failed");
                // Never break the tab — empty list is better than a hard error.
                return Ok(new { items = Array.Empty<object>(), warning = ex.Message });
            }
        }

        [HttpGet("Collection/Customers")]
        public async Task<IActionResult> CollectionCustomers(
            [FromQuery] string? asyncId,
            [FromQuery] int listId,
            CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(asyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                if (listId <= 0) return BadRequest(new { message = "يجب اختيار قائمة" });
                var items = await _collectionPayments.ListCustomersAsync(
                    father.UserId ?? father.DelegateId, listId, ct);
                return Ok(new { items });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
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

        [HttpPost("Collection/PostPaymentIdempotent")]
        public async Task<IActionResult> CollectionPostPayment(
            [FromBody] FollowerCollectionPaymentBody? body,
            CancellationToken ct)
        {
            try
            {
                var father = await AuthenticateFollower(body?.AsyncId);
                if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
                if (body == null || body.ListId <= 0)
                {
                    return BadRequest(new { message = "يجب اختيار قائمة" });
                }

                var dto = new CustomersPaymentsRequestsPostDTO
                {
                    CustomerId = body.CustomerId,
                    DelegateId = body.ListId,
                    Amount = body.Amount,
                    Location = body.Location,
                    ClientPaymentId = body.ClientPaymentId,
                    CreatedAtUtc = body.CreatedAtUtc,
                    ReceiptNumber = body.ReceiptNumber,
                    AsyncId = body.AsyncId,
                    DeviceId = body.DeviceId,
                    DeviceModel = body.DeviceModel,
                    DevicePlatform = body.DevicePlatform,
                    AppVersion = body.AppVersion,
                };
                var result = await _collectionPayments.PostPaymentAsync(
                    father.UserId ?? father.DelegateId,
                    body.AsyncId,
                    body.ListId,
                    dto,
                    ct);
                if (!result.Success)
                {
                    return BadRequest(result);
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        public sealed class FollowerCollectionPaymentBody
        {
            public string? AsyncId { get; set; }
            public int ListId { get; set; }
            public int? CustomerId { get; set; }
            public double? Amount { get; set; }
            public string? Location { get; set; }
            public string? ClientPaymentId { get; set; }
            public DateTime? CreatedAtUtc { get; set; }
            public string? ReceiptNumber { get; set; }
            public string? DeviceId { get; set; }
            public string? DeviceModel { get; set; }
            public string? DevicePlatform { get; set; }
            public string? AppVersion { get; set; }
        }

        [HttpGet("LegalFollowups")]
        public async Task<IActionResult> LegalFollowups([FromQuery] string? asyncId, [FromQuery] string? status, CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId);
            if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            try
            {
                return Ok(await _legalFollowups.ListAsync(father.UserId ?? father.DelegateId, status, ct));
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = ex.Message });
            }
        }

        [HttpGet("LegalFollowups/{id:int}")]
        public async Task<IActionResult> LegalFollowup(int id, [FromQuery] string? asyncId, CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId);
            if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            var item = await _legalFollowups.GetAsync(id, father.UserId ?? father.DelegateId, ct);
            return item == null ? NotFound(new { message = "المتابعة غير موجودة" }) : Ok(item);
        }

        public sealed class LegalFollowupCompleteBody
        {
            public string? AsyncId { get; set; }
            public string? Note { get; set; }
        }

        public sealed class LegalFollowupLocationBody
        {
            public string? AsyncId { get; set; }
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string? Address { get; set; }
        }

        [HttpPut("LegalFollowups/{id:int}/location")]
        public async Task<IActionResult> SetLegalFollowupLocation(int id, [FromBody] LegalFollowupLocationBody? body, CancellationToken ct)
        {
            var father = await AuthenticateFollower(body?.AsyncId);
            if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            try
            {
                return Ok(await _legalFollowups.SetLocationAsync(
                    id,
                    father.UserId ?? father.DelegateId,
                    body?.Latitude ?? 0,
                    body?.Longitude ?? 0,
                    body?.Address,
                    ct));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("LegalFollowups/{id:int}/documents")]
        [RequestSizeLimit(25_000_000)]
        public async Task<IActionResult> UploadLegalFollowupDocument(
            int id,
            [FromQuery] string? asyncId,
            [FromForm] string? documentType,
            [FromForm] IFormFile? file,
            CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId ?? Request.Form["asyncId"].FirstOrDefault());
            if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            try
            {
                var type = documentType ?? Request.Form["documentType"].FirstOrDefault() ?? "";
                var upload = file ?? Request.Form.Files.FirstOrDefault();
                return Ok(await _legalFollowups.UploadDocumentAsync(
                    id,
                    father.UserId ?? father.DelegateId,
                    type,
                    upload!,
                    ct));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("LegalFollowups/{id:int}/complete")]
        public async Task<IActionResult> CompleteLegalFollowup(int id, [FromBody] LegalFollowupCompleteBody? body, CancellationToken ct)
        {
            var father = await AuthenticateFollower(body?.AsyncId);
            if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            try
            {
                return Ok(await _legalFollowups.CompleteAsync(id, father.UserId ?? father.DelegateId, body?.Note, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("LegalFollowups/{id:int}/documents/{documentId:int}/file")]
        public async Task<IActionResult> LegalFollowupDocumentFile(
            int id, int documentId, [FromQuery] string? asyncId, CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId);
            if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            var file = await _legalFollowups.ReadDocumentAsync(id, documentId, father.UserId ?? father.DelegateId, ct);
            if (file == null) return NotFound(new { message = "المستند غير موجود" });
            return File(file.Value.Bytes, file.Value.ContentType);
        }

        [HttpGet("LegalFollowups/{id:int}/case-documents/{documentId:int}/file")]
        public async Task<IActionResult> LegalFollowupCaseDocumentFile(
            int id, int documentId, [FromQuery] string? asyncId, CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId);
            if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            var file = await _legalFollowups.ReadCaseDocumentAsync(id, documentId, father.UserId ?? father.DelegateId, ct);
            if (file == null) return NotFound(new { message = "المستند غير موجود" });
            return File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName);
        }

        [HttpGet("LegalFollowups/{id:int}/shop-image")]
        public async Task<IActionResult> LegalFollowupShopImage(int id, [FromQuery] string? asyncId, CancellationToken ct)
        {
            var father = await AuthenticateFollower(asyncId);
            if (father == null) return Unauthorized(new { message = "رمز المتابع غير صحيح" });
            var file = await _legalFollowups.ReadShopImageAsync(id, father.UserId ?? father.DelegateId, ct);
            if (file == null) return NotFound(new { message = "صورة المحل غير موجودة" });
            return File(file.Value.Bytes, file.Value.ContentType);
        }

        private async Task<DelegateGetDTO?> AuthenticateFollower(string? asyncId)
        {
            // User-based follower only (Users.UserType = متابع). Never Delegates login.
            var user = await _followerIdentity.ResolveByAsyncIdAsync(asyncId);
            if (user == null || user.UserId <= 0 || !user.IsActive)
            {
                return null;
            }

            // Compat DTO: DelegateId field carries UserId for existing list-scope call sites.
            return new DelegateGetDTO
            {
                DelegateId = user.UserId,
                DelegateName = user.UserName,
                AsyncId = user.AsyncId,
                UserId = user.UserId,
                CityId = user.CityId,
            };
        }
    }
}
