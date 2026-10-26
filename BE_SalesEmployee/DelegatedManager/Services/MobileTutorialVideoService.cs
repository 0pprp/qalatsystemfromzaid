using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class MobileTutorialVideoService
{
    /// <summary>Apps shown in the director add-video form (current scope).</summary>
    public static readonly IReadOnlyList<(string AppKey, string TitleAr)> ManageableApps =
    [
        ("delegate", "تطبيق المندوب"),
        ("branch-manager", "تطبيق مدير الفرع"),
        ("follower", "تطبيق مسؤول اللجنة"),
    ];

    private readonly IMobileTutorialVideoStore _store;

    public MobileTutorialVideoService(IMobileTutorialVideoStore store)
    {
        _store = store;
    }

    public Task EnsureSchemaAsync(CancellationToken ct) => _store.EnsureSchemaAsync(ct);

    public async Task<IReadOnlyList<object>> ListAppsAsync(CancellationToken ct)
    {
        await _store.EnsureSchemaAsync(ct);
        return ManageableApps
            .Select(a => (object)new { appKey = a.AppKey, titleAr = a.TitleAr })
            .ToList();
    }

    public async Task<IReadOnlyList<MobileAppTutorialVideo>> ListForManagerAsync(CancellationToken ct)
    {
        await _store.EnsureSchemaAsync(ct);
        return await _store.ListAllAsync(ct);
    }

    public async Task<IReadOnlyList<MobileAppTutorialVideo>> ListPublicByAppAsync(string appKey, CancellationToken ct)
    {
        var key = (appKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return Array.Empty<MobileAppTutorialVideo>();
        }

        var rows = await _store.ListByAppAsync(key, enabledOnly: true, ct);
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.VideoUrl))
            .ToList();
    }

    public async Task<(bool Ok, string? Error, MobileAppTutorialVideo? Row)> CreateAsync(
        string? appKey,
        string? questionAr,
        string? videoUrl,
        bool isEnabled,
        string? updatedBy,
        CancellationToken ct)
    {
        var key = (appKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return (false, "التطبيق مطلوب.", null);
        }

        if (key.Length > 64 || key.Contains(' ') || key.Contains('/'))
        {
            return (false, "مفتاح التطبيق غير صالح.", null);
        }

        var question = (questionAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            return (false, "السؤال مطلوب.", null);
        }

        if (question.Length > 400)
        {
            return (false, "السؤال طويل جداً.", null);
        }

        var url = (videoUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            return (false, "رابط الفيديو مطلوب.", null);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return (false, "رابط الفيديو يجب أن يبدأ بـ http أو https.", null);
        }

        var existing = await _store.ListByAppAsync(key, enabledOnly: false, ct);
        var sort = existing.Count == 0 ? 0 : existing.Max(r => r.SortOrder) + 1;
        var now = DateTime.UtcNow;
        var row = new MobileAppTutorialVideo
        {
            AppKey = key,
            QuestionAr = question,
            VideoUrl = url,
            IsEnabled = isEnabled,
            SortOrder = sort,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy.Trim()
        };
        var saved = await _store.InsertAsync(row, ct);
        return (true, null, saved);
    }

    public async Task<(bool Ok, string? Error, MobileAppTutorialVideo? Row)> UpdateAsync(
        int id,
        string? appKey,
        string? questionAr,
        string? videoUrl,
        bool isEnabled,
        string? updatedBy,
        CancellationToken ct)
    {
        var existing = await _store.GetByIdAsync(id, ct);
        if (existing is null)
        {
            return (false, "الفيديو غير موجود.", null);
        }

        var key = string.IsNullOrWhiteSpace(appKey) ? existing.AppKey : appKey.Trim();
        if (string.IsNullOrWhiteSpace(key) || key.Length > 64 || key.Contains(' ') || key.Contains('/'))
        {
            return (false, "مفتاح التطبيق غير صالح.", null);
        }

        var question = (questionAr ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(question))
        {
            return (false, "السؤال مطلوب.", null);
        }

        if (question.Length > 400)
        {
            return (false, "السؤال طويل جداً.", null);
        }

        var url = (videoUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            return (false, "رابط الفيديو مطلوب.", null);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return (false, "رابط الفيديو يجب أن يبدأ بـ http أو https.", null);
        }

        existing.AppKey = key;
        existing.QuestionAr = question;
        existing.VideoUrl = url;
        existing.IsEnabled = isEnabled;
        existing.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? existing.UpdatedBy : updatedBy.Trim();
        await _store.UpdateAsync(existing, ct);
        return (true, null, existing);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await _store.GetByIdAsync(id, ct);
        if (existing is null)
        {
            return (false, "الفيديو غير موجود.");
        }

        await _store.DeleteAsync(id, ct);
        return (true, null);
    }
}
