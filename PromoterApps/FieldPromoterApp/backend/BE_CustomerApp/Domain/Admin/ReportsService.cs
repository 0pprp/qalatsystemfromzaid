using System.Globalization;
using System.Text;
using Dapper;
using BE_CustomerApp.Domain.Orders;
using BE_CustomerApp.Infrastructure;

namespace BE_CustomerApp.Domain.Admin;

public sealed record StatusCountDto(string Status, string Label, int Count);
public sealed record TopItemDto(string Name, int Quantity);

/// <summary>One agent's numbers in the selected range.</summary>
public sealed record AgentPerformanceDto(
    long UserId, string FullName, string? Username, int Orders, int Waiting, int WithAgent, int Sold, int Finished, int Denied, int NotSold, int Pins,
    int WorkedDays, int WorkedMinutes);

/// <summary>One name behind a number of the «أداء المسوّقين» table: an order's customer or a pinned shop.</summary>
public sealed record AgentDetailRowDto(
    long Id, string? OrderNumber, string Name, string? Phone, string? Status, string? StatusLabel, string? BranchStatusLabel,
    string? CityName, string? Reason, DateTime CreatedAt);

/// <summary>Dashboard → «نظرة عامة». Range is by Iraq calendar day, both ends included.</summary>
public sealed record OverviewDto(
    int Agents, int WorkingNow, int Orders, int Sold, int Finished, int NotSold, int Open, int Pins,
    IReadOnlyList<StatusCountDto> ByStatus, IReadOnlyList<AgentPerformanceDto> ByAgent, IReadOnlyList<TopItemDto> TopItems);

public sealed class ReportsService
{
    private readonly Db _db;

    public ReportsService(Db db) => _db = db;

    // Company-system statuses that mean a sales employee already holds the request (anything but unassigned «New»).
    private const string InRange = "o.CreatedAt >= @fromUtc AND o.CreatedAt < @toUtc";
    private const string Unassigned = "(o.BranchStatus IS NULL OR o.BranchStatus = 'New')";

