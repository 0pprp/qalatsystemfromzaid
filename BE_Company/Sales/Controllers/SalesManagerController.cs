using BE_Company.IRepository;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using BE_Company.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Sales.Controllers
{
    [Authorize(Policy = SalesPolicies.SalesManager)]
    [Route("api/sales-manager")]
    [ApiController]
    public class SalesManagerController : ControllerBase
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly SalesIdentityService _identity;
        private readonly SalesManagerQueryService _query;
        private readonly ISalesRequestService _requests;
        private readonly IGlobalCustomerSearchService _search;
        private readonly IIraqClock _clock;
        private readonly ISalesShopProfileService _shops;
        private readonly ISalesCompleteService _complete;
        private readonly ISalesCustomerDocumentService _customerDocs;
        private readonly ISalesPurchaseService _purchases;
        private readonly ISalesExcelCustomerSearchService _excelSearch;
        private readonly ISalesRequestEvaluationService _evaluation;
        private readonly IExceptionReviewService _exceptionReview;
        private readonly ISharedCustomerNotesService _sharedNotes;
        private readonly ILegalFollowupService _legalFollowups;
        private readonly IFollowerDirectoryService _followers;
        private readonly IDelegatesRepository _delegates;
        private readonly IBranchManagerStatsService _stats;
        private readonly IBranchMonthStatsService _monthStats;
        private readonly ISalesManagerFollowerQueryService _followerQuery;
        private readonly IDirectorSummaryService _directorSummary;

        public SalesManagerController(
            SalesDevelopmentGuard guard,
            SalesIdentityService identity,
            SalesManagerQueryService query,
            ISalesRequestService requests,
            IGlobalCustomerSearchService search,
            IIraqClock clock,
            ISalesShopProfileService shops,
            ISalesCompleteService complete,
            ISalesCustomerDocumentService customerDocs,
            ISalesPurchaseService purchases,
            ISalesExcelCustomerSearchService excelSearch,
            IExceptionReviewService exceptionReview,
            ISalesRequestEvaluationService evaluation,
            ISharedCustomerNotesService sharedNotes,
            ILegalFollowupService legalFollowups,
            IFollowerDirectoryService followers,
            IDelegatesRepository delegates,
            IBranchManagerStatsService stats,
            IBranchMonthStatsService monthStats,
            ISalesManagerFollowerQueryService followerQuery,
            IDirectorSummaryService directorSummary)
        {
            _guard = guard;
            _identity = identity;
            _query = query;
            _requests = requests;
            _search = search;
            _clock = clock;
            _shops = shops;
            _complete = complete;
            _customerDocs = customerDocs;
            _purchases = purchases;
            _excelSearch = excelSearch;
            _evaluation = evaluation;
            _exceptionReview = exceptionReview;
            _sharedNotes = sharedNotes;
            _legalFollowups = legalFollowups;
            _followers = followers;
            _delegates = delegates;
            _stats = stats;
            _monthStats = monthStats;
            _followerQuery = followerQuery;
            _directorSummary = directorSummary;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> Dashboard(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _query.DashboardAsync(ct));
        }

        [HttpGet("employees")]
        public async Task<IActionResult> Employees([FromQuery] string? cityValue, [FromQuery] string? shiftStatus, [FromQuery] string? locationStatus, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            // Branch SQL connection is the tenancy boundary. Gateway selects the branch URL;
            // do not equality-filter rows by GetAdmin numeric ids (that emptied or mis-routed lists).
            _ = cityValue;
            var rows = await _query.ListEmployeesAsync(shiftStatus, locationStatus, ct);
            await _stats.AttachEmployeeLatestRatingsAsync(rows, ct);
            return Ok(rows);
        }

        /// <summary>Active sales lists (delegates) for main-follower committee reviews.</summary>
        [HttpGet("delegates")]
        public async Task<IActionResult> Delegates([FromQuery] string? cityValue, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            _ = cityValue;
            var rows = await _delegates.Delegates_GetAll(null) ?? [];
            return Ok(rows
                .Where(d => d.DelegateID is > 0)
                .Where(d => d.DelegateState != false)
                .Select(d => new
                {
                    delegateId = d.DelegateID,
                    delegateName = d.DelegateName,
                    phone = d.PhoneNumber,
                    notes = d.Notes,
                    cityId = d.CityID,
                    customerCount = d.NumberOfCustomer
                }));
        }

        [HttpGet("live-locations")]
        public async Task<IActionResult> LiveLocations([FromQuery] string? cityValue, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            _ = cityValue;
            var rows = await _query.ListLiveLocationsAsync(ct);
            return Ok(rows);
        }

        [HttpGet("employees/{employeeId:int}")]
        public async Task<IActionResult> Employee(int employeeId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var row = await _query.GetEmployeeAsync(employeeId, ct);
            return row == null ? NotFound() : Ok(row);
        }

        [HttpGet("employees/{employeeId:int}/route")]
        public async Task<IActionResult> Route(int employeeId, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                var day = date?.Date ?? IraqTimeService.IraqNow(_clock).Date;
                return Ok(await _query.GetRouteAsync(employeeId, day, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("employees/{employeeId:int}/tracking-events")]
        public async Task<IActionResult> Events(int employeeId, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var day = date?.Date ?? DateTime.UtcNow.Date;
            return Ok(await _query.GetEventsAsync(employeeId, day, ct));
        }

        [HttpGet("sales")]
        public async Task<IActionResult> Sales([FromQuery] int? employeeId, [FromQuery] string? status, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            DateTime? from = date?.Date;
            DateTime? to = from?.AddDays(1);
            var rows = await _query.ListSalesAsync(employeeId, status, from, to, ct);
            return Ok(rows.Select(s => new SalesManagerSaleListItemDTO
            {
                SaleId = s.SaleId,
                CustomerName = s.FullName,
                CustomerPhone = s.Phone,
                EmployeeId = s.EmployeeId,
                EmployeeName = s.UserName,
                CityName = s.CityName,
                CityValue = s.CityValue,
                Province = s.Province,
                CustomerId = s.CustomerId,
                BaseSalePrice = s.BaseSalePrice,
                FinalSalePrice = s.FinalSalePrice,
                DailyInstallment = s.DailyInstallment,
                DownPayment = s.DownPayment,
                EvaluationLevel = s.EvaluationLevel,
                EvaluationName = SalesEvaluationLevels.DisplayName(s.EvaluationLevel),
                Status = s.Status,
                CreatedAt = s.CreatedAt,
                CompletedAt = s.CompletedAt
            }));
        }

        [HttpGet("sales/{saleId:int}")]
        public async Task<IActionResult> Sale(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var sale = await _query.GetSaleAsync(saleId, ct);
            if (sale == null) return NotFound();
            try
            {
                sale.Shop = await _shops.GetBySaleIdAsync(saleId, ct);
            }
            catch
            {
                sale.Shop = null;
            }

            try
            {
                var docs = await _complete.GetDocumentsAsync(saleId, sale.EmployeeId, ct);
                foreach (var doc in docs)
                {
                    doc.DownloadUrl = $"/api/sales-manager/sales/{saleId}/documents/{doc.DocumentId}/download";
                }

                sale.Documents = docs.ToList();
            }
            catch
            {
                sale.Documents = [];
            }

            return Ok(sale);
        }

        [HttpGet("sales/{saleId:int}/documents")]
        public async Task<IActionResult> SaleDocuments(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var sale = await _query.GetSaleAsync(saleId, ct);
            if (sale == null) return NotFound();
            try
            {
                var docs = await _complete.GetDocumentsAsync(saleId, sale.EmployeeId, ct);
                foreach (var doc in docs)
                {
                    doc.DownloadUrl = $"/api/sales-manager/sales/{saleId}/documents/{doc.DocumentId}/download";
                }

                return Ok(docs);
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("sales/{saleId:int}/documents/{documentId:int}/download")]
        public async Task<IActionResult> SaleDocumentDownload(int saleId, int documentId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var sale = await _query.GetSaleAsync(saleId, ct);
            if (sale == null) return NotFound();
            try
            {
                var file = await _complete.DownloadAsync(saleId, documentId, sale.EmployeeId, ct);
                Console.WriteLine($"SALE_DOCUMENT_DOWNLOAD saleId={saleId} documentId={documentId} bytes={file.Bytes.Length}");
                return File(file.Bytes, "application/pdf", file.FileName);
            }
            catch (SalesCompleteException ex)
            {
                if (ex.StatusCode == StatusCodes.Status404NotFound)
                {
                    Console.WriteLine($"SALE_DOCUMENT_NOT_FOUND saleId={saleId} documentId={documentId}");
                }
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("customers/profile")]
        public async Task<IActionResult> CustomerProfile(
            [FromQuery] int? customerId,
            [FromQuery] string? name,
            [FromQuery] string? phone,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            if (customerId is null or <= 0 && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest(new { message = "حدد الزبون." });
            }

            var profileTask = _shops.GetCustomerProfileAsync(customerId, name, phone, ct);
            var docsTask = _customerDocs.ListForCustomerAsync(customerId, name, phone, true, ct);
            await Task.WhenAll(profileTask, docsTask);
            var profile = await profileTask;
            profile.CustomerDocuments = await docsTask;
            var identity = _identity.FromAuthenticatedUser();
            if (identity != null)
            {
                profile.CityValue ??= identity.BranchId;
                profile.CityName ??= identity.BranchName;
            }

            return Ok(profile);
        }

        [HttpGet("customers/{customerId:int}/profile")]
        public async Task<IActionResult> CustomerProfileById(int customerId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            if (customerId <= 0)
            {
                return BadRequest(new { message = "حدد الزبون." });
            }

            var profile = await _shops.GetCustomerProfileAsync(customerId, null, null, ct);
            profile.CustomerDocuments = await _customerDocs.ListForCustomerAsync(customerId, profile.CustomerName, profile.Phone, true, ct);
            var identity = _identity.FromAuthenticatedUser();
            if (identity != null)
            {
                profile.CityValue ??= identity.BranchId;
                profile.CityName ??= identity.BranchName;
            }

            return Ok(profile);
        }

        [HttpPut("customers/profile")]
        public async Task<IActionResult> UpdateCustomerProfile([FromBody] SalesCustomerUpdateDTO? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            if (body == null
                || (body.CustomerId is null or <= 0
                && string.IsNullOrWhiteSpace(body.OriginalName)
                && string.IsNullOrWhiteSpace(body.OriginalPhone)
                && string.IsNullOrWhiteSpace(body.CustomerName)
                && string.IsNullOrWhiteSpace(body.Phone)))
            {
                return BadRequest(new { message = "حدد الزبون." });
            }

            try
            {
                var identity = _identity.FromAuthenticatedUser();
                var profile = await _shops.UpdateCustomerProfileAsync(body, identity?.EmployeeId, ct);
                profile.CustomerDocuments = await _customerDocs.ListForCustomerAsync(
                    profile.CustomerId, profile.CustomerName, profile.Phone, true, ct);
                if (identity != null)
                {
                    profile.CityValue ??= identity.BranchId;
                    profile.CityName ??= identity.BranchName;
                }

                return Ok(profile);
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("customers/{customerId:int}/notes")]
        public async Task<IActionResult> ListSharedCustomerNotes(int customerId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            if (customerId <= 0)
            {
                return BadRequest(new { message = "الزبون مطلوب" });
            }

            var rows = await _sharedNotes.ListAsync(customerId, ct);
            return Ok(rows.Select(n => new
            {
                noteId = n.NoteId,
                customerId = n.CustomerId,
                noteText = n.NoteText,
                createdByUserId = n.CreatedByUserId,
                createdByName = n.CreatedByName,
                createdAtUtc = n.CreatedAtUtc,
                userName = n.CreatedByName,
                createdDate = n.CreatedDate ?? n.CreatedAtUtc,
            }));
        }

        [HttpPost("customers/notes")]
        public async Task<IActionResult> AddCustomerNote([FromBody] SalesCustomerNoteCreateDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _shops.AddNoteAsync(body, "SalesManager", identity?.EmployeeName, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("sales/{saleId:int}/shop-image")]
        public async Task<IActionResult> ShopImage(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var image = await _shops.ReadImageAsync(saleId, ct);
            if (image == null)
            {
                return NotFound();
            }

            var ext = Path.GetExtension(image.Value.FileName).ToLowerInvariant();
            var type = ext == ".png" ? "image/png" : "image/jpeg";
            return File(image.Value.Bytes, type, image.Value.FileName);
        }

        [HttpGet("customer-documents/{documentId:int}/file")]
        public async Task<IActionResult> CustomerDocumentFile(int documentId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var image = await _customerDocs.ReadFileAsync(documentId, ct);
            if (image == null)
            {
                return NotFound(new { message = "الملف غير موجود على السيرفر." });
            }

            // Browsers + gateway blob clients handle jpeg/png better than octet-stream.
            var type = string.IsNullOrWhiteSpace(image.Value.ContentType)
                       || image.Value.ContentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase)
                ? GuessImageContentType(image.Value.FileName)
                : image.Value.ContentType;
            return File(image.Value.Bytes, type, image.Value.FileName);
        }

        [HttpPost("customers/documents")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(50_000_000)]
        [RequestFormLimits(MultipartBodyLengthLimit = 50_000_000)]
        public async Task<IActionResult> UploadCustomerDocument(
            [FromQuery] string? type,
            [FromQuery] int? customerId,
            [FromQuery] string? name,
            [FromQuery] string? phone,
            [FromQuery] int? saleId,
            [FromForm] IFormFile? file,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            file = SalesCustomerDocumentMatcher.ResolveUpload(file, Request);
            if (file == null || file.Length <= 0)
            {
                return BadRequest(new { message = "الصورة مطلوبة." });
            }

            if (customerId is null or <= 0 && string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(phone))
            {
                return BadRequest(new { message = "حدد الزبون." });
            }

            try
            {
                return Ok(await _customerDocs.SaveAsync(
                    type ?? "",
                    file,
                    saleId,
                    null,
                    customerId,
                    name,
                    phone,
                    replaceSameType: false,
                    managerUrls: true,
                    ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpDelete("customer-documents/{documentId:int}")]
        public async Task<IActionResult> DeleteCustomerDocument(int documentId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                await _customerDocs.DeleteAsync(documentId, null, ct);
                return Ok(new { ok = true });
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("customers/search")]
        public async Task<IActionResult> SearchCustomers([FromQuery] string? q, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var query = (q ?? string.Empty).Trim();
            if (query.Length < 2)
            {
                return BadRequest(new { message = "اكتب حرفين على الأقل للبحث" });
            }

            return Ok(await _search.SearchAsync(query, ct));
        }

        [HttpPost("customers/excel-search")]
        public async Task<IActionResult> ExcelSearchCustomers([FromBody] SalesExcelSearchRequestDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                return Ok(await _excelSearch.SearchAsync(body?.Names ?? [], ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("sales-requests/deleted")]
        public async Task<IActionResult> DeletedRequests(
            [FromQuery] string? cityValue,
            [FromQuery] int? page,
            [FromQuery] int pageSize = 40,
            [FromQuery] string? q = null,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            _ = cityValue;
            // Paged path for FE board; legacy callers without page still get capped list.
            if (page is > 0)
            {
                return Ok(await _requests.ListDeletedForManagerPageAsync(q, page.Value, pageSize, ct));
            }

            var rows = await _requests.ListDeletedForManagerAsync(ct);
            return Ok(rows);
        }

        [HttpGet("sales-requests")]
        public async Task<IActionResult> Requests(
            [FromQuery] string? cityValue,
            [FromQuery] int? employeeId,
            [FromQuery] string? status,
            [FromQuery] DateTime? date,
            [FromQuery] int? page,
            [FromQuery] int pageSize = 40,
            [FromQuery] string? tab = null,
            [FromQuery] string? q = null,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;

            // Production board: page+tab+q → lean paged JSON (20k+ safe).
            if (page is > 0)
            {
                _ = cityValue;
                _ = status;
                _ = date;
                return Ok(await _requests.ListForManagerPageAsync(tab, employeeId, q, page.Value, pageSize, ct));
            }

            DateTime? from = date?.Date;
            DateTime? to = from?.AddDays(1);
            var rows = await _requests.ListForManagerAsync(status, employeeId, from, to, ct);
            if (!string.IsNullOrWhiteSpace(cityValue))
            {
                rows = rows.Where(r => string.Equals(r.CityValue, cityValue, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return Ok(rows);
        }

        [HttpGet("sales-requests/employee-submitted")]
        public async Task<IActionResult> EmployeeSubmittedRequests([FromQuery] string? cityValue, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var rows = await _requests.ListEmployeeSubmittedAsync(ct);
            if (!string.IsNullOrWhiteSpace(cityValue))
            {
                rows = rows.Where(r => string.Equals(r.CityValue, cityValue, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return Ok(rows);
        }

        [HttpGet("sales-requests/unread-count")]
        public async Task<IActionResult> UnreadCount([FromQuery] string? cityValue, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            // Always SQL COUNT — never hydrate employee-submitted rows for a badge.
            // cityValue is gateway routing only; each branch DB is one province.
            _ = cityValue;
            return Ok(new { count = await _requests.CountUnreadEmployeeSubmittedAsync(ct) });
        }

        [HttpPost("sales-requests/{id:int}/mark-read")]
        public async Task<IActionResult> MarkRead(int id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.MarkReadAsync(identity!, id, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/mark-all-read")]
        public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                var marked = await _requests.MarkAllReadAsync(identity!, ct);
                return Ok(new { marked });
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/reject")]
        public async Task<IActionResult> RejectRequest(int id, [FromBody] SalesRequestRejectDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.ManagerRejectAsync(identity!, id, body?.Reason ?? string.Empty, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/prepare")]
        public async Task<IActionResult> PrepareRequest(int id, [FromBody] SalesRequestNoteDTO? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.ManagerPrepareForSaleAsync(identity!, id, body?.Note ?? body?.Reason, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/pending")]
        public async Task<IActionResult> PendRequest(int id, [FromBody] SalesRequestNoteDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.ManagerPendAsync(identity!, id, body?.Note ?? body?.Reason ?? string.Empty, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("sales-requests/{id:int}")]
        public async Task<IActionResult> RequestDetails(int id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            var row = await _requests.GetForManagerAsync(id, ct);
            if (row == null) return NotFound();
            if (SalesRequestSources.IsEmployeeSubmitted(row.CustomerSourceType) && row.ManagerReadAtUtc == null)
            {
                try
                {
                    row = await _requests.MarkReadAsync(identity!, id, ct);
                }
                catch (SalesCompleteException)
                {
                    // listing still works if mark-read is denied
                }
            }

            return Ok(row);
        }

        [HttpPost("sales-requests")]
        public async Task<IActionResult> CreateRequest([FromBody] SalesRequestCreateDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.CreateAsync(identity!, body, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/import")]
        public async Task<IActionResult> ImportRequests([FromBody] SalesRequestImportDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.ImportRowsAsync(identity!, body ?? new SalesRequestImportDTO(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/exception-hold")]
        [HttpPut("sales-requests/{id:int}/exception-hold")]
        public async Task<IActionResult> SetExceptionHold(int id, [FromBody] ExceptionHoldBody? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                await _requests.SetExceptionHoldAsync(id, body?.Status, body?.ExceptionId, ct);
                return Ok(new { id, status = body?.Status, exceptionId = body?.ExceptionId });
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        public sealed class ExceptionHoldBody
        {
            public string? Status { get; set; }
            public Guid? ExceptionId { get; set; }
        }

        [HttpPost("sales-requests/assign-batch")]
        public async Task<IActionResult> AssignBatch([FromBody] SalesRequestAssignBatchDTO? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.AssignBatchAsync(identity!, body ?? new SalesRequestAssignBatchDTO(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/assign")]
        public async Task<IActionResult> AssignRequest(int id, [FromBody] SalesRequestAssignDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.AssignAsync(identity!, id, body, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/return")]
        public async Task<IActionResult> ReturnRequest(int id, [FromBody] SalesRequestNoteDTO? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.ReturnAsync(
                    identity!,
                    id,
                    body?.Note ?? body?.Reason ?? string.Empty,
                    ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/evaluate")]
        public async Task<IActionResult> EvaluateRequests([FromBody] SalesRequestEvaluationBatchRequestDTO? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _evaluation.EvaluateBatchAsync(identity!, body ?? new SalesRequestEvaluationBatchRequestDTO(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        /// <summary>
        /// Same-province exception review context for Delegated Manager (via gateway S2S).
        /// </summary>
        [HttpGet("sales-requests/{id:int}/exception-review-context")]
        public async Task<IActionResult> ExceptionReviewContext(int id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _exceptionReview.BuildContextAsync(identity!, id, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("sales-requests/{id:int}/evaluation-hits")]
        public async Task<IActionResult> EvaluationHits(
            int id,
            [FromQuery] string category,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _evaluation.ListHitsAsync(identity!, id, category, page, pageSize, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/evaluation-hits")]
        public async Task<IActionResult> EvaluationHitsByPayload(
            [FromBody] SalesRequestEvaluationHitsRequestDTO? body,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _evaluation.ListHitsByPayloadAsync(identity!, body ?? new SalesRequestEvaluationHitsRequestDTO(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/accept-province-transfer")]
        public async Task<IActionResult> AcceptProvinceTransfer(
            [FromBody] SalesRequestAcceptTransferDTO? body,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.AcceptProvinceTransferAsync(identity!, body ?? new SalesRequestAcceptTransferDTO(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/mark-province-transferred-out")]
        public async Task<IActionResult> MarkProvinceTransferredOut(
            int id,
            [FromBody] SalesRequestMarkTransferredOutDTO? body,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                await _requests.MarkProvinceTransferredOutAsync(identity!, id, body ?? new SalesRequestMarkTransferredOutDTO(), ct);
                return Ok(new { message = "تم أرشفة الطلب في الفرع المصدر بعد النقل." });
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPut("sales-requests/{id:int}")]
        public async Task<IActionResult> UpdateRequest(int id, [FromBody] SalesRequestManagerUpdateDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.ManagerUpdateAsync(identity!, id, body ?? new SalesRequestManagerUpdateDTO(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("sales-requests/{id:int}/reassign")]
        public async Task<IActionResult> ReassignRequest(int id, [FromBody] SalesRequestManagerReassignDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.ManagerReassignAsync(identity!, id, body ?? new SalesRequestManagerReassignDTO(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("sales-requests/{id:int}/soft-delete-impact")]
        public async Task<IActionResult> SoftDeleteImpact(int id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.GetSoftDeleteImpactAsync(identity!, id, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message, code = ex.Code });
            }
        }

        [HttpDelete("sales-requests/{id:int}")]
        public async Task<IActionResult> DeleteRequest(int id, [FromBody] SalesRequestSoftDeleteDTO? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                await _requests.SoftDeleteAsync(
                    identity!,
                    id,
                    body?.Reason ?? "",
                    body?.ConfirmCascadePayments ?? false,
                    ct);
                return Ok(new { message = "تم حذف الطلب." });
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message, code = ex.Code });
            }
        }

        [HttpPost("sales-requests/{id:int}/soft-delete")]
        public async Task<IActionResult> SoftDeleteRequest(int id, [FromBody] SalesRequestSoftDeleteDTO? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                await _requests.SoftDeleteAsync(
                    identity!,
                    id,
                    body?.Reason ?? "",
                    body?.ConfirmCascadePayments ?? false,
                    ct);
                return Ok(new { message = "تم حذف الطلب." });
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message, code = ex.Code });
            }
        }

        [HttpGet("sales-requests/{id:int}/cancel-impact")]
        public async Task<IActionResult> CancelImpact(int id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _requests.GetCancelImpactAsync(identity!, id, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message, code = ex.Code });
            }
        }

        [HttpPost("sales-requests/{id:int}/cancel")]
        public async Task<IActionResult> CancelRequest(int id, [FromBody] SalesRequestSoftDeleteDTO? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                var row = await _requests.CancelAndUnassignAsync(
                    identity!,
                    id,
                    body?.Reason ?? "",
                    body?.ConfirmCascadePayments ?? false,
                    ct);
                return Ok(new { message = "تم إلغاء الطلب وإعادته إلى غير المسند.", request = row });
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message, code = ex.Code });
            }
        }

        [HttpGet("purchases")]
        public async Task<IActionResult> Purchases(
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] string? textSearch,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                return Ok(await _purchases.ListAsync(fromDate, toDate, textSearch, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("purchases/lookups")]
        public async Task<IActionResult> PurchaseLookups(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                return Ok(await _purchases.GetLookupsAsync(ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("purchases/items")]
        public async Task<IActionResult> PurchaseItems([FromQuery] int storeId, [FromQuery] string? q, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                return Ok(await _purchases.GetItemsAsync(storeId, q, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpPost("purchases")]
        public async Task<IActionResult> CreatePurchase([FromBody] SalesPurchaseCreateDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                return Ok(await _purchases.CreateAsync(identity!, body, ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        [HttpGet("legal-followups")]
        public async Task<IActionResult> LegalFollowups([FromQuery] string? status, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                return Ok(await _legalFollowups.ListAsync(status, ct));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("legal-followups/followers")]
        public async Task<IActionResult> LegalFollowupFollowers(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var rows = await _followers.ListActiveFollowersAsync(ct);
            return Ok(rows.Select(f => new
            {
                userId = f.UserId,
                followerId = f.FollowerId,
                followerName = f.FollowerName,
                cityName = f.CityName,
                cityId = f.CityId
            }));
        }

        [HttpGet("legal-followups/{id:int}")]
        public async Task<IActionResult> LegalFollowup(int id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var item = await _legalFollowups.GetAsync(id, ct);
            return item == null ? NotFound(new { message = "المتابعة غير موجودة" }) : Ok(item);
        }

        [HttpGet("legal-followups/{id:int}/case-documents/{documentId:int}/file")]
        public async Task<IActionResult> LegalFollowupCaseDocumentFile(int id, int documentId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var file = await _legalFollowups.ReadCaseDocumentAsync(id, documentId, ct);
            if (file == null) return NotFound(new { message = "المستند غير موجود" });
            return File(file.Value.Bytes, file.Value.ContentType, file.Value.FileName);
        }

        [HttpPost("legal-followups")]
        public async Task<IActionResult> CreateLegalFollowup([FromBody] LegalFollowupCreateRequest body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                var created = await _legalFollowups.CreateAsync(
                    body ?? new LegalFollowupCreateRequest(),
                    identity!.EmployeeId,
                    identity.EmployeeName,
                    ct);
                return Ok(created);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>إلغاء إرسال قانونية بالغلط → قضية Cancelled وطلب البيع غير مسند.</summary>
        [HttpPost("legal-followups/{id:int}/cancel")]
        public async Task<IActionResult> CancelLegalFollowup(
            int id,
            [FromBody] LegalFollowupCancelRequest? body,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser();
            try
            {
                var cancelled = await _legalFollowups.CancelAsync(
                    id,
                    body?.Reason ?? "",
                    identity!.EmployeeId,
                    identity.EmployeeName,
                    ct);
                return Ok(cancelled);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Same ratings catalog as branch-manager/stats/catalog.
        /// Gateway (مدير مفوض / مدير مبيعات) uses this so city BM role is not required.
        /// </summary>
        [HttpGet("stats/catalog")]
        public async Task<IActionResult> StatsCatalog(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _stats.GetCatalogAsync(ct));
        }

        /// <summary>Branch new/zeroed/net for current + previous calendar month (new = any sale in month).</summary>
        [HttpGet("stats/branch-month")]
        public async Task<IActionResult> StatsBranchMonth(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _monthStats.GetAsync(ct));
        }

        /// <summary>
        /// Director ملخص: day totals for one metric in a calendar month + primary box balance.
        /// Gateway fans out across قلعة الضمان branches.
        /// </summary>
        [HttpGet("director-summary/matrix")]
        public async Task<IActionResult> DirectorSummaryMatrix(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] string? metric,
            [FromQuery] string? branchName,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _directorSummary.GetMatrixAsync(year, month, metric ?? "receipts", branchName, ct));
        }

        /// <summary>Director ملخص drill-down for one calendar day.</summary>
        [HttpGet("director-summary/drilldown")]
        public async Task<IActionResult> DirectorSummaryDrilldown(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] int day,
            [FromQuery] string? metric,
            [FromQuery] string? branchName,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            try
            {
                return Ok(await _directorSummary.GetDrilldownAsync(
                    year, month, day, metric ?? "receipts", branchName, ct));
            }
            catch (ArgumentOutOfRangeException)
            {
                return BadRequest(new { message = "يوم غير صالح." });
            }
        }

        [HttpGet("stats/trend")]
        public async Task<IActionResult> StatsTrend(
            [FromQuery] string? scope,
            [FromQuery] int id,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] bool fullHistory = false,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;

            var today = DateTime.UtcNow.AddHours(3).Date;
            var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (toDate ?? today).Date;
            return Ok(await _stats.GetTrendAsync(scope ?? "branch", id, from, to, ct, fullHistory));
        }

        /// <summary>مجموع تسديدات قائمة ضمن فترة — للمراقب/المدير على كروت القوائم.</summary>
        [HttpGet("stats/list-payments-sum")]
        public async Task<IActionResult> StatsListPaymentsSum(
            [FromQuery] int listId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;

            var today = DateTime.UtcNow.AddHours(3).Date;
            var yesterday = today.AddDays(-1);
            var from = (fromDate ?? yesterday).Date;
            var to = (toDate ?? yesterday).Date;
            return Ok(await _stats.GetListPaymentsSumAsync(listId, from, to, ct));
        }

        /// <summary>
        /// زبائن مستوى تقييم (ممتاز/جيد/…) لنطاق الفرع/قائمة/متابع/موظف — drill-down للمدير.
        /// Page-capped for production (~100k customers).
        /// </summary>
        [HttpGet("stats/rating-customers")]
        public async Task<IActionResult> StatsRatingCustomers(
            [FromQuery] string? scope,
            [FromQuery] int id,
            [FromQuery] string? level,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            [FromQuery] int take = 500,
            [FromQuery] int skip = 0,
            [FromQuery] string? q = null,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;

            var today = DateTime.UtcNow.AddHours(3).Date;
            var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
            var to = (toDate ?? today).Date;
            return Ok(await _stats.GetRatingCustomersAsync(
                scope ?? "branch", id, level, from, to, ct, take, skip, q));
        }

        /// <summary>Lifetime daily rating for one sales employee (first sale → today).</summary>
        [HttpGet("employees/{employeeId:int}/rating-trend")]
        public async Task<IActionResult> EmployeeRatingTrend(int employeeId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _stats.GetEmployeeLifetimeTrendAsync(employeeId, ct));
        }

        /// <summary>Customers in one rating bucket for an employee (ممتاز/جيد/…).</summary>
        [HttpGet("employees/{employeeId:int}/rating-customers")]
        public async Task<IActionResult> EmployeeRatingCustomers(
            int employeeId,
            [FromQuery] string? level,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _stats.GetEmployeeRatingCustomersAsync(employeeId, level, ct));
        }

        /// <summary>
        /// Followers tab — parallel to /employees. GPS is enriched from the Delegate
        /// FollowerTracking proxy (same config as the Accountant follower-tracking pages).
        /// </summary>
        [HttpGet("followers")]
        public async Task<IActionResult> Followers(
            [FromQuery] string? cityValue,
            [FromQuery] string? shiftStatus,
            [FromQuery] string? locationStatus,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            _ = cityValue;
            var rows = await _followerQuery.ListFollowersAsync(shiftStatus, locationStatus, ct);
            await _stats.AttachFollowerLatestRatingsAsync(rows, ct);
            return Ok(rows);
        }

        [HttpGet("followers/live-locations")]
        public async Task<IActionResult> FollowerLiveLocations([FromQuery] string? cityValue, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            _ = cityValue;
            var rows = await _followerQuery.ListLiveLocationsAsync(ct);
            return Ok(rows);
        }

        [HttpGet("followers/{followerId:int}/route")]
        public async Task<IActionResult> FollowerRoute(int followerId, [FromQuery] DateTime? date, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var day = date?.Date ?? IraqTimeService.IraqNow(_clock).Date;
            var json = await _followerQuery.GetRouteJsonAsync(followerId, day, ct);
            if (json == null)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = "تعذر الوصول لخدمة تتبع المتابعين." });
            }

            return Content(json, "application/json");
        }

        /// <summary>Lifetime daily rating for one follower's assigned lists (first sale → today).</summary>
        [HttpGet("followers/{followerId:int}/rating-trend")]
        public async Task<IActionResult> FollowerRatingTrend(int followerId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _stats.GetFollowerLifetimeTrendAsync(followerId, ct));
        }

        /// <summary>Customers in one rating bucket for a follower's assigned lists (ممتاز/جيد/…).</summary>
        [HttpGet("followers/{followerId:int}/rating-customers")]
        public async Task<IActionResult> FollowerRatingCustomers(
            int followerId,
            [FromQuery] string? level,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _stats.GetFollowerRatingCustomersAsync(followerId, level, ct));
        }

        private static string GuessImageContentType(string? fileName)
        {
            var ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };
        }

        private async Task<IActionResult?> GateAsync(CancellationToken ct)
        {
            var check = await _guard.CanRunSalesModuleAsync(ct);
            if (!check.Ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = check.Reason });
            }

            if (_identity.FromAuthenticatedUser() == null)
            {
                return Unauthorized();
            }

            return null;
        }
    }
}
