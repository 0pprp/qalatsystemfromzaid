namespace BE_SalesEmployee.MainFollower.Domain;

public sealed class MainFollowerShiftRecord
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public double? StartLatitude { get; set; }
    public double? StartLongitude { get; set; }
    public double? EndLatitude { get; set; }
    public double? EndLongitude { get; set; }
}

public sealed class MainFollowerLocationPoint
{
    public long Id { get; set; }
    public int ShiftId { get; set; }
    public string UserName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public DateTime RecordedAtUtc { get; set; }
}

public static class MainFollowerShiftRules
{
    public static readonly TimeSpan MinLocationInterval = TimeSpan.FromMinutes(8);
    public static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan StaleWindow = TimeSpan.FromMinutes(60);

    public static string LocationStatus(DateTime? lastAtUtc, DateTime nowUtc, bool hasActiveShift)
    {
        if (!hasActiveShift) return "NoShift";
        if (lastAtUtc == null) return "NoLocation";
        var age = nowUtc - lastAtUtc.Value;
        if (age <= LiveWindow) return "Live";
        if (age <= StaleWindow) return "Stale";
        return "Offline";
    }
}
