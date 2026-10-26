namespace BE_SalesEmployee.DelegatedManager.Models;

/// <summary>One educational Q&amp;A video for a mobile app (many per AppKey).</summary>
public sealed class MobileAppTutorialVideo
{
    public int Id { get; set; }
    public string AppKey { get; set; } = string.Empty;
    public string QuestionAr { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    public MobileAppTutorialVideo Clone() => new()
    {
        Id = Id,
        AppKey = AppKey,
        QuestionAr = QuestionAr,
        VideoUrl = VideoUrl,
        IsEnabled = IsEnabled,
        SortOrder = SortOrder,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc,
        UpdatedBy = UpdatedBy
    };
}