    /// <summary>
    /// The order filters behind each per-agent number (alias o). The table counts and the names popup share them,
    /// so a number always equals the length of its list.
    /// </summary>
    private static readonly Dictionary<string, string> OrderBuckets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["orders"] = InRange,
        // Not with a sales employee yet: waiting for the dashboard review, or sent to the branch and still unassigned.
        ["waiting"] = $"{InRange} AND (o.Status IN ('New','UnderReview') OR (o.Status = 'Transferred' AND {Unassigned}))",
        // Approved and assigned: a sales employee is working on it.
        ["withAgent"] = $"{InRange} AND (o.Status = 'FieldInspection' OR (o.Status = 'Transferred' AND NOT {Unassigned}))",
        ["sold"] = "o.Status IN ('Sold','Finished') AND o.SoldAt >= @fromUtc AND o.SoldAt < @toUtc",
        ["finished"] = $"{InRange} AND o.Status = 'Finished'",
        // Refused by the dashboard user before it reached the branch.
        ["denied"] = $"{InRange} AND o.Status = 'Denied'",
        // Refused (or cancelled) in the company system by the sales side.
        ["notSold"] = $"{InRange} AND o.Status = 'NotSold'",
    };

    private static string CountOf(string bucket) =>
        $"(SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND {OrderBuckets[bucket]})";

    private static string? BranchStatusLabel(string? s) => s switch
    {
        null or "" => null,
        "New" => "غير مسند لمندوب",
        "Assigned" => "مسند لمندوب المبيعات",
        "Viewed" => "شاهده مندوب المبيعات",
        "Returned" => "معاد لمندوب المبيعات",
        "Pending" => "معلّق عند المندوب",
        "PreparedForSale" => "جاهز للبيع",
        "InProgress" => "قيد التنفيذ",
        "Inspected" => "تم الكشف",
        "ConvertedToSale" => "قيد التحويل إلى مبيعة",
        "LegalFollowup" => "متابعة قانونية",
        "Completed" => "تم البيع",
        "Rejected" => "مرفوض من المبيعات",
        _ => s,
    };

    /// <summary>The names counted in one number of an agent's row. Buckets: the order ones above, or «pins».</summary>
    public async Task<IReadOnlyList<AgentDetailRowDto>> AgentDetailsAsync(long userId, string? bucket, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Range(from, to);
        await using var c = await _db.OpenAsync(ct);
        if (string.Equals(bucket, "pins", StringComparison.OrdinalIgnoreCase))
        {
            var pins = await c.QueryAsync<(long Id, string? ShopName, string? Phone, string? Description, DateTime CreatedAt)>("""
                SELECT p.Id, p.ShopName, p.Phone, p.Description, p.CreatedAt FROM agents.Prospects p
                WHERE p.CreatedByUserId = @userId AND p.IsDeleted = 0 AND p.CreatedAt >= @fromUtc AND p.CreatedAt < @toUtc
                ORDER BY p.CreatedAt DESC
                """, new { userId, fromUtc, toUtc });
            return pins.Select(p => new AgentDetailRowDto(p.Id, null, string.IsNullOrWhiteSpace(p.ShopName) ? "—" : p.ShopName!, p.Phone, null,
                string.IsNullOrWhiteSpace(p.Description) ? null : p.Description, null, null, null, p.CreatedAt)).ToList();
        }

        if (bucket is null || !OrderBuckets.TryGetValue(bucket, out var filter))
            throw ApiException.BadRequest("validation", "نوع القائمة غير معروف");
        var rows = await c.QueryAsync<(long Id, string? OrderNumber, string Name, string? Phone, string Status, string? BranchStatus, string? CityName,
            string? PublicReason, string? BranchRejectReason, DateTime CreatedAt)>($"""
            SELECT o.Id, o.OrderNumber, CONCAT(o.FirstName,' ',o.FatherName,' ',o.GrandfatherName,' ',o.Surname) AS Name, o.CustomerPhone AS Phone,
                   o.Status, o.BranchStatus, o.CityName, o.PublicReason, o.BranchRejectReason, o.CreatedAt
            FROM agents.Orders o WHERE o.SubmittedByUserId = @userId AND {filter}
            ORDER BY o.CreatedAt DESC
            """, new { userId, fromUtc, toUtc });
        return rows.Select(r =>
        {
            var refused = r.Status is OrderStatus.NotSold or OrderStatus.Denied;
            return new AgentDetailRowDto(r.Id, r.OrderNumber, r.Name.Trim(), r.Phone, r.Status, OrderStatus.Label(r.Status),
                r.Status is OrderStatus.Transferred or OrderStatus.FieldInspection ? BranchStatusLabel(r.BranchStatus) : null,
                r.CityName, refused ? (string.IsNullOrWhiteSpace(r.BranchRejectReason) ? r.PublicReason : r.BranchRejectReason) : null, r.CreatedAt);
        }).ToList();
    }

    public async Task<OverviewDto> OverviewAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Range(from, to);
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync($"""
            SELECT COUNT(*) FROM agents.Users WHERE IsDeleted = 0 AND Username IS NOT NULL AND IsSuspended = 0;
            SELECT COUNT(DISTINCT UserId) FROM agents.WorkSessions WHERE EndedAt IS NULL;
            SELECT Status, COUNT(*) FROM agents.Orders WHERE CreatedAt >= @fromUtc AND CreatedAt < @toUtc GROUP BY Status;
            SELECT COUNT(*) FROM agents.Orders WHERE Status IN ('Sold','Finished') AND SoldAt >= @fromUtc AND SoldAt < @toUtc;
            SELECT COUNT(*) FROM agents.Prospects WHERE IsDeleted = 0 AND CreatedAt >= @fromUtc AND CreatedAt < @toUtc;
            SELECT u.Id AS UserId, CONCAT(u.FirstName, ' ', u.FatherName, ' ', u.GrandfatherName) AS FullName, u.Username,
                   {CountOf("orders")} AS Orders,
                   {CountOf("waiting")} AS Waiting,
                   {CountOf("withAgent")} AS WithAgent,
                   {CountOf("sold")} AS Sold,
                   {CountOf("finished")} AS Finished,
                   {CountOf("denied")} AS Denied,
                   {CountOf("notSold")} AS NotSold,
                   (SELECT COUNT(*) FROM agents.Prospects p WHERE p.CreatedByUserId = u.Id AND p.IsDeleted = 0 AND p.CreatedAt >= @fromUtc AND p.CreatedAt < @toUtc) AS Pins,
                   (SELECT COUNT(DISTINCT w.WorkDate) FROM agents.WorkSessions w WHERE w.UserId = u.Id AND w.StartedAt >= @fromUtc AND w.StartedAt < @toUtc) AS WorkedDays,
                   (SELECT ISNULL(SUM(DATEDIFF(MINUTE, w.StartedAt, ISNULL(w.EndedAt, SYSUTCDATETIME()))), 0) FROM agents.WorkSessions w
                    WHERE w.UserId = u.Id AND w.StartedAt >= @fromUtc AND w.StartedAt < @toUtc) AS WorkedMinutes
            FROM agents.Users u WHERE u.IsDeleted = 0 AND u.Username IS NOT NULL;
            SELECT TOP 10 i.Name, SUM(i.Quantity) AS Quantity
            FROM agents.OrderItems i JOIN agents.Orders o ON o.Id = i.OrderId
            WHERE o.Status IN ('Sold','Finished') AND o.SoldAt >= @fromUtc AND o.SoldAt < @toUtc
            GROUP BY i.Name ORDER BY Quantity DESC, i.Name;
            """, new { fromUtc, toUtc });
        var agents = await multi.ReadSingleAsync<int>();
        var working = await multi.ReadSingleAsync<int>();
        var byStatus = (await multi.ReadAsync<(string Status, int Count)>()).ToDictionary(x => x.Status, x => x.Count);
        var sold = await multi.ReadSingleAsync<int>();
        var pins = await multi.ReadSingleAsync<int>();
        var byAgent = (await multi.ReadAsync<AgentPerformanceDto>())
            .OrderByDescending(a => a.Sold).ThenByDescending(a => a.Orders).ThenByDescending(a => a.WorkedMinutes).ThenBy(a => a.FullName)
            .ToList();
        var topItems = (await multi.ReadAsync<TopItemDto>()).ToList();

        return new OverviewDto(
            agents, working, byStatus.Values.Sum(), sold, byStatus.GetValueOrDefault(OrderStatus.Finished),
            byStatus.GetValueOrDefault(OrderStatus.NotSold) + byStatus.GetValueOrDefault(OrderStatus.Denied),
            OrderStatus.Open.Sum(s => byStatus.GetValueOrDefault(s)), pins,
            OrderStatus.All.Where(byStatus.ContainsKey).Select(s => new StatusCountDto(s, OrderStatus.Label(s), byStatus[s])).ToList(),
            byAgent, topItems);
    }

    /// <summary>CSV with UTF-8 BOM so Excel opens Arabic correctly. Types: orders, agents, prospects.</summary>
    public async Task<(string FileName, byte[] Content)> ExportAsync(string type, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Range(from, to);
        var sql = type switch
        {
            "orders" => """
                SELECT o.OrderNumber AS [رقم الطلب], o.Status AS [الحالة], o.PurchaseType AS [نوع الشراء], o.CityName AS [الفرع],
                       (SELECT STRING_AGG(i.Name + CASE WHEN i.Quantity > 1 THEN N' ×' + CAST(i.Quantity AS NVARCHAR(5)) ELSE N'' END, N'، ')
                        FROM agents.OrderItems i WHERE i.OrderId = o.Id) AS [المواد],
                       CONCAT(o.FirstName,' ',o.FatherName,' ',o.GrandfatherName,' ',o.Surname) AS [الزبون], o.CustomerPhone AS [الهاتف],
                       o.Area AS [المنطقة], CONCAT(u.FirstName,' ',u.FatherName,' ',u.GrandfatherName) AS [المسوّق],
                       o.SalesRequestId AS [رقم طلب البيع], o.CreatedAt AS [تاريخ الطلب], o.SoldAt AS [تاريخ البيع]
                FROM agents.Orders o JOIN agents.Users u ON u.Id = o.SubmittedByUserId
                WHERE o.CreatedAt >= @fromUtc AND o.CreatedAt < @toUtc ORDER BY o.Id
                """,
            "agents" => $"""
                SELECT CONCAT(u.FirstName,' ',u.FatherName,' ',u.GrandfatherName,' ',u.Surname) AS [المسوّق], u.Username AS [اسم المستخدم], u.Phone AS [الهاتف],
                       {CountOf("orders")} AS [الطلبات],
                       {CountOf("waiting")} AS [بانتظار الإسناد],
                       {CountOf("withAgent")} AS [عند مندوب المبيعات],
                       {CountOf("denied")} AS [مرفوضة من لوحة التحكم],
                       {CountOf("notSold")} AS [مرفوضة من المبيعات],
                       {CountOf("sold")} AS [المبيعات],
                       (SELECT COUNT(*) FROM agents.Prospects p WHERE p.CreatedByUserId = u.Id AND p.IsDeleted = 0 AND p.CreatedAt >= @fromUtc AND p.CreatedAt < @toUtc) AS [المحلات المثبتة],
                       (SELECT COUNT(DISTINCT w.WorkDate) FROM agents.WorkSessions w WHERE w.UserId = u.Id AND w.StartedAt >= @fromUtc AND w.StartedAt < @toUtc) AS [أيام الدوام],
                       (SELECT ISNULL(SUM(DATEDIFF(MINUTE, w.StartedAt, ISNULL(w.EndedAt, SYSUTCDATETIME()))), 0) / 60 FROM agents.WorkSessions w
                        WHERE w.UserId = u.Id AND w.StartedAt >= @fromUtc AND w.StartedAt < @toUtc) AS [ساعات الدوام],
                       u.IsSuspended AS [موقوف]
                FROM agents.Users u WHERE u.IsDeleted = 0 AND u.Username IS NOT NULL ORDER BY u.FirstName
                """,
            "prospects" => """
                SELECT p.ShopName AS [اسم المحل], p.Description AS [النوع], p.Interest AS [الاهتمام (1-5)], p.Phone AS [الهاتف],
                       p.GoodsValue AS [قيمة البضاعة التقديرية],
                       p.Latitude AS [خط العرض], p.Longitude AS [خط الطول],
                       p.AgentLatitude AS [موقع المسوّق - خط العرض], p.AgentLongitude AS [موقع المسوّق - خط الطول],
                       CONCAT(u.FirstName,' ',u.FatherName,' ',u.GrandfatherName) AS [أضافه], p.CreatedAt AS [التاريخ]
                FROM agents.Prospects p JOIN agents.Users u ON u.Id = p.CreatedByUserId
                WHERE p.IsDeleted = 0 AND p.CreatedAt >= @fromUtc AND p.CreatedAt < @toUtc ORDER BY p.Id
                """,
            _ => throw ApiException.BadRequest("validation", "نوع التقرير غير معروف"),
        };

        await using var c = await _db.OpenAsync(ct);
        await using var reader = await c.ExecuteReaderAsync(sql, new { fromUtc, toUtc }, commandTimeout: 300);
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Enumerable.Range(0, reader.FieldCount).Select(i => Csv(reader.GetName(i)))));
        while (await reader.ReadAsync(ct))
        {
            sb.AppendLine(string.Join(',', Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "" : Csv(Format(reader.GetValue(i))))));
        }
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return ($"{type}-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv", bytes);
    }

    /// <summary>Default: the current month so far (Iraq time).</summary>
    private static (DateTime FromUtc, DateTime ToUtc) Range(DateOnly? from, DateOnly? to)
    {
        var today = Work.IraqTime.DateOf(DateTime.UtcNow);
        var f = from ?? new DateOnly(today.Year, today.Month, 1);
        var t = to ?? today;
        if (t < f) (f, t) = (t, f);
        return (Work.IraqTime.DayStartUtc(f), Work.IraqTime.DayStartUtc(t.AddDays(1)));
    }

    private static string Format(object v) => v switch
    {
        DateTime d => (d + Work.IraqTime.Offset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        bool b => b ? "نعم" : "لا",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? "",
    };

    private static string Csv(string s)
    {
        // Neutralise spreadsheet formula injection from user-entered text.
        if (s.Length > 0 && "=+-@".Contains(s[0]) && !long.TryParse(s, out _)) s = "'" + s;
        return s.Contains(',') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }
}
