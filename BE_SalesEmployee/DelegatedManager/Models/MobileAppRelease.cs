namespace BE_SalesEmployee.DelegatedManager.Models;

/// <summary>Published APK metadata for a mobile app key. Sha256 lets the client verify before install.</summary>
public sealed class MobileAppRelease
{
    public string AppKey { get; set; } = "";
    public string LatestVersionName { get; set; } = "";
    public int LatestVersionCode { get; set; }
    public int MinimumSupportedVersionCode { get; set; }
    public bool ForceUpdate { get; set; }
    public string ApkUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public string? ReleaseNotes { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public MobileAppRelease Clone() => (MobileAppRelease)MemberwiseClone();
}
