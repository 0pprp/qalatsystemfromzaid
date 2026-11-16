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
    long UserId, string FullName, string? Username, int Orders, int Sold, int Finished, int NotSold, int Pins, int WorkedDays, int WorkedMinutes);

/// <summary>Dashboard → «نظرة عامة». Range is by Iraq calendar day, both ends included.</summary>
public sealed record OverviewDto(
    int Agents, int WorkingNow, int Orders, int Sold, int Finished, int NotSold, int Open, int Pins,
    IReadOnlyList<StatusCountDto> ByStatus, IReadOnlyList<AgentPerformanceDto> ByAgent, IReadOnlyList<TopItemDto> TopItems);

public sealed class ReportsService
{
    private readonly Db _db;

    public ReportsService(Db db) => _db = db;

    public async Task<OverviewDto> OverviewAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (fromUtc, toUtc) = Range(from, to);
        await using var c = await _db.OpenAsync(ct);
        using var multi = await c.QueryMultipleAsync("""
            SELECT COUNT(*) FROM agents.Users WHERE IsDeleted = 0 AND Username IS NOT NULL AND IsSuspended = 0;
            SELECT COUNT(DISTINCT UserId) FROM agents.WorkSessions WHERE EndedAt IS NULL;
            SELECT Status, COUNT(*) FROM agents.Orders WHERE CreatedAt >= @fromUtc AND CreatedAt < @toUtc GROUP BY Status;
            SELECT COUNT(*) FROM agents.Orders WHERE Status IN ('Sold','Finished') AND SoldAt >= @fromUtc AND SoldAt < @toUtc;
            SELECT COUNT(*) FROM agents.Prospects WHERE IsDeleted = 0 AND CreatedAt >= @fromUtc AND CreatedAt < @toUtc;
            SELECT u.Id AS UserId, CONCAT(u.FirstName, ' ', u.FatherName, ' ', u.GrandfatherName) AS FullName, u.Username,
                   (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND o.CreatedAt >= @fromUtc AND o.CreatedAt < @toUtc) AS Orders,
                   (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND o.Status IN ('Sold','Finished') AND o.SoldAt >= @fromUtc AND o.SoldAt < @toUtc) AS Sold,
                   (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND o.Status = 'Finished' AND o.CreatedAt >= @fromUtc AND o.CreatedAt < @toUtc) AS Finished,
                   (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND o.Status IN ('NotSold','Denied') AND o.CreatedAt >= @fromUtc AND o.CreatedAt < @toUtc) AS NotSold,
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
            "agents" => """
                SELECT CONCAT(u.FirstName,' ',u.FatherName,' ',u.GrandfatherName,' ',u.Surname) AS [المسوّق], u.Username AS [اسم المستخدم], u.Phone AS [الهاتف],
                       (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND o.CreatedAt >= @fromUtc AND o.CreatedAt < @toUtc) AS [الطلبات],
                       (SELECT COUNT(*) FROM agents.Orders o WHERE o.SubmittedByUserId = u.Id AND o.Status IN ('Sold','Finished') AND o.SoldAt >= @fromUtc AND o.SoldAt < @toUtc) AS [المبيعات],
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
