using System.Text.Json.Nodes;
using BE_SalesEmployee.DelegatedManager;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Sales.Stores;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_SalesEmployee.Controllers;

/// <summary>
/// Central director (+ monitor: follower-ops and ratings read).
/// Class policy: DirectorOrMonitor. Director-only endpoints add <see cref="SalesPolicies.Director"/>.
/// </summary>
[Route("api/director")]
[ApiController]
[Authorize(Policy = SalesPolicies.DirectorOrMonitor)]
public sealed class DirectorController : ControllerBase
{
    private readonly ISalesManagerBranchAggregator _aggregator;
    private readonly CustomerWaiverService _waivers;
    private readonly ISalesEmployeeAppBlockStore _appBlocks;

    public DirectorController(
        ISalesManagerBranchAggregator aggregator,
        CustomerWaiverService waivers,
        ISalesEmployeeAppBlockStore appBlocks)
    {
        _aggregator = aggregator;
        _waivers = waivers;
        _appBlocks = appBlocks;
    }

    public sealed class SalesEmployeeAppBlockBody
    {
        public bool IsBlocked { get; set; }
    }

    [HttpGet("cities")]
    public async Task<IActionResult> Cities(CancellationToken ct)
    {
        var branches = await _aggregator.BranchesAsync(ct);
        return Ok(branches.Select(b => new
        {
            value = b.Value,
            name = b.Name,
            database = b.Database
        }));
    }

