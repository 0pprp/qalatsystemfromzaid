namespace BE_SalesEmployee.DelegatedManager.Models;

public sealed class AppCommitteeTask
{
    public int Id { get; set; }
    public string Body { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }

    public AppCommitteeTask Clone() => new()
    {
        Id = Id,
        Body = Body,
        IsEnabled = IsEnabled,
        SortOrder = SortOrder,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc,
        UpdatedBy = UpdatedBy
    };
}
