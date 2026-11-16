using Dapper;
using BE_CustomerApp.Auth;
using BE_CustomerApp.Domain.Admin;
using BE_CustomerApp.Domain.Users;
using BE_CustomerApp.Infrastructure;
using BE_CustomerApp.Integrations.Gateway;

namespace BE_CustomerApp.Domain.Work;

/// <summary>How interested the shop owner seemed, 1 (not at all) … 5 (very).</summary>
public static class ProspectInterest
{
    public static readonly string[] Labels = ["", "غير مهتم إطلاقاً", "قليل الاهتمام", "مهتم نوعاً ما", "مهتم", "مهتم جداً"];

    public static string Label(int level) => level is >= 1 and <= 5 ? Labels[level] : "";
}

public sealed record ProspectInput(
    decimal? Latitude, decimal? Longitude, string? ShopName, string? Description, int? Interest, string? Phone,
    long? GoodsValue, decimal? LengthM, decimal? WidthM, int? MinutesSpent = null, decimal? AgentLatitude = null, decimal? AgentLongitude = null);

public sealed record ProspectDto(
    long Id, decimal Latitude, decimal Longitude, string ShopName, string? Description, int Interest, string InterestLabel,
    string? Phone, long? GoodsValue, decimal? LengthM, decimal? WidthM, decimal? AreaM2,
    long CreatedByUserId, string CreatedByName, string? CityName, DateTime CreatedAt, DateTime UpdatedAt, bool IsMine,
    int? MinutesSpent = null, decimal? AgentLatitude = null, decimal? AgentLongitude = null);

/// <summary>
/// «المحلات المحتملة»: a pin an agent drops for a shop that may buy later. Every agent sees every pin near them
/// (so a colleague visiting the same area knows the shop was already approached); the dashboard sees them all.
/// </summary>
public sealed class ProspectService
{
    private const int MaxInArea = 500;

    private readonly Db _db;
    private readonly BranchCatalog _branches;
    private readonly AuditService _audit;

    public ProspectService(Db db, BranchCatalog branches, AuditService audit)
    {
        _db = db;
        _branches = branches;
        _audit = audit;
    }

    private const string Select = """
        SELECT p.Id, p.Latitude, p.Longitude, p.ShopName, p.Description, CAST(p.Interest AS INT) AS Interest, p.Phone, p.GoodsValue,
               p.LengthM, p.WidthM, p.CreatedByUserId, CONCAT(u.FirstName, ' ', u.FatherName, ' ', u.GrandfatherName) AS CreatedByName,
               p.CityValue, p.CreatedAt, p.UpdatedAt, CAST(p.MinutesSpent AS INT) AS MinutesSpent,
               p.AgentLatitude, p.AgentLongitude
        FROM agents.Prospects p JOIN agents.Users u ON u.Id = p.CreatedByUserId
        """;

    private sealed record Row(
        long Id, decimal Latitude, decimal Longitude, string ShopName, string? Description, int Interest, string? Phone, long? GoodsValue,
        decimal? LengthM, decimal? WidthM, long CreatedByUserId, string CreatedByName, int? CityValue, DateTime CreatedAt, DateTime UpdatedAt,
        int? MinutesSpent, decimal? AgentLatitude, decimal? AgentLongitude);

    // ---------------- Agent ----------------

    /// <summary>Pins inside a map area (all agents'), newest first. The area is capped to ~0.5° each way.</summary>
    public async Task<IReadOnlyList<ProspectDto>> InAreaAsync(long viewerId, decimal minLat, decimal minLng, decimal maxLat, decimal maxLng, CancellationToken ct)
    {
        if (minLat > maxLat) (minLat, maxLat) = (maxLat, minLat);
        if (minLng > maxLng) (minLng, maxLng) = (maxLng, minLng);
        var cLat = (minLat + maxLat) / 2;
        var cLng = (minLng + maxLng) / 2;
        minLat = Math.Max(minLat, cLat - 0.5m);
        maxLat = Math.Min(maxLat, cLat + 0.5m);
        minLng = Math.Max(minLng, cLng - 0.5m);
        maxLng = Math.Min(maxLng, cLng + 0.5m);
        await using var c = await _db.OpenAsync(ct);
        var rows = await c.QueryAsync<Row>($"""
            {Select.Replace("SELECT p.Id", $"SELECT TOP {MaxInArea} p.Id")}
            WHERE p.IsDeleted = 0 AND p.Latitude BETWEEN @minLat AND @maxLat AND p.Longitude BETWEEN @minLng AND @maxLng
            ORDER BY p.CreatedAt DESC
            """, new { minLat, maxLat, minLng, maxLng });
        return await MapAsync(rows, viewerId, ct);
    }

