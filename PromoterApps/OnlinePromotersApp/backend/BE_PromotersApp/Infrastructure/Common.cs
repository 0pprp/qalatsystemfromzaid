namespace BE_PromotersApp.Infrastructure;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

public readonly record struct Paging(int Page, int PageSize)
{
    public int Offset => (Page - 1) * PageSize;

    public static Paging From(int? page, int? pageSize, int defaultSize = 20) =>
        new(Math.Max(1, page ?? 1), Math.Clamp(pageSize is null or <= 0 ? defaultSize : pageSize.Value, 1, 100));
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>The API's public address, used for media links (marketing material).</summary>
public sealed class PublicUrls
{
    public PublicUrls(IConfiguration configuration) =>
        ApiBaseUrl = (configuration["PublicUrls:Api"] ?? "http://localhost:5430").TrimEnd('/');

    public string ApiBaseUrl { get; }

    /// <summary>Uploaded files are stored as "media/…" relative paths.</summary>
    public string? MediaUrl(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? null
        : stored.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || stored.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? stored
        : $"{ApiBaseUrl}/{stored.TrimStart('/')}";
}

public static class MediaStorage
{
    /// <summary>Where uploaded files are stored (Media:Root, default App_Data/media under the content root).</summary>
    public static string Root(IConfiguration config, IWebHostEnvironment env) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(config["Media:Root"]) ? Path.Combine(env.ContentRootPath, "App_Data", "media") : config["Media:Root"]!);

    /// <summary>Transfer receipts (Receipts:Root). Never under the public /media folder: served only through authorised endpoints.</summary>
    public static string ReceiptsRoot(IConfiguration config, IWebHostEnvironment env) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(config["Receipts:Root"]) ? Path.Combine(env.ContentRootPath, "App_Data", "receipts") : config["Receipts:Root"]!);
}

/// <summary>Iraq is UTC+3 all year (no daylight saving). Days and months in the app are Iraq calendar days.</summary>
public static class IraqTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    public static DateOnly DateOf(DateTime utc) => DateOnly.FromDateTime(utc + Offset);

    public static DateTime DayStartUtc(DateOnly day) => DateTime.SpecifyKind(day.ToDateTime(TimeOnly.MinValue) - Offset, DateTimeKind.Utc);

    public static DateTime MonthStartUtc(DateTime utc)
    {
        var d = DateOf(utc);
        return DayStartUtc(new DateOnly(d.Year, d.Month, 1));
    }
}

public static class Text
{
    public static string CollapseSpaces(string? s) => string.Join(' ', (s ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    public static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
