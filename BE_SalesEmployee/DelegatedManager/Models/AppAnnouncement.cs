namespace BE_SalesEmployee.DelegatedManager.Models;

public sealed class AppAnnouncement
{
    public int Id { get; set; }
    public string AppKey { get; set; } = "";
    /// <summary>text | image</summary>
    public string ContentType { get; set; } = "text";
    public string TextBody { get; set; } = "";
    /// <summary>Banner image shown in the home carousel.</summary>
    public string ImageFileName { get; set; } = "";
    /// <summary>Larger image shown when the user taps to zoom (~80% frame).</summary>
    public string ZoomImageFileName { get; set; } = "";
    public bool HasLink { get; set; }
    public string LinkUrl { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    public AppAnnouncement Clone() => new()
    {
        Id = Id,
        AppKey = AppKey,
        ContentType = ContentType,
        TextBody = TextBody,
        ImageFileName = ImageFileName,
        ZoomImageFileName = ZoomImageFileName,
        HasLink = HasLink,
        LinkUrl = LinkUrl,
        IsEnabled = IsEnabled,
        SortOrder = SortOrder,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc,
        UpdatedBy = UpdatedBy
    };
}
