using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace BE_Company.Sales.Controllers
{
    [Authorize]
    [Route("api/branch-manager")]
    [ApiController]
    public sealed class BranchManagerController : ControllerBase
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly SalesIdentityService _identity;
        private readonly IBranchManagerNewSalesService _newSales;
        private readonly ISalesRequestService _requests;
        private readonly IBranchManagerComplaintForwarder _complaints;
        private readonly IBranchManagerDirectorNoteForwarder _directorNotes;
        private readonly ICustomerWaiverGatewayForwarder _waivers;
        private readonly IWrongSaleGatewayForwarder _wrongSales;
        private readonly IBranchManagerStatsService _stats;
        private readonly IBranchMonthStatsService _monthStats;
        private readonly IBmRatingDeclineAlertService _declineAlerts;
        private readonly IBmCustomerDeclineService _customerDeclines;
        private readonly IDirectorDelegateFollowupService _followup;
        private readonly IConfiguration _configuration;

        public BranchManagerController(
            SalesDevelopmentGuard guard,
            SalesIdentityService identity,
            IBranchManagerNewSalesService newSales,
            ISalesRequestService requests,
            IBranchManagerComplaintForwarder complaints,
            IBranchManagerDirectorNoteForwarder directorNotes,
            ICustomerWaiverGatewayForwarder waivers,
            IWrongSaleGatewayForwarder wrongSales,
            IBranchManagerStatsService stats,
            IBranchMonthStatsService monthStats,
            IBmRatingDeclineAlertService declineAlerts,
            IBmCustomerDeclineService customerDeclines,
            IDirectorDelegateFollowupService followup,
            IConfiguration configuration)
        {
            _guard = guard;
            _identity = identity;
            _newSales = newSales;
            _requests = requests;
            _complaints = complaints;
            _directorNotes = directorNotes;
            _waivers = waivers;
            _wrongSales = wrongSales;
            _stats = stats;
            _monthStats = monthStats;
            _declineAlerts = declineAlerts;
            _customerDeclines = customerDeclines;
            _followup = followup;
            _configuration = configuration;
        }

        private string? ClaimUserType() =>
            HttpContext.Items["UserType"] as string
            ?? User.FindFirst("UserType")?.Value;

        /// <summary>
        /// Authenticated non-sales identities (e.g. محاسب) must get 403 — never 401 —
        /// or the FE refresh interceptor logs them out.
        /// </summary>
        private IActionResult? DenyIfAuthenticatedElseUnauthorized(string message)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message });
            }

            return Unauthorized();
        }

        private async Task<IActionResult?> GateAsync(CancellationToken ct)
        {
            var (ok, reason) = await _guard.CanRunSalesModuleAsync(ct);
            if (!ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return DenyIfAuthenticatedElseUnauthorized("هذه الواجهة لمدير الفرع فقط");
            }

            if (!SalesRoles.IsBranchManager(identity.UserType))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "هذه الواجهة لمدير الفرع فقط" });
            }

            return null;
        }

        /// <summary>BM app + SalesGateway visit mirror.</summary>
        private async Task<IActionResult?> GateSupervisionMirrorAsync(CancellationToken ct)
        {
            const string supervisionMirrorGateway = "SupervisionMirrorGateway_V1";
            var (ok, reason) = await _guard.CanRunSalesModuleAsync(ct);
            if (!ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
            }

            var identity = _identity.FromAuthenticatedUser();
            if (identity == null)
            {
                return DenyIfAuthenticatedElseUnauthorized(
                    "هذه الواجهة لمدير الفرع أو بوابة المبيعات فقط (" + supervisionMirrorGateway + ")");
            }

            if (SalesRoles.IsBranchManager(identity.UserType) || SalesRoles.IsSalesManager(identity.UserType))
            {
                return null;
            }

            var authSource = User.FindFirst(SalesGatewayKeyHandler.AuthSourceClaim)?.Value;
            if (string.Equals(authSource, SalesGatewayKeyHandler.AuthSourceGateway, StringComparison.Ordinal))
            {
                return null;
            }

            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "هذه الواجهة لمدير الفرع أو بوابة المبيعات فقط (" + supervisionMirrorGateway + ")"
            });
        }

        /// <summary>قراءة الزبائن المتدنية: مدير فرع + محاسب (لوحة العملاء).</summary>
        private async Task<IActionResult?> GateDecliningReadAsync(CancellationToken ct)
        {
            var (ok, reason) = await _guard.CanRunSalesModuleAsync(ct);
            if (!ok)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = reason });
            }

            var userType = ClaimUserType()?.Trim();
            if (string.IsNullOrWhiteSpace(userType))
            {
                return DenyIfAuthenticatedElseUnauthorized("غير مصرح بعرض الزبائن المتدنية");
            }

            if (SalesRoles.IsBranchManager(userType)
                || string.Equals(userType, SalesRoles.UserTypeMainAccountant, StringComparison.Ordinal)
                || string.Equals(userType, SalesRoles.UserTypeSubAccountant, StringComparison.Ordinal))
            {
                return null;
            }

            return DenyIfAuthenticatedElseUnauthorized("غير مصرح بعرض الزبائن المتدنية");
        }

        [HttpGet("new-sales")]
        public async Task<IActionResult> ListNewSales([FromQuery] bool unreadOnly = false, CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _newSales.ListAsync(unreadOnly, ct));
        }

        [HttpGet("new-sales/unread-count")]
        public async Task<IActionResult> UnreadCount(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(new { count = await _newSales.CountUnreadAsync(ct) });
        }

        [HttpGet("new-sales/{saleId:int}")]
        public async Task<IActionResult> GetNewSale(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var row = await _newSales.GetAsync(saleId, ct);
            return row == null ? NotFound(new { message = "المبيع غير موجود" }) : Ok(row);
        }

        [HttpPost("new-sales/{saleId:int}/mark-read")]
        public async Task<IActionResult> MarkRead(int saleId, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            await _newSales.MarkReadAsync(saleId, ct);
            return Ok(new { ok = true });
        }

        [HttpPost("new-sales/mark-all-read")]
        public async Task<IActionResult> MarkAllRead(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var marked = await _newSales.MarkAllReadAsync(ct);
            return Ok(new { marked });
        }

        [HttpGet("stats/catalog")]
        public async Task<IActionResult> StatsCatalog(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _stats.GetCatalogAsync(ct));
        }

        /// <summary>
        /// Lists for branch-visit 0–10 ratings — same as المراقب متابعة المندوبين
        /// (payment within 40 days). Not the full stats catalog.
        /// </summary>
        [HttpGet("stats/visit-rating-lists")]
        public async Task<IActionResult> VisitRatingLists(CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _stats.GetVisitRatingListsAsync(ct));
        }

        /// <summary>
        /// Mirror BM visit 0–10 list ratings into company for المراقب board card
        /// «تقييم لجنة الإشراف». ActivateInputRatingSlots_V1
        /// Accepts مدير فرع JWT or trusted SalesGateway key (mirror from visit save).
        /// </summary>
        [HttpPost("list-supervision-ratings")]
        public async Task<IActionResult> UpsertListSupervisionRatings(
            [FromBody] BmListSupervisionRatingsBody? body,
            CancellationToken ct)
        {
            var gate = await GateSupervisionMirrorAsync(ct);
            if (gate != null) return gate;
            var ratings = (body?.Ratings ?? [])
                .Where(r => r.ListId > 0 && r.Score is >= 0 and <= 10)
                .Select(r => (r.ListId, r.Score, r.Note, r.RatedBy ?? ClaimUserName()))
                .ToList();
            if (ratings.Count == 0)
                return BadRequest(new { message = "لا توجد تقييمات صالحة" });
            var (ok, error) = await _followup.UpsertSupervisionRatingsAsync(
                ratings, body?.RatingDate, ct);
            if (!ok)
                return BadRequest(new { message = error ?? "تعذر الحفظ" });
            return Ok(new { ok = true, count = ratings.Count });
        }

        private string? ClaimUserName() =>
            User.FindFirst("UserName")?.Value
            ?? User.FindFirst("unique_name")?.Value
            ?? User.Identity?.Name;

        public sealed class BmListSupervisionRatingsBody
        {
            public DateTime? RatingDate { get; set; }
            public List<BmListSupervisionRatingItem> Ratings { get; set; } = [];
        }

        public sealed class BmListSupervisionRatingItem
        {
            public int ListId { get; set; }
            public int Score { get; set; }
            public string? Note { get; set; }
            public string? RatedBy { get; set; }
        }

        [HttpGet("stats/trend")]
        public async Task<IActionResult> StatsTrend(
            [FromQuery] string? scope,
            [FromQuery] int id,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;

            var today = DateTime.UtcNow.AddHours(3).Date;
            var from = (fromDate ?? today.AddDays(-30)).Date;
            var to = (toDate ?? today).Date;
            return Ok(await _stats.GetTrendAsync(scope ?? "branch", id, from, to, ct));
        }

        /// <summary>زبائن التقييم حسب النطاق والمستوى — للضغط على المعدل/الحالات في تطبيق مدير الفرع.</summary>
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
            [FromQuery] bool phantom = false,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;

            var today = DateTime.UtcNow.AddHours(3).Date;
            var from = (fromDate ?? today.AddDays(-30)).Date;
            var to = (toDate ?? today).Date;
            return Ok(await _stats.GetRatingCustomersAsync(
                scope ?? "branch", id, level, from, to, ct, take, skip, q, phantom));
        }

        /// <summary>إحصائيات الجدد/المصفرين للشهر الحالي والسابق — فرع / قائمة / متابع / موظف.</summary>
        [HttpGet("stats/branch-month")]
        public async Task<IActionResult> StatsBranchMonth(
            [FromQuery] string? scope,
            [FromQuery] int id = 0,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            return Ok(await _monthStats.GetAsync(scope ?? "branch", id, ct));
        }

        /// <summary>إشعارات نزول التقييم ≥ 0.3 خلال 7 أيام للمتابعين وموظفي المبيعات.</summary>
        [HttpGet("stats/decline-alerts")]
        public async Task<IActionResult> ListDeclineAlerts(
            [FromQuery] bool unreadOnly = false,
            [FromQuery] bool refresh = true,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            if (refresh)
            {
                await _declineAlerts.RefreshAsync(ct);
            }

            return Ok(await _declineAlerts.ListAsync(unreadOnly, ct));
        }

        [HttpGet("stats/decline-alerts/{id:int}")]
        public async Task<IActionResult> GetDeclineAlert(int id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var row = await _declineAlerts.GetAsync(id, ct);
            return row == null ? NotFound(new { message = "التنبيه غير موجود" }) : Ok(row);
        }

        [HttpPost("stats/decline-alerts/{id:int}/read")]
        public async Task<IActionResult> MarkDeclineAlertRead(int id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var ok = await _declineAlerts.MarkReadAsync(id, ct);
            return ok ? Ok(new { ok = true }) : NotFound(new { message = "التنبيه غير موجود" });
        }

        /// <summary>زبائن انخفض تقييمهم من ممتاز إلى سيء/متلكئ خلال 15 يوماً — مدير فرع + محاسب.</summary>
        [HttpGet("customers/declining")]
        public async Task<IActionResult> ListDecliningCustomers(
            [FromQuery] bool refresh = true,
            CancellationToken ct = default)
        {
            var gate = await GateDecliningReadAsync(ct);
            if (gate != null) return gate;
            if (refresh)
            {
                await _customerDeclines.RefreshAsync(ct);
            }

            return Ok(await _customerDeclines.ListDecliningAsync(ct));
        }

        [HttpPost("sales-requests")]
        public async Task<IActionResult> CreateSalesRequest([FromBody] SalesRequestCreateDTO body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            try
            {
                return Ok(await _requests.CreateAsync(identity, body ?? new SalesRequestCreateDTO(), ct));
            }
            catch (SalesCompleteException ex)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        public sealed class DelegatedManagerRequestBody
        {
            public string? Message { get; set; }
            public string? Subject { get; set; }
        }

        [HttpPost("delegated-manager-requests")]
        public async Task<IActionResult> CreateDelegatedManagerRequest(
            [FromBody] DelegatedManagerRequestBody? body,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            var message = (body?.Message ?? "").Trim();
            if (message.Length < 10)
            {
                return BadRequest(new { message = "اكتب 10 أحرف على الأقل" });
            }

            if (message.Length > 2000)
            {
                return BadRequest(new { message = "الحد الأقصى 2000 حرف" });
            }

            var subject = string.IsNullOrWhiteSpace(body?.Subject)
                ? "طلب من مدير الفرع"
                : body!.Subject!.Trim();

            var forwarded = await _complaints.ForwardAsync(new BranchManagerComplaintForwardRequest
            {
                Message = message,
                Subject = subject,
                CityValue = identity.BranchId,
                CityName = identity.BranchName,
                SenderUserId = identity.EmployeeId.ToString(),
                SenderUserName = identity.EmployeeName,
                SenderDisplayName = string.IsNullOrWhiteSpace(identity.EmployeeName)
                    ? "مدير فرع"
                    : identity.EmployeeName,
                SenderRole = SalesRoles.UserTypeBranchManager,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    branchId = identity.BranchId,
                    configuredBranch = _configuration["SalesManagement:BranchId"]
                })
            }, ct);

            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر إرسال الطلب" });
            }

            return Ok(new { message = "تم إرسال الطلب إلى المدير المفوض", detail = forwarded.ResponseBody });
        }

        public sealed class DirectorNoteBody
        {
            public string? Message { get; set; }
            public string? Subject { get; set; }
        }

        /// <summary>Note to المدير (حيدر) — separate from المفوض complaints inbox.</summary>
        [HttpPost("director-notes")]
        public async Task<IActionResult> CreateDirectorNote(
            [FromBody] DirectorNoteBody? body,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            var message = (body?.Message ?? "").Trim();
            if (message.Length < 10)
                return BadRequest(new { message = "اكتب 10 أحرف على الأقل" });
            if (message.Length > 2000)
                return BadRequest(new { message = "الحد الأقصى 2000 حرف" });

            var subject = string.IsNullOrWhiteSpace(body?.Subject)
                ? "ملاحظة مدير فرع"
                : body!.Subject!.Trim();

            var cityLabel = BranchDisplayNames.Resolve(identity.BranchName, identity.BranchId);
            var forwarded = await _directorNotes.ForwardAsync(new BranchManagerDirectorNoteForwardRequest
            {
                Message = message,
                Subject = subject,
                CityValue = identity.BranchId,
                CityName = cityLabel,
                SenderUserId = identity.EmployeeId.ToString(),
                SenderUserName = identity.EmployeeName,
                SenderDisplayName = string.IsNullOrWhiteSpace(identity.EmployeeName)
                    ? "مدير فرع"
                    : identity.EmployeeName,
                SenderRole = SalesRoles.UserTypeBranchManager,
                MetadataJson = JsonSerializer.Serialize(new
                {
                    branchId = identity.BranchId,
                    configuredBranch = _configuration["SalesManagement:BranchId"],
                    cityName = cityLabel
                })
            }, ct);

            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر إرسال الملاحظة" });
            }

            return Ok(new { message = "تم إرسال الملاحظة إلى المدير بنجاح" });
        }

        public sealed class WaiverCreateBody
        {
            public int CustomerId { get; set; }
            public string? CustomerName { get; set; }
            public string? CustomerPhone { get; set; }
            public string? Note { get; set; }
        }

        [HttpPost("waivers")]
        public async Task<IActionResult> CreateWaiver([FromBody] WaiverCreateBody? body, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            var note = (body?.Note ?? "").Trim();
            if (string.IsNullOrWhiteSpace(note))
            {
                return BadRequest(new { message = "ملاحظة مدير الفرع إجبارية" });
            }

            if (body is null || body.CustomerId <= 0)
            {
                return BadRequest(new { message = "معرّف الزبون مطلوب" });
            }

            var forwarded = await _waivers.CreateAsync(new CustomerWaiverCreateForwardRequest
            {
                CityValue = identity.BranchId,
                CityName = identity.BranchName,
                CustomerId = body.CustomerId,
                CustomerName = body.CustomerName,
                CustomerPhone = body.CustomerPhone,
                Note = note,
                RequesterUserId = identity.EmployeeId.ToString(),
                RequesterUserName = identity.EmployeeName,
                RequesterDisplayName = string.IsNullOrWhiteSpace(identity.EmployeeName)
                    ? "مدير فرع"
                    : identity.EmployeeName,
                Role = SalesRoles.UserTypeBranchManager
            }, ct);

            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر إرسال طلب التنازل" });
            }

            return Content(forwarded.ResponseBody ?? "{}", "application/json");
        }

        [HttpGet("waivers")]
        public async Task<IActionResult> ListWaivers(
            [FromQuery] string? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            var forwarded = await _waivers.ListAsync(new CustomerWaiverListForwardRequest
            {
                Status = status,
                CityValue = identity.BranchId,
                Page = page,
                PageSize = pageSize,
                RequesterUserName = identity.EmployeeName
            }, ct);

            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر تحميل طلبات التنازل" });
            }

            return Content(forwarded.ResponseBody ?? "{}", "application/json");
        }

        [HttpGet("wrong-sales")]
        public async Task<IActionResult> ListWrongSales(
            [FromQuery] bool reviewed = false,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            CancellationToken ct = default)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            var forwarded = await _wrongSales.ListAsync(new WrongSaleListForwardRequest
            {
                Reviewed = reviewed,
                CityValue = identity.BranchId,
                Page = page,
                PageSize = pageSize
            }, ct);

            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر تحميل المبيعات الخاطئة" });
            }

            return Content(forwarded.ResponseBody ?? "{}", "application/json");
        }

        [HttpGet("wrong-sales/{id:guid}")]
        public async Task<IActionResult> GetWrongSale(Guid id, CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var forwarded = await _wrongSales.GetAsync(id, ct);
            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر تحميل تفاصيل المبيع الخاطئ" });
            }

            return Content(forwarded.ResponseBody ?? "{}", "application/json");
        }

        public sealed class WrongSaleReviewBody
        {
            public string? Note { get; set; }
        }

        [HttpPost("wrong-sales/{id:guid}/review")]
        public async Task<IActionResult> ReviewWrongSale(
            Guid id,
            [FromBody] WrongSaleReviewBody? body,
            CancellationToken ct)
        {
            var gate = await GateAsync(ct);
            if (gate != null) return gate;
            var identity = _identity.FromAuthenticatedUser()!;
            var note = (body?.Note ?? "").Trim();
            if (note.Length < 3)
            {
                return BadRequest(new { message = "ملاحظة المراجعة إجبارية" });
            }

            var forwarded = await _wrongSales.ReviewAsync(id, new WrongSaleReviewForwardRequest
            {
                Note = note,
                ReviewerUserName = identity.EmployeeName,
                ReviewerDisplayName = string.IsNullOrWhiteSpace(identity.EmployeeName)
                    ? "مدير فرع"
                    : identity.EmployeeName
            }, ct);

            if (!forwarded.Ok)
            {
                return StatusCode(
                    forwarded.FailureStatusCode <= 0 ? StatusCodes.Status502BadGateway : forwarded.FailureStatusCode,
                    new { message = forwarded.ErrorMessage ?? "تعذر تسجيل المراجعة" });
            }

            return Content(forwarded.ResponseBody ?? "{}", "application/json");
        }
    }
}
