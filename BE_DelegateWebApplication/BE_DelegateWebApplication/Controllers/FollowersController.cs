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

        public FollowersController(
            IDelegateRepository delegateRepository,
            ICustomersRepository customersRepository,
            IFollowerActionsRepository followerActions,
            IFollowerIdentityService followerIdentity,
            ISharedCustomerNotesService sharedNotes,
            IWebHostEnvironment env,
            IConfiguration configuration,
            ILogger<FollowersController> logger)
        {
            _delegateRepository = delegateRepository;
            _customersRepository = customersRepository;
            _followerActions = followerActions;
            _followerIdentity = followerIdentity;
            _sharedNotes = sharedNotes;
            _env = env;
            _configuration = configuration;
            _logger = logger;
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
                Images = images
            };
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
            // Shared customer notes are read-only for followers (same dbo.CustomerNotes record).
            _ = customerId;
            _ = body;
            _ = ct;
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "المتابع يقرأ ملاحظات الزبون فقط" });
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

            // Province/city ALWAYS from authenticated follower — never from client body.
            var followerCity = await _followerActions.GetFollowerCityNameAsync(father.DelegateId, ct);
            if (string.IsNullOrWhiteSpace(followerCity) && father.CityId is > 0)
            {
                followerCity = await _followerActions.GetCityNameByIdAsync(father.CityId.Value, ct);
            }

            var province = FollowerAuthorization.ResolveProvinceFromFollower(followerCity, body.Province);
            if (string.IsNullOrWhiteSpace(province))
            {
                return BadRequest(new { message = "محافظة حساب المتابع غير معرّفة في النظام. حدّث CityID لحساب المتابع في Delegates." });
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
            };
        }
    }
}
