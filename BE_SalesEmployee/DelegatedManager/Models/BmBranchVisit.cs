namespace BE_SalesEmployee.DelegatedManager.Models;

public static class BmBranchVisitStatus
{
    public const string Open = "Open";
    public const string Closed = "Closed";
}

public sealed class BmBranchVisit
{
    public Guid Id { get; set; }
    public string CityValue { get; set; } = "";
    public string? CityName { get; set; }
    public Guid OpenedByUserId { get; set; }
    public string OpenedByUserName { get; set; } = "";
    public string OpenedByDisplayName { get; set; } = "";
    public DateTime ArrivedAtUtc { get; set; }
    public DateTime? DepartedAtUtc { get; set; }
    public string Status { get; set; } = BmBranchVisitStatus.Open;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<BmBranchVisitMember> Members { get; set; } = [];
    public List<BmBranchVisitDelegateRating> DelegateRatings { get; set; } = [];
    public List<BmBranchVisitAttachment> Attachments { get; set; } = [];

    public BmBranchVisit Clone()
    {
        var copy = (BmBranchVisit)MemberwiseClone();
        copy.Members = Members.Select(m => m.Clone()).ToList();
        copy.DelegateRatings = DelegateRatings.Select(r => r.Clone()).ToList();
        copy.Attachments = Attachments.Select(a => a.Clone()).ToList();
        return copy;
    }
}

public sealed class BmBranchVisitMember
{
    public Guid VisitId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";

    public BmBranchVisitMember Clone() => (BmBranchVisitMember)MemberwiseClone();
}

public sealed class BmBranchVisitDelegateRating
{
    public Guid VisitId { get; set; }
    public int DelegateId { get; set; }
    public string DelegateName { get; set; } = "";
    public int Score { get; set; }
    public Guid RatedByUserId { get; set; }
    public string? RatedByDisplayName { get; set; }
    public DateTime RatedAtUtc { get; set; }

    public BmBranchVisitDelegateRating Clone() => (BmBranchVisitDelegateRating)MemberwiseClone();
}

public sealed class BmBranchVisitAttachment
{
    public Guid Id { get; set; }
    public Guid VisitId { get; set; }
    public string FileName { get; set; } = "";
    public string StoredFileName { get; set; } = "";
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public string? UploadedBy { get; set; }

    public BmBranchVisitAttachment Clone() => (BmBranchVisitAttachment)MemberwiseClone();
}
