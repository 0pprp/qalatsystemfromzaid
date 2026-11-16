using Dapper;
using BE_PromotersApp.Auth;
using BE_PromotersApp.Domain.Admin;
using BE_PromotersApp.Infrastructure;

namespace BE_PromotersApp.Domain.Materials;

public sealed record MaterialDto(long Id, string Title, string Kind, string Url, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt);

public sealed record MaterialUpdateInput(string? Title);

/// <summary>
/// Marketing material uploaded from the dashboard (photos, videos, PDFs). Files are public under /media/materials/… so
/// promoters can open, download and share them.
/// </summary>
public sealed class MaterialService
{
    public const long MaxBytes = 300L * 1024 * 1024;

    private static readonly Dictionary<string, (string Kind, string ContentType)> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = ("image", "image/jpeg"), [".jpeg"] = ("image", "image/jpeg"), [".png"] = ("image", "image/png"),
        [".webp"] = ("image", "image/webp"), [".gif"] = ("image", "image/gif"),
        [".mp4"] = ("video", "video/mp4"), [".mov"] = ("video", "video/quicktime"), [".webm"] = ("video", "video/webm"),
        [".m4v"] = ("video", "video/mp4"), [".3gp"] = ("video", "video/3gpp"),
        [".pdf"] = ("pdf", "application/pdf"),
    };

    private readonly Db _db;
    private readonly PublicUrls _urls;
    private readonly AuditService _audit;

    public MaterialService(Db db, PublicUrls urls, AuditService audit)
    {
        _db = db;
        _urls = urls;
        _audit = audit;
    }

    public async Task<IReadOnlyList<MaterialDto>> ListAsync(string? kind, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        var rows = await c.QueryAsync<(long Id, string Title, string Kind, string FilePath, string FileName, string ContentType, long SizeBytes, DateTime CreatedAt)>("""
            SELECT Id, Title, Kind, FilePath, FileName, ContentType, SizeBytes, CreatedAt FROM promoters.Materials
            WHERE IsDeleted = 0 AND (@kind IS NULL OR Kind = @kind) ORDER BY Id DESC
            """, new { kind = Text.NullIfBlank(kind) });
        return rows.Select(r => new MaterialDto(r.Id, r.Title, r.Kind, _urls.MediaUrl(r.FilePath)!, r.FileName, r.ContentType, r.SizeBytes, r.CreatedAt)).ToList();
    }

    public async Task<MaterialDto> UploadAsync(IFormFile? file, string? title, string mediaRoot, AdminActor actor, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw ApiException.Validation(new() { ["file"] = "اختر ملفاً" });
        if (file.Length > MaxBytes) throw ApiException.Validation(new() { ["file"] = "حجم الملف يجب ألا يتجاوز 300 ميغابايت" });
        var ext = Path.GetExtension(file.FileName ?? "");
        if (!Allowed.TryGetValue(ext, out var type))
            throw ApiException.Validation(new() { ["file"] = "الملفات المسموحة: صور (JPG, PNG, WEBP)، فيديو (MP4, MOV, WEBM)، PDF" });

        await using var input = file.OpenReadStream();
        var header = new byte[12];
        var read = await input.ReadAsync(header, ct);
        if (!LooksLike(type.Kind, ext, header.AsSpan(0, read))) throw ApiException.Validation(new() { ["file"] = "محتوى الملف لا يطابق نوعه" });
        input.Position = 0;

        var cleanTitle = Text.CollapseSpaces(title);
        if (cleanTitle.Length == 0) cleanTitle = Text.CollapseSpaces(Path.GetFileNameWithoutExtension(file.FileName));
        cleanTitle = Text.Truncate(cleanTitle.Length == 0 ? "مادة تسويقية" : cleanTitle, 150);

        var month = DateTime.UtcNow.ToString("yyyy-MM");
        var dir = Path.Combine(mediaRoot, "materials", month);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        await using (var output = File.Create(Path.Combine(dir, name)))
            await input.CopyToAsync(output, ct);
        var stored = $"media/materials/{month}/{name}";
        var originalName = Text.Truncate(Path.GetFileName(file.FileName ?? name), 200);

        await using var c = await _db.OpenAsync(ct);
        var id = await c.ExecuteScalarAsync<long>("""
            INSERT promoters.Materials(Title, Kind, FilePath, FileName, ContentType, SizeBytes, CreatedBy) OUTPUT inserted.Id
            VALUES (@cleanTitle, @Kind, @stored, @originalName, @ContentType, @Length, @Name)
            """, new { cleanTitle, type.Kind, stored, originalName, type.ContentType, file.Length, actor.Name });
        await _audit.WriteAsync(actor, "material.create", "Material", id, null, new { cleanTitle, type.Kind, file.Length }, c);
        return new MaterialDto(id, cleanTitle, type.Kind, _urls.MediaUrl(stored)!, originalName, type.ContentType, file.Length, DateTime.UtcNow);
    }

    public async Task UpdateAsync(long id, MaterialUpdateInput input, AdminActor actor, CancellationToken ct)
    {
        var title = Text.CollapseSpaces(input.Title);
        if (title.Length is 0 or > 150) throw ApiException.Validation(new() { ["title"] = "أدخل العنوان (150 حرفاً كحد أقصى)" });
        await using var c = await _db.OpenAsync(ct);
        if (await c.ExecuteAsync("UPDATE promoters.Materials SET Title = @title WHERE Id = @id AND IsDeleted = 0", new { id, title }) == 0)
            throw ApiException.NotFound("المادة غير موجودة");
        await _audit.WriteAsync(actor, "material.update", "Material", id, null, new { title }, c);
    }

    /// <summary>Hidden from the app. The file is kept (already shared copies point to it).</summary>
    public async Task DeleteAsync(long id, AdminActor actor, CancellationToken ct)
    {
        await using var c = await _db.OpenAsync(ct);
        if (await c.ExecuteAsync("UPDATE promoters.Materials SET IsDeleted = 1 WHERE Id = @id AND IsDeleted = 0", new { id }) == 0)
            throw ApiException.NotFound("المادة غير موجودة");
        await _audit.WriteAsync(actor, "material.delete", "Material", id, null, null, c);
    }

    private static bool LooksLike(string kind, string ext, ReadOnlySpan<byte> h)
    {
        switch (kind)
        {
            case "pdf":
                return h.Length >= 4 && h[..4].SequenceEqual("%PDF"u8);
            case "image":
                return (h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
                       || (h.Length >= 4 && h[..4].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }))
                       || (h.Length >= 12 && h[..4].SequenceEqual("RIFF"u8) && h.Slice(8, 4).SequenceEqual("WEBP"u8))
                       || (h.Length >= 4 && h[..4].SequenceEqual("GIF8"u8));
            default:
                // MP4/MOV/3GP: "ftyp" box at offset 4; WebM: EBML header.
                return (h.Length >= 8 && h.Slice(4, 4).SequenceEqual("ftyp"u8))
                       || (h.Length >= 4 && h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3)
                       || (ext.Equals(".mov", StringComparison.OrdinalIgnoreCase) && h.Length >= 8
                           && (h.Slice(4, 4).SequenceEqual("moov"u8) || h.Slice(4, 4).SequenceEqual("wide"u8) || h.Slice(4, 4).SequenceEqual("mdat"u8)));
        }
    }
}
