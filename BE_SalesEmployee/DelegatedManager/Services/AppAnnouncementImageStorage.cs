namespace BE_SalesEmployee.DelegatedManager.Services;

/// <summary>Stores announcement images under ContentRoot/App_Data/announcements.</summary>
public sealed class AppAnnouncementImageStorage
{
    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<AppAnnouncementImageStorage> _log;

    public AppAnnouncementImageStorage(IWebHostEnvironment env, ILogger<AppAnnouncementImageStorage> log)
    {
        _env = env;
        _log = log;
    }

    public string RootDir =>
        Path.Combine(_env.ContentRootPath, "App_Data", "announcements");

    public async Task<(bool Ok, string? Error, string? FileName)> SaveAsync(
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length <= 0)
            return (false, "الصورة مطلوبة.", null);

        if (file.Length > 8 * 1024 * 1024)
            return (false, "حجم الصورة يجب ألا يتجاوز 8 ميغابايت.", null);

        var ext = Path.GetExtension(file.FileName ?? "");
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExt.Contains(ext))
            return (false, "صيغة الصورة غير مدعومة (jpg / png / webp / gif).", null);

        try
        {
            Directory.CreateDirectory(RootDir);
            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var path = Path.Combine(RootDir, fileName);
            await using (var fs = File.Create(path))
            {
                await file.CopyToAsync(fs, ct);
            }

            _log.LogInformation("Announcement image saved {File}", fileName);
            return (true, null, fileName);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed saving announcement image under {Root}", RootDir);
            return (false, "تعذر حفظ ملف الصورة على السيرفر.", null);
        }
    }

    public (bool Ok, string? FullPath, string? ContentType) Resolve(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Trim());
        if (string.IsNullOrWhiteSpace(name) || name != fileName?.Trim())
            return (false, null, null);

        var full = Path.Combine(RootDir, name);
        if (!File.Exists(full))
            return (false, null, null);

        var ext = Path.GetExtension(name).ToLowerInvariant();
        var ct = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
        return (true, full, ct);
    }

    public void TryDelete(string? fileName)
    {
        try
        {
            var (ok, full, _) = Resolve(fileName);
            if (ok && full != null)
                File.Delete(full);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed deleting announcement image {File}", fileName);
        }
    }
}
