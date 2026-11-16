namespace BE_CustomerApp.Infrastructure;

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

/// <summary>Public URLs used in links, shares and media.</summary>
public sealed class PublicUrls
{
    public PublicUrls(IConfiguration configuration)
    {
        StoreBaseUrl = (configuration["PublicUrls:Store"] ?? "https://store.qalaa.iq").TrimEnd('/');
        ApiBaseUrl = (configuration["PublicUrls:Api"] ?? "http://localhost:5380").TrimEnd('/');
    }

    public string StoreBaseUrl { get; }
    public string ApiBaseUrl { get; }

    public string ProductShare(int productId) => $"{ApiBaseUrl}/p/{productId}";
    /// <summary>Uploaded files are stored as "media/…" relative paths; external URLs (YouTube, CDN) pass through.</summary>
    public string? MediaUrl(string? stored) =>
        string.IsNullOrWhiteSpace(stored) ? null
        : stored.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || stored.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? stored
        : $"{ApiBaseUrl}/{stored.TrimStart('/')}";

    /// <summary>Inverse of <see cref="MediaUrl"/> for values coming back from the dashboard.</summary>
    public string? ToStored(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var prefix = ApiBaseUrl + "/";
        return url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? url[prefix.Length..] : url.Trim();
    }
}

public static class MediaStorage
{
    /// <summary>Where uploaded shop photos are stored (Media:Root, default App_Data/media under the content root).</summary>
    public static string Root(IConfiguration config, IWebHostEnvironment env) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(config["Media:Root"]) ? Path.Combine(env.ContentRootPath, "App_Data", "media") : config["Media:Root"]!);
}
