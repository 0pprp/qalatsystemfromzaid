namespace BE_SalesEmployee.DelegatedManager.Models;

public sealed class MobileAppTutorialLink
{
    public string AppKey { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string VideoUrl { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    public MobileAppTutorialLink Clone() => new()
    {
        AppKey = AppKey,
        TitleAr = TitleAr,
        VideoUrl = VideoUrl,
        IsEnabled = IsEnabled,
        UpdatedAtUtc = UpdatedAtUtc,
        UpdatedBy = UpdatedBy
    };
}
