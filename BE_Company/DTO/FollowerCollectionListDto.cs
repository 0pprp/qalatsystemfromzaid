namespace BE_Company.DTO;

public sealed class FollowerCollectionListDto
{
    public int DelegateId { get; set; }
    public string? DelegateName { get; set; }
    public bool FollowerCollectionEnabled { get; set; }
    public int? NumberOfCustomer { get; set; }
    public int? NumberOfCustomerIsNotZero { get; set; }
}
