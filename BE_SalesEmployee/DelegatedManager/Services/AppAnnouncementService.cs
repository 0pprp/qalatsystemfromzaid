using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class AppAnnouncementService
{
    public static readonly IReadOnlyList<(string AppKey, string TitleAr)> ManageableApps =
    [
        ("delegate", "تطبيق المندوب"),
        ("follower", "تطبيق المتابع"),
    ];

    private readonly IAppAnnouncementStore _store;
    private readonly AppAnnouncementImageStorage _images;

    public AppAnnouncementService(IAppAnnouncementStore store, AppAnnouncementImageStorage images)
    {
        _store = store;
        _images = images;
    }

    public Task EnsureSchemaAsync(CancellationToken ct) => _store.EnsureSchemaAsync(ct);

    public async Task<IReadOnlyList<object>> ListAppsAsync(CancellationToken ct)
    {
        await _store.EnsureSchemaAsync(ct);
        return ManageableApps
            .Select(a => (object)new { appKey = a.AppKey, titleAr = a.TitleAr })
            .ToList();
    }

    public Task<IReadOnlyList<AppAnnouncement>> ListForManagerAsync(CancellationToken ct) =>
        _store.ListAllAsync(ct);

    public async Task<IReadOnlyList<AppAnnouncement>> ListPublicByAppAsync(string appKey, CancellationToken ct)
    {
        var key = (appKey ?? "").Trim();
        if (string.IsNullOrWhiteSpace(key))
            return Array.Empty<AppAnnouncement>();

        var rows = await _store.ListByAppAsync(key, enabledOnly: true, ct);
        return rows.Where(IsPublicReady).ToList();
    }

    public async Task<(bool Ok, string? Error, AppAnnouncement? Row)> CreateAsync(
        string? appKey,
        string? contentType,
        string? textBody,
        string? imageFileName,
        string? zoomImageFileName,
        bool hasLink,
        string? linkUrl,
        bool isEnabled,
        string? updatedBy,
        CancellationToken ct)
    {
        var (ok, error, draft) = ValidateDraft(
            appKey, contentType, textBody, imageFileName, zoomImageFileName, hasLink, linkUrl);
        if (!ok || draft is null)
            return (false, error, null);

        var existing = await _store.ListByAppAsync(draft.AppKey, enabledOnly: false, ct);
        draft.SortOrder = existing.Count == 0 ? 0 : existing.Max(r => r.SortOrder) + 1;
        draft.IsEnabled = isEnabled;
        draft.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? null : updatedBy.Trim();
        var now = DateTime.UtcNow;
        draft.CreatedAtUtc = now;
        draft.UpdatedAtUtc = now;

        var saved = await _store.InsertAsync(draft, ct);
        return (true, null, saved);
    }

    public async Task<(bool Ok, string? Error, AppAnnouncement? Row)> UpdateAsync(
        int id,
        string? appKey,
        string? contentType,
        string? textBody,
        string? imageFileName,
        string? zoomImageFileName,
        bool hasLink,
        string? linkUrl,
        bool isEnabled,
        string? updatedBy,
        CancellationToken ct)
    {
        var existing = await _store.GetByIdAsync(id, ct);
        if (existing is null)
            return (false, "الإعلان غير موجود.", null);

        var keepImage = string.IsNullOrWhiteSpace(imageFileName)
            ? existing.ImageFileName
            : imageFileName;
        var keepZoom = string.IsNullOrWhiteSpace(zoomImageFileName)
            ? existing.ZoomImageFileName
            : zoomImageFileName;

        var (ok, error, draft) = ValidateDraft(
            string.IsNullOrWhiteSpace(appKey) ? existing.AppKey : appKey,
            contentType ?? existing.ContentType,
            textBody,
            keepImage,
            keepZoom,
            hasLink,
            linkUrl);
        if (!ok || draft is null)
            return (false, error, null);

        var oldImage = existing.ImageFileName;
        var oldZoom = existing.ZoomImageFileName;
        existing.AppKey = draft.AppKey;
        existing.ContentType = draft.ContentType;
        existing.TextBody = draft.TextBody;
        existing.ImageFileName = draft.ImageFileName;
        existing.ZoomImageFileName = draft.ZoomImageFileName;
        existing.HasLink = draft.HasLink;
        existing.LinkUrl = draft.LinkUrl;
        existing.IsEnabled = isEnabled;
        existing.UpdatedBy = string.IsNullOrWhiteSpace(updatedBy) ? existing.UpdatedBy : updatedBy.Trim();

        await _store.UpdateAsync(existing, ct);

        if (!string.Equals(oldImage, existing.ImageFileName, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(oldImage)
            && !string.Equals(oldImage, existing.ZoomImageFileName, StringComparison.OrdinalIgnoreCase))
        {
            _images.TryDelete(oldImage);
        }

        if (!string.Equals(oldZoom, existing.ZoomImageFileName, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(oldZoom)
            && !string.Equals(oldZoom, existing.ImageFileName, StringComparison.OrdinalIgnoreCase))
        {
            _images.TryDelete(oldZoom);
        }

        return (true, null, existing);
    }

    public async Task<(bool Ok, string? Error)> DeleteAsync(int id, CancellationToken ct)
    {
        var existing = await _store.GetByIdAsync(id, ct);
        if (existing is null)
            return (false, "الإعلان غير موجود.");

        await _store.DeleteAsync(id, ct);
        if (!string.IsNullOrWhiteSpace(existing.ImageFileName))
            _images.TryDelete(existing.ImageFileName);
        if (!string.IsNullOrWhiteSpace(existing.ZoomImageFileName)
            && !string.Equals(existing.ZoomImageFileName, existing.ImageFileName, StringComparison.OrdinalIgnoreCase))
        {
            _images.TryDelete(existing.ZoomImageFileName);
        }
        return (true, null);
    }

    public Task<(bool Ok, string? Error, string? FileName)> SaveImageAsync(IFormFile file, CancellationToken ct) =>
        _images.SaveAsync(file, ct);

    public (bool Ok, string? FullPath, string? ContentType) ResolveImage(string? fileName) =>
        _images.Resolve(fileName);

    private static (bool Ok, string? Error, AppAnnouncement? Draft) ValidateDraft(
        string? appKey,
        string? contentType,
        string? textBody,
        string? imageFileName,
        string? zoomImageFileName,
        bool hasLink,
        string? linkUrl)
    {
        var key = (appKey ?? "").Trim();
        if (key is not ("delegate" or "follower"))
            return (false, "اختر المندوب أو المتابع.", null);

        var type = (contentType ?? "").Trim().ToLowerInvariant();
        if (type is not ("text" or "image"))
            return (false, "نوع الإعلان يجب أن يكون نص أو صورة.", null);

        var text = (textBody ?? "").Trim();
        var image = (imageFileName ?? "").Trim();
        var zoom = (zoomImageFileName ?? "").Trim();

        if (type == "text")
        {
            if (string.IsNullOrWhiteSpace(text))
                return (false, "نص الإعلان مطلوب.", null);
            if (text.Length > 2000)
                return (false, "نص الإعلان طويل جداً.", null);
            image = "";
            zoom = "";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(image))
                return (false, "صورة لوحة الإعلان مطلوبة.", null);
            if (string.IsNullOrWhiteSpace(zoom))
                return (false, "صورة التكبير مطلوبة.", null);
            text = "";
        }

        var url = (linkUrl ?? "").Trim();
        if (hasLink)
        {
            if (string.IsNullOrWhiteSpace(url))
                return (false, "أدخل الرابط.", null);
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return (false, "الرابط يجب أن يبدأ بـ http أو https.", null);
            }
        }
        else
        {
            url = "";
        }

        return (true, null, new AppAnnouncement
        {
            AppKey = key,
            ContentType = type,
            TextBody = text,
            ImageFileName = image,
            ZoomImageFileName = zoom,
            HasLink = hasLink,
            LinkUrl = url
        });
    }

    private static bool IsPublicReady(AppAnnouncement r)
    {
        if (string.Equals(r.ContentType, "text", StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(r.TextBody);
        if (string.Equals(r.ContentType, "image", StringComparison.OrdinalIgnoreCase))
            return !string.IsNullOrWhiteSpace(r.ImageFileName);
        return false;
    }
}