    /// <summary>أسماء قانونية (عرض فقط) — لوحة ضعفاء الأسبوع.</summary>
    [HttpGet("customer-decisions/weak-week-payers")]
    public async Task<IActionResult> LegalNamesWeakWeekPayers(
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, DirectorLegalNamesPaths.WeakWeekPayers(), ct);
        return StatusCode(status, payload);
    }

    /// <summary>أسماء قانونية — طلبات التحويل (قراءة).</summary>
    [HttpGet("customer-decisions/legal-conversion-requests")]
    public async Task<IActionResult> LegalNamesLegalConversionRequests(
        [FromQuery] string? cityValue,
        [FromQuery] string? status,
        [FromQuery] int take = 100,
        [FromQuery] int skip = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var path = DirectorLegalNamesPaths.LegalConversionRequests(status, take, skip);
        var (st, payload) = await _aggregator.GetOneAsync(actor, cityValue, path, ct);
        return StatusCode(st, payload);
    }

    /// <summary>موافقة تحويل قانونية — المراقب فقط.</summary>
    [HttpPut("customer-decisions/legal-conversion-requests/{requestId:int}/approve")]
    [Authorize(Policy = SalesPolicies.Monitor)]
    public async Task<IActionResult> ApproveLegalConversion(
        int requestId,
        [FromQuery] string? cityValue,
        [FromBody] LegalConversionReviewBody? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var json = System.Text.Json.JsonSerializer.Serialize(new { note = body?.Note });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var (st, payload) = await _aggregator.SendContentAsync(
            actor,
            cityValue,
            DirectorLegalNamesPaths.LegalConversionApprove(requestId),
            HttpMethod.Put,
            content,
            ct,
            actorRole: "Monitor");
        return StatusCode(st, payload);
    }

    /// <summary>رفض تحويل قانونية — المراقب فقط؛ ملاحظة إلزامية.</summary>
    [HttpPut("customer-decisions/legal-conversion-requests/{requestId:int}/reject")]
    [Authorize(Policy = SalesPolicies.Monitor)]
    public async Task<IActionResult> RejectLegalConversion(
        int requestId,
        [FromQuery] string? cityValue,
        [FromBody] LegalConversionReviewBody? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        if (string.IsNullOrWhiteSpace(body?.Note))
            return BadRequest(new { message = "ملاحظة الرفض مطلوبة" });

        var actor = TokenService.FromPrincipal(User);
        var json = System.Text.Json.JsonSerializer.Serialize(new { note = body.Note });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var (st, payload) = await _aggregator.SendContentAsync(
            actor,
            cityValue,
            DirectorLegalNamesPaths.LegalConversionReject(requestId),
            HttpMethod.Put,
            content,
            ct,
            actorRole: "Monitor");
        return StatusCode(st, payload);
    }

    /// <summary>تبويب حيدر — كل طلبات التنازل بكل الحالات.</summary>
    [HttpGet("waivers")]
    [Authorize(Policy = SalesPolicies.Director)]
    public async Task<IActionResult> ListWaivers(
        [FromQuery] string? status,
        [FromQuery] string? cityValue,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await _waivers.ListAsync(new CustomerWaiverQuery
        {
            Page = page <= 0 ? 1 : page,
            PageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 200),
            Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim(),
            CityValue = string.IsNullOrWhiteSpace(cityValue) ? null : cityValue.Trim(),
        }, ct);

        return Ok(new
        {
            page = result.Page,
            pageSize = result.PageSize,
            total = result.TotalCount,
            items = result.Items.Select(DelegatedManagerController.ToWaiverSummary),
        });
    }

    /// <summary>أسماء قانونية — طلبات التنازل للمحاسب (قراءة).</summary>
    [HttpGet("customer-decisions/waiver-accountant-requests")]
    public async Task<IActionResult> LegalNamesWaiverAccountantRequests(
        [FromQuery] string? cityValue,
        [FromQuery] string? status,
        [FromQuery] int take = 100,
        [FromQuery] int skip = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var path = DirectorLegalNamesPaths.WaiverAccountantRequests(status, take, skip);
        var (st, payload) = await _aggregator.GetOneAsync(actor, cityValue, path, ct);
        return StatusCode(st, payload);
    }

    /// <summary>أسماء قانونية — سجل القرارات (قراءة).</summary>
    [HttpGet("customer-decisions/decisions")]
    public async Task<IActionResult> LegalNamesDecisions(
        [FromQuery] string? cityValue,
        [FromQuery] string? decisionType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? customerID,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });

        var actor = TokenService.FromPrincipal(User);
        var path = DirectorLegalNamesPaths.Decisions(decisionType, fromDate, toDate, customerID);
        var (st, payload) = await _aggregator.GetOneAsync(actor, cityValue, path, ct);
        return StatusCode(st, payload);
    }

    /// <summary>Ratings catalog — director + monitor.</summary>
    [HttpGet("stats/catalog")]
    public async Task<IActionResult> StatsCatalog([FromQuery] string? cityValue, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyCatalog,
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>
    /// New/zeroed/net month stats per province. Omit cityValue to fan-out all branches.
    /// Director + monitor.
    /// </summary>
    [HttpGet("stats/branch-month")]
    public async Task<IActionResult> StatsBranchMonth([FromQuery] string? cityValue, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            var (statusAll, payloadAll) = await _aggregator.GetObjectFanoutAsync(
                actor,
                DelegatedManagerStatsPaths.CompanyBranchMonth,
                ct);
            return StatusCode(statusAll, payloadAll);
        }

        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyBranchMonth,
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>Ratings trend — director + monitor.</summary>
    [HttpGet("stats/trend")]
    public async Task<IActionResult> StatsTrend(
        [FromQuery] string? cityValue,
        [FromQuery] string? scope,
        [FromQuery] int id,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var today = DateTime.UtcNow.AddHours(3).Date;
        var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var to = (toDate ?? today).Date;
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyTrend(scope, id, from, to),
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>مجموع تسديدات قائمة ضمن فترة — مراقب/مدير.</summary>
    [HttpGet("stats/list-payments-sum")]
    public async Task<IActionResult> StatsListPaymentsSum(
        [FromQuery] string? cityValue,
        [FromQuery] int listId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        if (listId <= 0)
        {
            return BadRequest(new { message = "معرف القائمة مطلوب." });
        }

        var today = DateTime.UtcNow.AddHours(3).Date;
        var yesterday = today.AddDays(-1);
        var from = (fromDate ?? yesterday).Date;
        var to = (toDate ?? yesterday).Date;
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyListPaymentsSum(listId, from, to),
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>زبائن مستوى تقييم واحد (ممتاز/جيد/…) — director + monitor.</summary>
    [HttpGet("stats/rating-customers")]
    public async Task<IActionResult> StatsRatingCustomers(
        [FromQuery] string? cityValue,
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
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        if (string.IsNullOrWhiteSpace(level))
        {
            return BadRequest(new { message = "يجب تحديد مستوى التقييم." });
        }

        var today = DateTime.UtcNow.AddHours(3).Date;
        var from = (fromDate ?? new DateTime(today.Year, today.Month, 1)).Date;
        var to = (toDate ?? today).Date;
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            DelegatedManagerStatsPaths.CompanyRatingCustomers(scope, id, level, from, to, take, skip, q),
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>
    /// ملخص قلعة الضمان: days×branches matrix for one metric (+ قاصة الكلية per branch).
    /// Excludes منهج cities (كوت / ميسان / عقيل).
    /// </summary>
    /// <summary>Director ملخص + monitor «ملخص المبيعات» (salePrice).</summary>
    [Authorize(Policy = SalesPolicies.DirectorOrMonitor)]
    [HttpGet("summary/matrix")]
    public async Task<IActionResult> SummaryMatrix(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] string? metric,
        CancellationToken ct)
    {
        var baghdad = DateTime.UtcNow.AddHours(3);
        if (year < 2000 || year > 2100)
        {
            year = baghdad.Year;
        }

        if (month < 1 || month > 12)
        {
            month = baghdad.Month;
        }

        // Keep camelCase metrics (salePrice / purchasePrice); do not force lower-case.
        var normalizedMetric = string.IsNullOrWhiteSpace(metric) ? "receipts" : metric.Trim();
        var actor = TokenService.FromPrincipal(User);
        var all = await _aggregator.BranchesAsync(ct);
        var targets = all
            .Where(b => DirectorSummaryPaths.IsQalaaCity(b.Value))
            .OrderBy(b => int.TryParse(b.Value, out var n) ? n : 999)
            .ThenBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tasks = targets.Select(async city =>
        {
            var path = DirectorSummaryPaths.CompanyMatrix(year, month, normalizedMetric, city.Name);
            var (status, payload) = await _aggregator.GetOneAsync(actor, city.Value, path, ct);
            return new { city, status, payload };
        }).ToList();

        var results = await Task.WhenAll(tasks);
        var branches = new List<object>();
        var failed = new List<object>();
        var daysInMonth = DateTime.DaysInMonth(year, month);

        foreach (var r in results)
        {
            if (r.status is < 200 or >= 300 || r.payload is null)
            {
                failed.Add(new
                {
                    cityValue = r.city.Value,
                    name = r.city.Name,
                    status = r.status
                });
                branches.Add(new
                {
                    cityValue = r.city.Value,
                    name = r.city.Name,
                    database = r.city.Database,
                    boxName = (string?)null,
                    boxTotalAmountDenar = 0m,
                    treasuryAmountDenar = 0m,
                    treasuryBoxName = (string?)null,
                    days = Enumerable.Range(1, daysInMonth).Select(d => new { day = d, total = 0m }).ToList(),
                    ok = false
                });
                continue;
            }

            var obj = ToJsonObject(r.payload);
            var days = ExtractDays(obj, daysInMonth);
            branches.Add(new
            {
                cityValue = r.city.Value,
                name = r.city.Name,
                database = r.city.Database,
                boxName = ReadString(obj, "boxName", "BoxName"),
                boxTotalAmountDenar = ReadDecimal(obj, "boxTotalAmountDenar", "BoxTotalAmountDenar"),
                treasuryAmountDenar = ReadDecimal(obj, "treasuryAmountDenar", "TreasuryAmountDenar"),
                treasuryBoxName = ReadString(obj, "treasuryBoxName", "TreasuryBoxName"),
                days,
                ok = true
            });
        }

        return Ok(new
        {
            year,
            month,
            metric = normalizedMetric,
            daysInMonth,
            branches,
            failed
        });
    }

    [Authorize(Policy = SalesPolicies.DirectorOrMonitor)]
    [HttpGet("summary/drilldown")]
    public async Task<IActionResult> SummaryDrilldown(
        [FromQuery] string? cityValue,
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int day,
        [FromQuery] string? metric,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        if (!DirectorSummaryPaths.IsQalaaCity(cityValue))
        {
            return BadRequest(new { message = "المحافظة غير مشمولة بملخص قلعة الضمان." });
        }

        var normalizedMetric = string.IsNullOrWhiteSpace(metric) ? "receipts" : metric.Trim();
        var actor = TokenService.FromPrincipal(User);
        var branches = await _aggregator.BranchesAsync(ct);
        var city = branches.FirstOrDefault(b =>
            string.Equals(b.Value, cityValue.Trim(), StringComparison.OrdinalIgnoreCase));
        var path = DirectorSummaryPaths.CompanyDrilldown(
            year, month, day, normalizedMetric, city?.Name);
        var (status, payload) = await _aggregator.GetOneAsync(actor, cityValue, path, ct);
        return StatusCode(status, payload);
    }

    private static JsonObject? ToJsonObject(object payload)
    {
        if (payload is JsonObject jo)
        {
            return jo;
        }

        try
        {
            return JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(payload)) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static List<object> ExtractDays(JsonObject? obj, int daysInMonth)
    {
        var result = new List<object>(daysInMonth);
        var map = new Dictionary<int, decimal>();
        var daysArr = obj?["days"] as JsonArray ?? obj?["Days"] as JsonArray;
        if (daysArr != null)
        {
            foreach (var node in daysArr)
            {
                if (node is not JsonObject d)
                {
                    continue;
                }

                var day = (int)ReadDecimal(d, "day", "Day");
                if (day < 1)
                {
                    continue;
                }

                map[day] = ReadDecimal(d, "total", "Total");
            }
        }

        for (var d = 1; d <= daysInMonth; d++)
        {
            result.Add(new { day = d, total = map.TryGetValue(d, out var t) ? t : 0m });
        }

        return result;
    }

    private static string? ReadString(JsonObject? obj, params string[] keys)
    {
        if (obj == null)
        {
            return null;
        }

        foreach (var key in keys)
        {
            if (obj[key] is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s))
            {
                return s;
            }
        }

        return null;
    }

    private static decimal ReadDecimal(JsonObject? obj, params string[] keys)
    {
        if (obj == null)
        {
            return 0m;
        }

        foreach (var key in keys)
        {
            if (obj[key] is not JsonValue v)
            {
                continue;
            }

            if (v.TryGetValue<decimal>(out var d))
            {
                return d;
            }

            if (v.TryGetValue<double>(out var dbl))
            {
                return (decimal)dbl;
            }

            if (v.TryGetValue<long>(out var l))
            {
                return l;
            }

            if (decimal.TryParse(v.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return 0m;
    }

    [Authorize(Policy = SalesPolicies.Director)]
    [HttpGet("customers/{cityValue}/{customerId:int}/profile")]
    public async Task<IActionResult> CustomerProfileById(string cityValue, int customerId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/customers/{customerId}/profile",
            ct);
        return StatusCode(status, payload);
    }

    [Authorize(Policy = SalesPolicies.Director)]
    [HttpGet("customers/{cityValue}/{customerId:int}/notes")]
    public async Task<IActionResult> CustomerNotes(string cityValue, int customerId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/customers/{customerId}/notes",
            ct);
        return StatusCode(status, payload);
    }

    [Authorize(Policy = SalesPolicies.Director)]
    [HttpGet("sales/{cityValue}/{saleId:int}/shop-image")]
    public async Task<IActionResult> SaleShopImage(string cityValue, int saleId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetFileAsync(
            actor,
            cityValue,
            $"sales-manager/sales/{saleId}/shop-image",
            ct);
        if (payload is ValueTuple<byte[], string> file)
        {
            return File(file.Item1, string.IsNullOrWhiteSpace(file.Item2) ? "image/jpeg" : file.Item2);
        }

        return StatusCode(status, payload ?? new { message = "الصورة غير موجودة." });
    }

    [Authorize(Policy = SalesPolicies.Director)]
    [HttpGet("customer-documents/{cityValue}/{documentId:int}/file")]
    public async Task<IActionResult> CustomerDocumentFile(string cityValue, int documentId, CancellationToken ct)
    {
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetFileAsync(
            actor,
            cityValue,
            $"sales-manager/customer-documents/{documentId}/file",
            ct);
        if (payload is ValueTuple<byte[], string> file)
        {
            return File(file.Item1, string.IsNullOrWhiteSpace(file.Item2) ? "image/jpeg" : file.Item2);
        }

        return StatusCode(status, payload ?? new { message = "الملف غير موجود على السيرفر." });
    }

    // ── عمل المتابعين (ops) ──────────────────────────────────────────

    [HttpGet("followers/ops/summaries")]
    public async Task<IActionResult> FollowerOpsSummaries(
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, "sales-manager/director-follower-ops/summaries", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/follower-cards")]
    public async Task<IActionResult> FollowerOpsFollowerCards(
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, "sales-manager/director-follower-ops/follower-cards", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/work-bundle")]
    public async Task<IActionResult> FollowerOpsWorkBundle(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-follower-ops/followers/{followerId}/work-bundle", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/sales-workspace")]
    public async Task<IActionResult> FollowerOpsSalesWorkspace(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-follower-ops/followers/{followerId}/sales-workspace", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/remaining-customers")]
    public async Task<IActionResult> FollowerOpsRemainingCustomers(
        int followerId,
        [FromQuery] string? cityValue,
        [FromQuery] string? bucket,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var q = string.IsNullOrWhiteSpace(bucket) ? "" : $"?bucket={Uri.EscapeDataString(bucket)}";
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-follower-ops/followers/{followerId}/remaining-customers{q}", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/sales-auth")]
    public async Task<IActionResult> FollowerOpsSalesAuth(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-follower-ops/followers/{followerId}/sales-auth", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/declining")]
    public async Task<IActionResult> FollowerOpsDeclining(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-follower-ops/followers/{followerId}/declining", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/notes")]
    public async Task<IActionResult> FollowerOpsNotes(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-follower-ops/followers/{followerId}/notes", ct);
        return StatusCode(status, payload);
    }

    /// <summary>الزبائن القديمة = قانونية مدير المبيعات لهذا المتابع.</summary>
    [HttpGet("followers/ops/{followerId:int}/legal-followups")]
    public async Task<IActionResult> FollowerOpsLegalFollowups(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-follower-ops/followers/{followerId}/legal-followups", ct);
        return StatusCode(status, payload);
    }

    /// <summary>إلغاء إرسال زبون قديم بالغلط → غير مسند في لوحة المبيعات.</summary>
    [HttpPost("followers/ops/legal-followups/{caseId:int}/cancel")]
    public async Task<IActionResult> FollowerOpsCancelLegalFollowup(
        int caseId,
        [FromQuery] string? cityValue,
        [FromBody] LegalFollowupCancelBody? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        if (caseId <= 0)
        {
            return BadRequest(new { message = "معرّف المتابعة غير صالح." });
        }

        var actor = TokenService.FromPrincipal(User);
        var json = System.Text.Json.JsonSerializer.Serialize(new { reason = body?.Reason ?? "" });
        var (status, payload) = await _aggregator.PostAsync(
            actor,
            cityValue,
            $"sales-manager/legal-followups/{caseId}/cancel",
            json,
            ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/delegate-notes")]
    public async Task<IActionResult> FollowerOpsDelegateNotes(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-follower-ops/followers/{followerId}/delegate-notes", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/ops/{followerId:int}/collection")]
    public async Task<IActionResult> FollowerOpsCollection(
        int followerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-follower-ops/followers/{followerId}/collection", ct);
        return StatusCode(status, payload);
    }

    [HttpPut("followers/ops/lists/{listId:int}/collection-enabled")]
    public async Task<IActionResult> FollowerOpsSetCollectionEnabled(
        int listId,
        [FromQuery] string? cityValue,
        [FromBody] CollectionEnabledBody? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        if (listId <= 0 || body is null)
        {
            return BadRequest(new { message = "معرّف القائمة والحالة مطلوبان" });
        }

        var actor = TokenService.FromPrincipal(User);
        var json = System.Text.Json.JsonSerializer.Serialize(new { enabled = body.Enabled });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var (status, payload) = await _aggregator.SendContentAsync(
            actor,
            cityValue,
            $"sales-manager/director-follower-ops/lists/{listId}/collection-enabled",
            HttpMethod.Put,
            content,
            ct);
        return StatusCode(status, payload);
    }

    // ── متابعة المندوبين (مدير + مراقب) ─────────────────────────────

    [HttpGet("delegate-followup/lists")]
    public async Task<IActionResult> DelegateFollowupLists(
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, "sales-manager/director-delegate-followup/lists", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("delegate-followup/lists/{listId:int}/summary")]
    public async Task<IActionResult> DelegateFollowupListSummary(
        int listId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-delegate-followup/lists/{listId}/summary", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("delegate-followup/lists/{listId:int}/customers")]
    public async Task<IActionResult> DelegateFollowupListCustomers(
        int listId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-delegate-followup/lists/{listId}/customers", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("delegate-followup/lists/{listId:int}/board-operator-rating")]
    public async Task<IActionResult> DelegateFollowupGetBoardOperatorRating(
        int listId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor,
            cityValue,
            $"sales-manager/director-delegate-followup/lists/{listId}/board-operator-rating",
            ct);
        return StatusCode(status, payload);
    }

    [HttpPost("delegate-followup/lists/{listId:int}/board-operator-rating")]
    public async Task<IActionResult> DelegateFollowupUpsertBoardOperatorRating(
        int listId,
        [FromQuery] string? cityValue,
        [FromBody] object? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        if (body is null)
        {
            return BadRequest(new { message = "بيانات التقييم مطلوبة" });
        }

        var actor = TokenService.FromPrincipal(User);
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        var (status, payload) = await _aggregator.PostAsync(
            actor,
            cityValue,
            $"sales-manager/director-delegate-followup/lists/{listId}/board-operator-rating",
            json,
            ct);
        return StatusCode(status, payload);
    }

    // —— متابعة المبيعات (موظفو المبيعات) ——
    [HttpGet("sales-followup/employees")]
    public async Task<IActionResult> SalesFollowupEmployees(
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, "sales-manager/director-sales-followup/employees", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("sales-followup/employees/{employeeId:int}")]
    public async Task<IActionResult> SalesFollowupEmployeeDetail(
        int employeeId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        if (employeeId <= 0)
            return BadRequest(new { message = "معرّف الموظف مطلوب." });
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-sales-followup/employees/{employeeId}", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("sales-followup/employees/{employeeId:int}/customers")]
    public async Task<IActionResult> SalesFollowupEmployeeCustomers(
        int employeeId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        if (employeeId <= 0)
            return BadRequest(new { message = "معرّف الموظف مطلوب." });
        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"sales-manager/director-sales-followup/employees/{employeeId}/customers", ct);
        return StatusCode(status, payload);
    }

    [HttpPost("sales-followup/employees/{employeeId:int}/manager-rating")]
    public async Task<IActionResult> SalesFollowupUpsertManagerRating(
        int employeeId,
        [FromQuery] string? cityValue,
        [FromBody] object? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        if (employeeId <= 0)
            return BadRequest(new { message = "معرّف الموظف مطلوب." });
        var actor = TokenService.FromPrincipal(User);
        var json = body is null ? "{}" : System.Text.Json.JsonSerializer.Serialize(body);
        var (status, payload) = await _aggregator.PostAsync(
            actor,
            cityValue,
            $"sales-manager/director-sales-followup/employees/{employeeId}/manager-rating",
            json,
            ct);
        return StatusCode(status, payload);
    }

    /// <summary>
    /// إيقاف/تفعيل تطبيق موظف المبيعات (Gateway) — يمنع Login وStartShift.
    /// </summary>
    [HttpGet("sales-followup/employees/{employeeId:int}/app-block")]
    public async Task<IActionResult> SalesFollowupGetAppBlock(
        int employeeId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        if (employeeId <= 0)
            return BadRequest(new { message = "معرّف الموظف مطلوب." });
        var state = await _appBlocks.GetAsync(cityValue, employeeId, ct);
        return Ok(new
        {
            cityValue,
            employeeId,
            isBlocked = state?.IsBlocked == true,
            updatedAtUtc = state?.UpdatedAtUtc,
            updatedBy = state?.UpdatedBy,
        });
    }

    [HttpPut("sales-followup/employees/{employeeId:int}/app-block")]
    public async Task<IActionResult> SalesFollowupSetAppBlock(
        int employeeId,
        [FromQuery] string? cityValue,
        [FromBody] SalesEmployeeAppBlockBody? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        if (employeeId <= 0)
            return BadRequest(new { message = "معرّف الموظف مطلوب." });
        var actor = TokenService.FromPrincipal(User);
        var blocked = body?.IsBlocked == true;
        await _appBlocks.SetBlockedAsync(
            cityValue,
            employeeId,
            blocked,
            actor.UserName,
            ct);
        return Ok(new
        {
            cityValue,
            employeeId,
            isBlocked = blocked,
            message = blocked
                ? "تم إيقاف تطبيق الموظف — لن يستطيع الدخول أو بدء الدوام."
                : "تم تفعيل تطبيق الموظف.",
        });
    }

    [HttpGet("delegate-followup/payments/{customerId:int}")]
    public async Task<IActionResult> DelegateFollowupPayments(
        int customerId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        if (customerId <= 0)
        {
            return BadRequest(new { message = "معرّف الزبون مطلوب" });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"CustomersPayments/CustomersPayments_GetByCustomerID/{customerId}", ct);
        return StatusCode(status, payload);
    }

    [HttpPost("delegate-followup/payments")]
    public async Task<IActionResult> DelegateFollowupCreatePayment(
        [FromQuery] string? cityValue,
        [FromBody] object? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        if (body is null)
        {
            return BadRequest(new { message = "بيانات التسديد مطلوبة" });
        }

        var actor = TokenService.FromPrincipal(User);
        var json = System.Text.Json.JsonSerializer.Serialize(body);
        var (status, payload) = await _aggregator.PostAsync(
            actor, cityValue, "CustomersPayments/CustomersPayments_Create", json, ct);
        return StatusCode(status, payload);
    }

    // ── إدارة حسابات المتابعين (مدير + مراقب) ───────────────────────

    [HttpGet("followers/manage/lists")]
    public async Task<IActionResult> FollowerManageLists(
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, "internal/follower-users/lists", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/manage")]
    public async Task<IActionResult> FollowerManageList(
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, "internal/follower-users", ct);
        return StatusCode(status, payload);
    }

    [HttpGet("followers/manage/{userId:int}")]
    public async Task<IActionResult> FollowerManageGet(
        int userId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.GetOneAsync(
            actor, cityValue, $"internal/follower-users/{userId}", ct);
        return StatusCode(status, payload);
    }

    [HttpPost("followers/manage")]
    public async Task<IActionResult> FollowerManageCreate(
        [FromQuery] string? cityValue,
        [FromBody] FollowerManageBody? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var json = System.Text.Json.JsonSerializer.Serialize(body ?? new FollowerManageBody());
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var (status, payload) = await _aggregator.SendContentAsync(
            actor, cityValue, "internal/follower-users", HttpMethod.Post, content, ct);
        return StatusCode(status, payload);
    }

    [HttpPut("followers/manage/{userId:int}")]
    public async Task<IActionResult> FollowerManageUpdate(
        int userId,
        [FromQuery] string? cityValue,
        [FromBody] FollowerManageBody? body,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var json = System.Text.Json.JsonSerializer.Serialize(body ?? new FollowerManageBody());
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var (status, payload) = await _aggregator.SendContentAsync(
            actor, cityValue, $"internal/follower-users/{userId}", HttpMethod.Put, content, ct);
        return StatusCode(status, payload);
    }

    [HttpDelete("followers/manage/{userId:int}")]
    public async Task<IActionResult> FollowerManageDelete(
        int userId,
        [FromQuery] string? cityValue,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            return BadRequest(new { message = "يجب تحديد المحافظة." });
        }

        var actor = TokenService.FromPrincipal(User);
        var (status, payload) = await _aggregator.SendContentAsync(
            actor, cityValue, $"internal/follower-users/{userId}", HttpMethod.Delete, null, ct);
        return StatusCode(status, payload);
    }

    public sealed class LegalConversionReviewBody
    {
        public string? Note { get; set; }
    }

    public sealed class CollectionEnabledBody
    {
        public bool Enabled { get; set; }
    }

    public sealed class LegalFollowupCancelBody
    {
        public string? Reason { get; set; }
    }

    public sealed class FollowerManageBody
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? DisplayName { get; set; }
        public string? PhoneNumber { get; set; }
        public List<int>? ListIds { get; set; }
    }
}
