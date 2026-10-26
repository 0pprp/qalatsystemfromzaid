namespace BE_SalesEmployee.DelegatedManager.Services;

/// <summary>Stores BM branch-visit attachments under App_Data/branch-visits/{visitId}.</summary>
public sealed class BmBranchVisitFileStorage
{
    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".pdf",
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<BmBranchVisitFileStorage> _log;

    public BmBranchVisitFileStorage(IWebHostEnvironment env, ILogger<BmBranchVisitFileStorage> log)
    {
        _env = env;
        _log = log;
    }

    public string RootDir => Path.Combine(_env.ContentRootPath, "App_Data", "branch-visits");

    public string VisitDir(Guid visitId) => Path.Combine(RootDir, visitId.ToString("N"));

    public async Task<(bool Ok, string? Error, string? StoredFileName)> SaveAsync(
        Guid visitId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length <= 0)
            return (false, "الملف مطلوب.", null);
        if (file.Length > 20 * 1024 * 1024)
            return (false, "حجم الملف يجب ألا يتجاوز 20 ميغابايت.", null);

        var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExt.Contains(ext))
            return (false, "صيغة الملف غير مدعومة (صور أو PDF).", null);

        try
        {
            var dir = VisitDir(visitId);
            Directory.CreateDirectory(dir);
            var stored = $"{Guid.NewGuid():N}{ext}";
            await using (var fs = File.Create(Path.Combine(dir, stored)))
                await file.CopyToAsync(fs, ct);
            return (true, null, stored);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed saving branch-visit file {VisitId}", visitId);
            return (false, "تعذر حفظ الملف على السيرفر.", null);
        }
    }

    public (bool Ok, string? FullPath, string? ContentType) Resolve(Guid visitId, string? storedFileName)
    {
        var name = Path.GetFileName((storedFileName ?? "").Trim());
        if (string.IsNullOrWhiteSpace(name)) return (false, null, null);
        var full = Path.Combine(VisitDir(visitId), name);
        if (!File.Exists(full)) return (false, null, null);
        var ext = Path.GetExtension(name).ToLowerInvariant();
        var ct = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg",
        };
        return (true, full, ct);
    }
}
