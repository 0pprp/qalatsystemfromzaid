namespace BE_Company.DTO;

public sealed class FollowerCollectionListDto
{
    public int DelegateId { get; set; }
    public string? DelegateName { get; set; }
    public bool FollowerCollectionEnabled { get; set; }
    /// <summary>When follower collection was last enabled (UTC). Null if never / disabled.</summary>
    public DateTime? FollowerCollectionAssignedAtUtc { get; set; }
    public int? NumberOfCustomer { get; set; }
    public int? NumberOfCustomerIsNotZero { get; set; }
}
