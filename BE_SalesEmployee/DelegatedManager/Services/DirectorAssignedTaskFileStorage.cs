namespace BE_SalesEmployee.DelegatedManager.Services;

/// <summary>
/// Stores director assigned-task attachments under ContentRoot/App_Data/assigned-tasks/{taskId}.
/// Allow-list: common images, PDF, Word, Excel.
/// </summary>
public sealed class DirectorAssignedTaskFileStorage
{
    private static readonly HashSet<string> AllowedExt = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif",
        ".pdf",
        ".doc", ".docx",
        ".xls", ".xlsx",
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<DirectorAssignedTaskFileStorage> _log;

    public DirectorAssignedTaskFileStorage(
        IWebHostEnvironment env,
        ILogger<DirectorAssignedTaskFileStorage> log)
    {
        _env = env;
        _log = log;
    }

    public string RootDir =>
        Path.Combine(_env.ContentRootPath, "App_Data", "assigned-tasks");

    public string TaskDir(Guid taskId) =>
        Path.Combine(RootDir, taskId.ToString("N"));

    public async Task<(bool Ok, string? Error, string? FileName)> SaveAsync(
        Guid taskId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length <= 0)
            return (false, "الملف مطلوب.", null);

        if (file.Length > 20 * 1024 * 1024)
            return (false, "حجم الملف يجب ألا يتجاوز 20 ميغابايت.", null);

        var rawExt = Path.GetExtension(file.FileName ?? "");
        var ext = SanitizeExtension(rawExt);
        if (ext == null || !AllowedExt.Contains(ext))
            return (false, "صيغة الملف غير مدعومة (صور، PDF، Word، Excel).", null);

        try
        {
            var dir = TaskDir(taskId);
            Directory.CreateDirectory(dir);
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var path = Path.Combine(dir, fileName);
            await using (var fs = File.Create(path))
            {
                await file.CopyToAsync(fs, ct);
            }

            _log.LogInformation("Assigned-task file saved {TaskId}/{File}", taskId, fileName);
            return (true, null, fileName);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed saving assigned-task file under {Root}/{TaskId}", RootDir, taskId);
            return (false, "تعذر حفظ الملف على السيرفر.", null);
        }
    }

    public (bool Ok, string? FullPath, string? ContentType) Resolve(Guid taskId, string? fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Trim());
        if (string.IsNullOrWhiteSpace(name) || name != fileName?.Trim())
            return (false, null, null);

        var full = Path.Combine(TaskDir(taskId), name);
        if (!File.Exists(full))
            return (false, null, null);

        var ext = Path.GetExtension(name).ToLowerInvariant();
        if (!AllowedExt.Contains(ext))
            return (false, null, null);

        return (true, full, ContentTypeFor(ext));
    }

    public void TryDelete(Guid taskId, string? fileName)
    {
        try
        {
            var (ok, full, _) = Resolve(taskId, fileName);
            if (ok && full != null)
                File.Delete(full);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed deleting assigned-task file {TaskId}/{File}", taskId, fileName);
        }
    }

    private static string? SanitizeExtension(string? rawExt)
    {
        if (string.IsNullOrWhiteSpace(rawExt))
            return null;

        var ext = rawExt.Trim().ToLowerInvariant();
        if (!ext.StartsWith('.'))
            ext = "." + ext;

        // Drop anything after a second dot or path junk.
        var cleaned = new string(ext.TakeWhile(c =>
            c == '.' || char.IsLetterOrDigit(c)).ToArray());

        if (cleaned.Length < 2 || cleaned.Count(c => c == '.') != 1)
            return null;

        return cleaned;
    }

    private static string ContentTypeFor(string ext) => ext switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".gif" => "image/gif",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream",
    };
}
