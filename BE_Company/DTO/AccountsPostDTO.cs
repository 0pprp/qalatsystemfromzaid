namespace BE_Company.DTO
{
    public class AccountsPostDTO
    {
        public int? UserCreateID { get; set; }
        public int? BoxID { get; set; }
        public double? Amount { get; set; }
        public string? Notes { get; set; }
        public string? Type { get; set; }
        public int? DestinationBoxID { get; set; }
    }
}