    /// <summary>
    /// «المحلات»: the agent's own pins, newest first; optionally one interest level, a search, and a range of Iraq days
    /// (<paramref name="from"/> … <paramref name="to"/>, both included).
    /// </summary>
    public async Task<PagedResult<ProspectDto>> MineAsync(long userId, int? interest, string? q, DateOnly? from, DateOnly? to, Paging paging,
        CancellationToken ct)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var where = """
            WHERE p.IsDeleted = 0 AND p.CreatedByUserId = @userId AND (@interest IS NULL OR p.Interest = @interest)
              AND (@q IS NULL OR p.ShopName LIKE '%' + @q + '%' OR p.Description LIKE '%' + @q + '%' OR p.Phone LIKE '%' + @q + '%')
              AND (@fromUtc IS NULL OR p.CreatedAt >= @fromUtc) AND (@toUtc IS NULL OR p.CreatedAt < @toUtc)
            """;
        DateTime? fromUtc = from is { } f ? IraqTime.DayStartUtc(f) : null;
        DateTime? toUtc = to is { } t ? IraqTime.DayStartUtc(t.AddDays(1)) : null;
        var args = new { userId, interest, q, fromUtc, toUtc, paging.Offset, paging.PageSize };
        await using var c = await _db.OpenAsync(ct);
        var total = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM agents.Prospects p {where}", args);
        var rows = await c.QueryAsync<Row>($"{Select} {where} ORDER BY p.CreatedAt DESC, p.Id DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", args);
        return new PagedResult<ProspectDto>(await MapAsync(rows, userId, ct), total, paging.Page, paging.PageSize);
    }

    /// <summary>A new pin. Besides the (movable) pin, it keeps where the agent's phone was, when the app sends it.</summary>
    public async Task<ProspectDto> CreateAsync(UserRow user, ProspectInput input, CancellationToken ct)
    {
        var v = Validate(input);
        await using var c = await _db.OpenAsync(ct);
        var id = await c.ExecuteScalarAsync<long>("""
            INSERT agents.Prospects(CreatedByUserId, CityValue, Latitude, Longitude, ShopName, Description, Interest, Phone, GoodsValue, LengthM, WidthM, MinutesSpent,
                AgentLatitude, AgentLongitude)
            OUTPUT inserted.Id
            VALUES (@UserId, @CityValue, @Latitude, @Longitude, @ShopName, @Description, @Interest, @Phone, @GoodsValue, @LengthM, @WidthM, @MinutesSpent,
                @AgentLatitude, @AgentLongitude)
            """, new
        {
            UserId = user.Id, user.CityValue, v.Latitude, v.Longitude, v.ShopName, v.Description, v.Interest, v.Phone, v.GoodsValue, v.LengthM, v.WidthM,
            v.MinutesSpent, v.AgentLatitude, v.AgentLongitude,
        });
        return await GetAsync(id, user.Id, ct);
    }

    /// <summary>Agents edit only their own pins.</summary>
    public async Task<ProspectDto> UpdateAsync(long userId, long id, ProspectInput input, CancellationToken ct)
    {
        var v = Validate(input);
        await using var c = await _db.OpenAsync(ct);
        var owner = await c.ExecuteScalarAsync<long?>("SELECT CreatedByUserId FROM agents.Prospects WHERE Id = @id AND IsDeleted = 0", new { id })
                    ?? throw ApiException.NotFound("الموقع غير موجود");
        if (owner != userId) throw ApiException.Forbidden("not_owner", "يمكنك تعديل المواقع التي أضفتها فقط");
        await c.ExecuteAsync("""
            UPDATE agents.Prospects SET Latitude = @Latitude, Longitude = @Longitude, ShopName = @ShopName, Description = @Description,
                Interest = @Interest, Phone = @Phone, GoodsValue = @GoodsValue, LengthM = @LengthM, WidthM = @WidthM,
                MinutesSpent = COALESCE(@MinutesSpent, MinutesSpent), UpdatedAt = SYSUTCDATETIME()
            WHERE Id = @id
            """, new { id, v.Latitude, v.Longitude, v.ShopName, v.Description, v.Interest, v.Phone, v.GoodsValue, v.LengthM, v.WidthM, v.MinutesSpent });
        return await GetAsync(id, userId, ct);
    }

    public async Task DeleteOwnAsync(long userId, long id, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var owner = await c.ExecuteScalarAsync<long?>("SELECT CreatedByUserId FROM agents.Prospects WHERE Id = @id AND IsDeleted = 0", new { id })
                    ?? throw ApiException.NotFound("الموقع غير موجود");
        if (owner != userId) throw ApiException.Forbidden("not_owner", "يمكنك حذف المواقع التي أضفتها فقط");
        await c.ExecuteAsync("UPDATE agents.Prospects SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @id", new { id });
    }

    public async Task<ProspectDto> GetAsync(long id, long viewerId, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var row = await c.QuerySingleOrDefaultAsync<Row>($"{Select} WHERE p.Id = @id AND p.IsDeleted = 0", new { id })
                  ?? throw ApiException.NotFound("الموقع غير موجود");
        return (await MapAsync([row], viewerId, ct))[0];
    }

    // ---------------- Dashboard ----------------

    /// <summary>One agent's pins in a time range (oldest first): the «المحلات المثبّتة» timeline.</summary>
    public async Task<IReadOnlyList<ProspectDto>> ForAgentDayAsync(long userId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var rows = await c.QueryAsync<Row>($"""
            {Select}
            WHERE p.IsDeleted = 0 AND p.CreatedByUserId = @userId AND p.CreatedAt >= @fromUtc AND p.CreatedAt < @toUtc
            ORDER BY p.CreatedAt, p.Id
            """, new { userId, fromUtc, toUtc });
        return await MapAsync(rows, 0, ct);
    }

    public async Task<PagedResult<ProspectDto>> AdminListAsync(int? cityValue, long? userId, int? minInterest, string? q,
        DateTime? from, DateTime? to, Paging paging, CancellationToken ct)
    {
        q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var where = """
            WHERE p.IsDeleted = 0 AND (@cityValue IS NULL OR p.CityValue = @cityValue) AND (@userId IS NULL OR p.CreatedByUserId = @userId)
              AND (@minInterest IS NULL OR p.Interest >= @minInterest) AND (@from IS NULL OR p.CreatedAt >= @from) AND (@to IS NULL OR p.CreatedAt < @to)
              AND (@q IS NULL OR p.ShopName LIKE '%' + @q + '%' OR p.Description LIKE '%' + @q + '%' OR p.Phone LIKE '%' + @q + '%')
            """;
        var args = new { cityValue, userId, minInterest, q, from, to, paging.Offset, paging.PageSize };
        await using var c = await _db.OpenAsync(ct);
        var total = await c.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM agents.Prospects p {where}", args);
        var rows = await c.QueryAsync<Row>($"{Select} {where} ORDER BY p.CreatedAt DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY", args);
        return new PagedResult<ProspectDto>(await MapAsync(rows, 0, ct), total, paging.Page, paging.PageSize);
    }

    public async Task AdminDeleteAsync(long id, AdminActor actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var updated = await c.ExecuteAsync("UPDATE agents.Prospects SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE Id = @id AND IsDeleted = 0", new { id });
        if (updated == 0) throw ApiException.NotFound("الموقع غير موجود");
        await _audit.WriteAsync(actor, "prospect.delete", "Prospect", id, null, null, c);
    }

    // ---------------- Helpers ----------------

    private async Task<List<ProspectDto>> MapAsync(IEnumerable<Row> rows, long viewerId, CancellationToken ct)
    {
        var list = new List<ProspectDto>();
        foreach (var r in rows)
        {
            list.Add(new ProspectDto(r.Id, r.Latitude, r.Longitude, r.ShopName, r.Description, r.Interest, ProspectInterest.Label(r.Interest),
                r.Phone, r.GoodsValue, r.LengthM, r.WidthM, r.LengthM is { } l && r.WidthM is { } w ? Math.Round(l * w, 2) : null,
                r.CreatedByUserId, r.CreatedByName, r.CityValue is { } cv ? await _branches.NameAsync(cv, ct) : null,
                r.CreatedAt, r.UpdatedAt, viewerId != 0 && r.CreatedByUserId == viewerId, r.MinutesSpent, r.AgentLatitude, r.AgentLongitude));
        }
        return list;
    }

    private sealed record Valid(decimal Latitude, decimal Longitude, string ShopName, string? Description, byte Interest, string? Phone,
        long? GoodsValue, decimal? LengthM, decimal? WidthM, short? MinutesSpent, decimal? AgentLatitude, decimal? AgentLongitude);

    private static Valid Validate(ProspectInput input)
    {
        var errors = new Dictionary<string, string>();
        if (input.Latitude is null || input.Longitude is null) errors["latitude"] = "حدد الموقع على الخريطة";
        else if (input.Latitude is < 29 or > 38 || input.Longitude is < 38 or > 49) errors["latitude"] = "الموقع خارج العراق";

        var name = input.ShopName?.Trim() ?? "";
        if (name.Length is < 2 or > 150) errors["shopName"] = "أدخل اسم المحل";
        var description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        if (description?.Length > 500) errors["description"] = "الوصف طويل جداً (500 حرف كحد أقصى)";
        if (input.Interest is not (>= 1 and <= 5)) errors["interest"] = "اختر مدى اهتمام صاحب المحل";

        string? phone = null;
        if (!string.IsNullOrWhiteSpace(input.Phone))
        {
            phone = IraqiPhone.Normalize(input.Phone);
            if (phone is null) errors["phone"] = "رقم هاتف عراقي غير صحيح (07XXXXXXXXX)";
        }
        if (input.GoodsValue is < 0 or > 1_000_000_000_000) errors["goodsValue"] = "قيمة غير صحيحة";
        if (input.LengthM is <= 0 or > 1000) errors["lengthM"] = "طول غير صحيح";
        if (input.WidthM is <= 0 or > 1000) errors["widthM"] = "عرض غير صحيح";
        // «الوقت المستغرق عند المحل» was removed in app 1.4.9 (time is measured from the pins); 1.4.7–1.4.8 still send it.
        if (input.MinutesSpent is not null and not (>= 1 and <= 600)) errors["minutesSpent"] = "الوقت بين 1 و600 دقيقة";
        if (errors.Count > 0) throw ApiException.Validation(errors);

        // The phone's own position never blocks a pin: kept only when it is a usable point inside Iraq.
        var agentOk = input.AgentLatitude is >= 29 and <= 38 && input.AgentLongitude is >= 38 and <= 49;
        return new Valid(Math.Round(input.Latitude!.Value, 6), Math.Round(input.Longitude!.Value, 6), name, description, (byte)input.Interest!.Value,
            phone, input.GoodsValue is 0 ? null : input.GoodsValue,
            input.LengthM is null ? null : Math.Round(input.LengthM.Value, 2), input.WidthM is null ? null : Math.Round(input.WidthM.Value, 2),
            (short?)input.MinutesSpent, agentOk ? Math.Round(input.AgentLatitude!.Value, 6) : null,
            agentOk ? Math.Round(input.AgentLongitude!.Value, 6) : null);
    }
}
