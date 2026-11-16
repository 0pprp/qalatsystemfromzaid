using Dapper;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Domain.Admin;

/// <summary>Settings edited in the dashboard («إعدادات التطبيق»).</summary>
public sealed record AppSettings
{
    public string SupportPhone { get; init; } = "";
    public string SupportWhatsapp { get; init; } = "";
    /// <summary>Smallest amount a promoter can ask to withdraw (IQD).</summary>
    public long MinPayout { get; init; } = 5_000;
}

public sealed class AppSettingsService
{
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
    private readonly Db _db;
    private AppSettings? _cached;
    private DateTime _cachedAt;

    public AppSettingsService(Db db) => _db = db;

    public async Task<AppSettings> GetAsync(CancellationToken ct = default)
    {
        if (_cached is not null && DateTime.UtcNow - _cachedAt < CacheFor) return _cached;
        await using var c = await _db.OpenAsync(ct);
        var rows = (await c.QueryAsync<(string Key, string Value)>("SELECT [Key], [Value] FROM promoters.AppSettings"))
            .ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
        var d = new AppSettings();
        _cached = new AppSettings
        {
            SupportPhone = rows.GetValueOrDefault(nameof(d.SupportPhone), d.SupportPhone),
            SupportWhatsapp = rows.GetValueOrDefault(nameof(d.SupportWhatsapp), d.SupportWhatsapp),
            MinPayout = long.TryParse(rows.GetValueOrDefault(nameof(d.MinPayout)), out var m) ? m : d.MinPayout,
        };
        _cachedAt = DateTime.UtcNow;
        return _cached;
    }

    public async Task SaveAsync(AppSettings s, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string>();
        if ((s.SupportPhone?.Length ?? 0) > 20) errors["supportPhone"] = "الرقم طويل جداً";
        if ((s.SupportWhatsapp?.Length ?? 0) > 20) errors["supportWhatsapp"] = "الرقم طويل جداً";
        if (s.MinPayout is < 0 or > 100_000_000) errors["minPayout"] = "أدخل مبلغاً صحيحاً";
        if (errors.Count > 0) throw ApiException.Validation(errors);

        var values = new Dictionary<string, string>
        {
            [nameof(s.SupportPhone)] = s.SupportPhone?.Trim() ?? "",
            [nameof(s.SupportWhatsapp)] = s.SupportWhatsapp?.Trim() ?? "",
            [nameof(s.MinPayout)] = s.MinPayout.ToString(),
        };
        await using var c = await _db.OpenAsync(ct);
        foreach (var (key, value) in values)
            await c.ExecuteAsync("""
                MERGE promoters.AppSettings AS t USING (SELECT @key AS [Key], @value AS [Value]) AS s ON t.[Key] = s.[Key]
                WHEN MATCHED THEN UPDATE SET [Value] = s.[Value], UpdatedAt = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT ([Key], [Value]) VALUES (s.[Key], s.[Value]);
                """, new { key, value });
        _cached = null;
    }
}
