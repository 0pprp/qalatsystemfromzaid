using Dapper;
using BE_CustomerApp.Infrastructure;

namespace BE_CustomerApp.Domain.Admin;

/// <summary>Runtime-editable app settings (dashboard → إعدادات التطبيق). Cached briefly in memory.</summary>
public sealed record AppSettings
{
    /// <summary>Shown to agents in «حسابي» → «الدعم».</summary>
    public string SupportPhone { get; init; } = "";
    public string SupportWhatsapp { get; init; } = "";
    /// <summary>«الهدف اليومي»: minutes of work (inside pin periods) each agent should reach per day unless they have their own (0 = no target).</summary>
    public int DailyWorkMinutesTarget { get; init; }
    // App versions are not settings: the published APK decides them (AppReleaseService).
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
        await using var connection = await _db.OpenAsync(ct);
        var rows = (await connection.QueryAsync<(string Key, string Value)>("SELECT [Key], [Value] FROM agents.AppSettings"))
            .ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
        var d = new AppSettings();
        var settings = new AppSettings
        {
            SupportPhone = rows.GetValueOrDefault(nameof(d.SupportPhone), d.SupportPhone),
            SupportWhatsapp = rows.GetValueOrDefault(nameof(d.SupportWhatsapp), d.SupportWhatsapp),
            DailyWorkMinutesTarget = int.TryParse(rows.GetValueOrDefault(nameof(d.DailyWorkMinutesTarget)), out var target) ? target : 0,
        };
        _cached = settings;
        _cachedAt = DateTime.UtcNow;
        return settings;
    }

    public async Task SaveAsync(AppSettings s, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string>();
        if ((s.SupportPhone?.Length ?? 0) > 20) errors["supportPhone"] = "الرقم طويل جداً";
        if ((s.SupportWhatsapp?.Length ?? 0) > 20) errors["supportWhatsapp"] = "الرقم طويل جداً";
        if (s.DailyWorkMinutesTarget is < 0 or > 1440) errors["dailyWorkMinutesTarget"] = "الهدف بين 0 و24 ساعة";
        if (errors.Count > 0) throw ApiException.Validation(errors);

        var values = new Dictionary<string, string>
        {
            [nameof(s.SupportPhone)] = s.SupportPhone?.Trim() ?? "",
            [nameof(s.SupportWhatsapp)] = s.SupportWhatsapp?.Trim() ?? "",
            [nameof(s.DailyWorkMinutesTarget)] = s.DailyWorkMinutesTarget.ToString(),
        };
        await using var connection = await _db.OpenAsync(ct);
        foreach (var (key, value) in values)
        {
            await connection.ExecuteAsync("""
                MERGE agents.AppSettings AS t USING (SELECT @key AS [Key], @value AS [Value]) AS s ON t.[Key] = s.[Key]
                WHEN MATCHED THEN UPDATE SET [Value] = s.[Value], UpdatedAt = SYSUTCDATETIME()
                WHEN NOT MATCHED THEN INSERT ([Key], [Value]) VALUES (s.[Key], s.[Value]);
                """, new { key, value });
        }
        _cached = null;
    }
}
