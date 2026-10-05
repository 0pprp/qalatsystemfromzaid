using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class MobileTutorialLinkService
{
    public static readonly IReadOnlyList<(string AppKey, string TitleAr)> KnownApps =
    [
        ("delegate", "تطبيق المندوب"),
        ("follower", "تطبيق المتابع"),
        ("main-follower", "تطبيق المتابع الرئيسي"),
        ("delegated-manager", "تطبيق المدير المفوض"),
        ("branch-manager", "تطبيق مدير الفرع"),
        ("sales-employee", "تطبيق موظف المبيعات"),
        ("accountant", "لوحة المحاسب الرئيسي"),
    ];

    private readonly IMobileTutorialLinkStore _store;

    public MobileTutorialLinkService(IMobileTutorialLinkStore store)
    {
        _store = store;
    }

    public async Task<IReadOnlyList<MobileAppTutorialLink>> ListForManagerAsync(CancellationToken ct)
    {
        await _store.EnsureSchemaAsync(ct);
        var existing = (await _store.ListAsync(ct)).ToDictionary(r => r.AppKey, StringComparer.OrdinalIgnoreCase);
        var merged = new List<MobileAppTutorialLink>();
        foreach (var (appKey, title) in KnownApps)
        {
            if (existing.TryGetValue(appKey, out var row))
            {
                merged.Add(row);
                existing.Remove(appKey);
            }
            else
            {
                merged.Add(new MobileAppTutorialLink
                {
                    AppKey = appKey,
                    TitleAr = title,
                    VideoUrl = string.Empty,
                    IsEnabled = false,
                    UpdatedAtUtc = DateTime.UtcNow
                });
            }
        }

        merged.AddRange(existing.Values.OrderBy(r => r.TitleAr, StringComparer.OrdinalIgnoreCase));
        return merged;
    }

    public async Task<MobileAppTutorialLink?> GetPublicAsync(string appKey, CancellationToken ct)
    {
        var row = await _store.GetAsync(appKey, ct);
        if (row is null || !row.IsEnabled || string.IsNullOrWhiteSpace(row.VideoUrl))
        {
            return null;
        }

        return row;
    }

    public async Task<(bool Ok, string? Error, MobileAppTutorialLink? Row)> UpsertAsync(
        string appKey,
        string? titleAr,
        string? videoUrl,
        bool isEnabled,
        string? updatedBy,
        CancellationToken ct)
    {
        var key = (appKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (false, "مفتاح التطبيق مطلوب.", null);
        }

        if (key.Length > 64 || key.Contains(' ') || key.Contains('/'))
        {
            return (false, "مفتاح التطبيق غير صالح.", null);
        }

        var title = (titleAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            var known = KnownApps.FirstOrDefault(a =>
                string.Equals(a.AppKey, key, StringComparison.OrdinalIgnoreCase));
            title = string.IsNullOrWhiteSpace(known.TitleAr) ? key : known.TitleAr;
        }

        var url = (videoUrl ?? string.Empty).Trim();
        if (isEnabled && string.IsNullOrWhiteSpace(url))
        {
            return (false, "رابط الفيديو مطلوب عند التفعيل.", null);
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "رابط الفيديو يجب أن يبدأ بـ http أو https.", null);
            }
        }

        var row = new MobileAppTutorialLink
        {
            AppKey = key,
            TitleAr = title,
            VideoUrl = url,
            IsEnabled = isEnabled && !string.IsNullOrWhiteSpace(url),
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy.Trim()
        };
        await _store.UpsertAsync(row, ct);
        return (true, null, row);
    }
}
