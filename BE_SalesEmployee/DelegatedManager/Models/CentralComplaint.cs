using BE_SalesEmployee.DelegatedManager.Domain;

namespace BE_SalesEmployee.DelegatedManager.Models;

/// <summary>Central inbox row. Sender identity always comes from the authenticated principal.</summary>
public sealed class CentralComplaint
{
    public Guid Id { get; set; }
    public string SourceApp { get; set; } = "";
    public string? SourceType { get; set; }
    public string? SenderUserId { get; set; }
    public string? SenderUserName { get; set; }
    public string SenderDisplayName { get; set; } = "";
    public string SenderRole { get; set; } = "";
    public string? CityValue { get; set; }
    public string? CityName { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public string Status { get; set; } = ComplaintStatuses.Unread;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public string? MetadataJson { get; set; }

    public CentralComplaint Clone() => (CentralComplaint)MemberwiseClone();
}
