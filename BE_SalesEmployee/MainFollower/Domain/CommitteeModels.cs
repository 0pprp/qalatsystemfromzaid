namespace BE_SalesEmployee.MainFollower.Domain;

public static class CommitteeMissionStatuses
{
    public const string Active = "Active";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public sealed class CommitteeRecord
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string CreatedByUserName { get; set; } = "";
    public string CreatedByDisplayName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyList<string> MemberUserNames { get; set; } = Array.Empty<string>();
}

public sealed class MissionRecord
{
    public int Id { get; set; }
    public int CommitteeId { get; set; }
    public string CommitteeName { get; set; } = "";
    public string Status { get; set; } = CommitteeMissionStatuses.Active;
    public string? Note { get; set; }
    public string CreatedByUserName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
    public IReadOnlyList<string> CityValues { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> MemberUserNames { get; set; } = Array.Empty<string>();
}

public sealed class CommitteeException : Exception
{
    public CommitteeException(string message) : base(message)
    {
    }
}
