namespace BE_SalesEmployee.MainFollower.Domain;

public sealed class StaffReviewRecord
{
    public int Id { get; set; }
    public int MissionId { get; set; }
    public string CommitteeName { get; set; } = "";
    public string CityValue { get; set; } = "";
    public string TargetType { get; set; } = "";
    public int TargetId { get; set; }
    public string? TargetName { get; set; }
    public int? ListId { get; set; }
    public int Score { get; set; }
    public string? UserNote { get; set; }
    public string FeedNote { get; set; } = "";
    public string ReviewerUserName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}
