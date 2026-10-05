using BE_SalesEmployee.MainFollower.Domain;
using BE_SalesEmployee.MainFollower.Services;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

[Route("api/main-follower")]
[ApiController]
[Authorize(Policy = SalesPolicies.MainFollower)]
public sealed class MainFollowerController : ControllerBase
{
    private readonly CommitteeService _committees;
    private readonly SaleReviewService _saleReviews;
    private readonly StaffReviewService _staffReviews;
    private readonly CommitteeHistoryService _history;
    private readonly MainFollowerShiftService _shifts;
    private readonly ISalesManagerBranchAggregator _branches;

    public MainFollowerController(
        CommitteeService committees,
        SaleReviewService saleReviews,
        StaffReviewService staffReviews,
        CommitteeHistoryService history,
        MainFollowerShiftService shifts,
        ISalesManagerBranchAggregator branches)
    {
        _committees = committees;
        _saleReviews = saleReviews;
        _staffReviews = staffReviews;
        _history = history;
        _shifts = shifts;
        _branches = branches;
    }

    public sealed class ShiftLocationBody
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public double? AccuracyMeters { get; set; }
        public DateTime? RecordedAtUtc { get; set; }
    }

    [HttpGet("shift/current")]
    public async Task<IActionResult> CurrentShift(CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var row = await _shifts.GetCurrentAsync(actor.UserName, ct);
        return Ok(row == null ? null : ToShiftDto(row));
    }

    [HttpPost("shift/start")]
    public async Task<IActionResult> StartShift([FromBody] ShiftLocationBody? body, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        try
        {
            var row = await _shifts.StartAsync(
                actor.UserName,
                actor.UserName,
                body?.Latitude,
                body?.Longitude,
                ct);
            return Ok(ToShiftDto(row));
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("shift/end")]
    public async Task<IActionResult> EndShift([FromBody] ShiftLocationBody? body, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        try
        {
            var row = await _shifts.EndAsync(actor.UserName, body?.Latitude, body?.Longitude, ct);
            return Ok(ToShiftDto(row));
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("shift/location")]
    public async Task<IActionResult> RecordLocation([FromBody] ShiftLocationBody? body, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        try
        {
            var point = await _shifts.RecordLocationAsync(
                actor.UserName,
                body?.Latitude ?? 0,
                body?.Longitude ?? 0,
                body?.AccuracyMeters,
                body?.RecordedAtUtc,
                ct);
            return Ok(new
            {
                id = point.Id,
                shiftId = point.ShiftId,
                latitude = point.Latitude,
                longitude = point.Longitude,
                accuracyMeters = point.AccuracyMeters,
                recordedAtUtc = point.RecordedAtUtc
            });
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("missions")]
    public async Task<IActionResult> MyMissions(CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var rows = await _committees.ListMissionsForMemberAsync(actor.UserName, ct);
        return Ok(rows.Select(ToMissionDto));
    }

    [HttpGet("missions/{missionId:int}/cities/{cityValue}/sales")]
    public async Task<IActionResult> MissionCitySales(int missionId, string cityValue, CancellationToken ct)
    {
        var access = await AuthorizeCityAsync(missionId, cityValue, ct);
        if (access != null) return access;

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _branches.GetExactBranchArrayAsync(
            actor, cityValue, "sales-manager/sales", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("missions/{missionId:int}/cities/{cityValue}/delegates")]
    public Task<IActionResult> MissionCityDelegates(int missionId, string cityValue, CancellationToken ct) =>
        ProxyStaffListAsync(missionId, cityValue, "sales-manager/delegates", ct);

    [HttpGet("missions/{missionId:int}/cities/{cityValue}/followers")]
    public Task<IActionResult> MissionCityFollowers(int missionId, string cityValue, CancellationToken ct) =>
        ProxyStaffListAsync(missionId, cityValue, "sales-manager/legal-followups/followers", ct);

    [HttpGet("missions/{missionId:int}/cities/{cityValue}/sales-employees")]
    public Task<IActionResult> MissionCitySalesEmployees(int missionId, string cityValue, CancellationToken ct) =>
        ProxyStaffListAsync(missionId, cityValue, "sales-manager/employees", ct);

    public sealed class SaleReviewBody
    {
        public int MissionId { get; set; }
        public string? CityValue { get; set; }
        public int SaleId { get; set; }
        public string? CustomerName { get; set; }
        public int Score { get; set; }
        public string? Note { get; set; }
        public decimal ShopStockEstimatedValue { get; set; }
        public decimal EstimatedDailyRevenue { get; set; }
    }

    [HttpPost("sale-reviews")]
    public async Task<IActionResult> SubmitSaleReview([FromBody] SaleReviewBody body, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        try
        {
            var row = await _saleReviews.SubmitAsync(
                actor.UserName,
                body?.MissionId ?? 0,
                body?.CityValue ?? "",
                body?.SaleId ?? 0,
                body?.CustomerName,
                body?.Score ?? 0,
                body?.Note,
                body?.ShopStockEstimatedValue ?? 0,
                body?.EstimatedDailyRevenue ?? 0,
                ct);
            return Ok(ToSaleReviewDto(row));
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("missions/{missionId:int}/sale-reviews")]
    public async Task<IActionResult> ListSaleReviews(int missionId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var missions = await _committees.ListMissionsForMemberAsync(actor.UserName, ct);
        if (missions.All(m => m.Id != missionId))
        {
            return Forbid();
        }

        var rows = await _saleReviews.ListForMissionAsync(missionId, ct);
        return Ok(rows.Select(ToSaleReviewDto));
    }

    public sealed class StaffReviewBody
    {
        public int MissionId { get; set; }
        public string? CityValue { get; set; }
        public string? TargetType { get; set; }
        public int TargetId { get; set; }
        public string? TargetName { get; set; }
        public int? ListId { get; set; }
        public int Score { get; set; }
        public string? Note { get; set; }
    }

    [HttpPost("staff-reviews")]
    public async Task<IActionResult> SubmitStaffReview([FromBody] StaffReviewBody body, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        try
        {
            var row = await _staffReviews.SubmitAsync(
                actor.UserName,
                body?.MissionId ?? 0,
                body?.CityValue ?? "",
                body?.TargetType ?? "",
                body?.TargetId ?? 0,
                body?.TargetName,
                body?.ListId,
                body?.Score ?? 0,
                body?.Note,
                ct);
            return Ok(ToStaffReviewDto(row));
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("missions/{missionId:int}/staff-reviews")]
    public async Task<IActionResult> ListMissionStaffReviews(int missionId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var missions = await _committees.ListMissionsForMemberAsync(actor.UserName, ct);
        if (missions.All(m => m.Id != missionId))
        {
            return Forbid();
        }

        var rows = await _staffReviews.ListForMissionAsync(missionId, ct);
        return Ok(rows.Select(ToStaffReviewDto));
    }

    /// <summary>Durable notes feed for a staff target (survives revisiting the province later).</summary>
    [HttpGet("staff/{targetType}/{targetId:int}/notes")]
    public async Task<IActionResult> StaffNotesFeed(
        string targetType,
        int targetId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        try
        {
            var missions = await _committees.ListMissionsForMemberAsync(actor.UserName, ct);
            var allowedCities = MissionAccessRules.AllowedCitiesForMember(missions, actor.UserName);
            var cityErr = MissionAccessRules.AuthorizeOptionalCityFilter(allowedCities, cityValue);
            if (cityErr != null)
            {
                return BadRequest(new { message = cityErr });
            }

            // Scope comes from server-side assignments. Caller city is an optional narrow filter only.
            var filterCity = string.IsNullOrWhiteSpace(cityValue) ? null : cityValue.Trim();
            var rows = await _staffReviews.ListForTargetAsync(targetType, targetId, filterCity, ct);
            var scoped = rows
                .Where(r => MissionAccessRules.CityIsAllowed(allowedCities, r.CityValue))
                .ToList();

            return Ok(scoped.Select(r => new
            {
                id = r.Id,
                missionId = r.MissionId,
                committeeName = r.CommitteeName,
                cityValue = r.CityValue,
                score = r.Score,
                note = r.FeedNote,
                userNote = r.UserNote,
                reviewerUserName = r.ReviewerUserName,
                createdAtUtc = r.CreatedAtUtc
            }));
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Cross-mission timeline for a province the member is (or was) assigned to.</summary>
    [HttpGet("cities/{cityValue}/history")]
    public async Task<IActionResult> CityHistory(string cityValue, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        try
        {
            var rows = await _history.ListCityHistoryForMemberAsync(actor.UserName, cityValue, ct);
            return Ok(rows.Select(ToHistoryDto));
        }
        catch (CommitteeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Read-only customer profile (مستمسكات، ملاحظات، مبيعات، تقييمات) for an assigned mission city.</summary>
    [HttpGet("missions/{missionId:int}/cities/{cityValue}/customers/{customerId:int}/profile")]
    public async Task<IActionResult> CustomerProfileById(
        int missionId,
        string cityValue,
        int customerId,
        CancellationToken ct)
    {
        var access = await AuthorizeCityAsync(missionId, cityValue, ct);
        if (access != null) return access;

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _branches.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/customers/{customerId}/profile",
            ct);
        return ProfileResult(status, payload, missionId, cityValue);
    }

    [HttpGet("missions/{missionId:int}/cities/{cityValue}/customers/profile")]
    public async Task<IActionResult> CustomerProfileLookup(
        int missionId,
        string cityValue,
        [FromQuery] int? customerId,
        [FromQuery] string? name,
        [FromQuery] string? phone,
        CancellationToken ct)
    {
        var access = await AuthorizeCityAsync(missionId, cityValue, ct);
        if (access != null) return access;

        var q = BuildQuery(
            ("customerId", customerId?.ToString()),
            ("name", name),
            ("phone", phone));
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _branches.GetOneAsync(
            actor,
            cityValue,
            "sales-manager/customers/profile" + q,
            ct);
        return ProfileResult(status, payload, missionId, cityValue);
    }

    [HttpGet("missions/{missionId:int}/cities/{cityValue}/customers/{customerId:int}/notes")]
    public async Task<IActionResult> CustomerNotes(
        int missionId,
        string cityValue,
        int customerId,
        CancellationToken ct)
    {
        var access = await AuthorizeCityAsync(missionId, cityValue, ct);
        if (access != null) return access;

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _branches.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/customers/{customerId}/notes",
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("missions/{missionId:int}/cities/{cityValue}/sales/{saleId:int}/documents")]
    public async Task<IActionResult> SaleDocuments(
        int missionId,
        string cityValue,
        int saleId,
        CancellationToken ct)
    {
        var access = await AuthorizeCityAsync(missionId, cityValue, ct);
        if (access != null) return access;

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _branches.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/sales/{saleId}/documents",
            ct);
        return SaleDocumentsResult(status, payload, missionId, cityValue, saleId);
    }

    [HttpGet("missions/{missionId:int}/cities/{cityValue}/sales/{saleId:int}/documents/{documentId:int}/download")]
    public async Task<IActionResult> SaleDocumentDownload(
        int missionId,
        string cityValue,
        int saleId,
        int documentId,
        CancellationToken ct)
    {
        var access = await AuthorizeCityAsync(missionId, cityValue, ct);
        if (access != null) return access;

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _branches.GetFileAsync(
            actor,
            cityValue,
            $"sales-manager/sales/{saleId}/documents/{documentId}/download",
            ct);
        if (payload is ValueTuple<byte[], string> file)
        {
            return File(file.Item1, file.Item2, $"sale-{saleId}-doc-{documentId}");
        }

        if (status == StatusCodes.Status404NotFound)
        {
            return NotFound(new { message = "ملف مستمسك المبيع غير موجود على الفرع." });
        }

        return StatusCode(status, payload);
    }

    [HttpGet("missions/{missionId:int}/cities/{cityValue}/customer-documents/{documentId:int}/file")]
    public async Task<IActionResult> CustomerDocumentFile(
        int missionId,
        string cityValue,
        int documentId,
        CancellationToken ct)
    {
        var access = await AuthorizeCityAsync(missionId, cityValue, ct);
        if (access != null) return access;

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _branches.GetFileAsync(
            actor,
            cityValue,
            $"sales-manager/customer-documents/{documentId}/file",
            ct);
        if (payload is ValueTuple<byte[], string> file)
        {
            return File(file.Item1, file.Item2, $"customer-doc-{documentId}");
        }

        if (status == StatusCodes.Status404NotFound)
        {
            return NotFound(new { message = "صورة المستمسك غير موجودة على الفرع." });
        }

        return StatusCode(status, payload);
    }

    private static string BuildQuery(params (string Key, string? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}")
            .ToList();
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    private IActionResult ProfileResult(int status, object? payload, int missionId, string cityValue)
    {
        if (status is >= 200 and < 300)
        {
            payload = MainFollowerProfileUrlRewriter.RewriteCustomerProfile(payload, missionId, cityValue);
        }
        else if (status == StatusCodes.Status404NotFound)
        {
            return NotFound(new { message = "ملف الزبون غير موجود في هذه المحافظة." });
        }

        return StatusCode(status, payload);
    }

    private IActionResult SaleDocumentsResult(
        int status,
        object? payload,
        int missionId,
        string cityValue,
        int saleId)
    {
        if (status is >= 200 and < 300)
        {
            payload = MainFollowerProfileUrlRewriter.RewriteSaleDocuments(payload, missionId, cityValue, saleId);
        }
        else if (status == StatusCodes.Status404NotFound)
        {
            return NotFound(new { message = "مستمسكات المبيع غير موجودة." });
        }

        return StatusCode(status, payload);
    }

    private async Task<IActionResult?> AuthorizeCityAsync(int missionId, string cityValue, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var missions = await _committees.ListMissionsForMemberAsync(actor.UserName, ct);
        var mission = missions.FirstOrDefault(m => m.Id == missionId);
        var err = MissionAccessRules.AuthorizeMemberCity(mission, actor.UserName, cityValue);
        return err == null ? null : BadRequest(new { message = err });
    }

    private async Task<IActionResult> ProxyStaffListAsync(
        int missionId,
        string cityValue,
        string companyPath,
        CancellationToken ct)
    {
        var access = await AuthorizeCityAsync(missionId, cityValue, ct);
        if (access != null) return access;

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _branches.GetExactBranchArrayAsync(actor, cityValue, companyPath, ct);
        return StatusCode(status, payload);
    }

    private static object ToShiftDto(MainFollowerShiftRecord row) => new
    {
        id = row.Id,
        userName = row.UserName,
        displayName = row.DisplayName,
        startedAtUtc = row.StartedAtUtc,
        endedAtUtc = row.EndedAtUtc,
        startLatitude = row.StartLatitude,
        startLongitude = row.StartLongitude,
        endLatitude = row.EndLatitude,
        endLongitude = row.EndLongitude,
        isActive = row.EndedAtUtc == null
    };

    private static object ToMissionDto(MissionRecord row) => new
    {
        id = row.Id,
        committeeId = row.CommitteeId,
        committeeName = row.CommitteeName,
        status = row.Status,
        note = row.Note,
        cityValues = row.CityValues,
        memberUserNames = row.MemberUserNames,
        createdAtUtc = row.CreatedAtUtc
    };

    private static object ToSaleReviewDto(SaleReviewRecord row) => new
    {
        id = row.Id,
        missionId = row.MissionId,
        cityValue = row.CityValue,
        saleId = row.SaleId,
        customerName = row.CustomerName,
        score = row.Score,
        note = row.Note,
        shopStockEstimatedValue = row.ShopStockEstimatedValue,
        estimatedDailyRevenue = row.EstimatedDailyRevenue,
        reviewerUserName = row.ReviewerUserName,
        createdAtUtc = row.CreatedAtUtc
    };

    private static object ToStaffReviewDto(StaffReviewRecord row) => new
    {
        id = row.Id,
        missionId = row.MissionId,
        committeeName = row.CommitteeName,
        cityValue = row.CityValue,
        targetType = row.TargetType,
        targetId = row.TargetId,
        targetName = row.TargetName,
        listId = row.ListId,
        score = row.Score,
        note = row.UserNote,
        feedNote = row.FeedNote,
        reviewerUserName = row.ReviewerUserName,
        createdAtUtc = row.CreatedAtUtc
    };

    private static object ToHistoryDto(MissionHistoryEntry row) => new
    {
        kind = row.Kind,
        atUtc = row.AtUtc,
        missionId = row.MissionId,
        committeeName = row.CommitteeName,
        cityValue = row.CityValue,
        score = row.Score,
        title = row.Title,
        note = row.Note,
        reviewerUserName = row.ReviewerUserName
    };
}
