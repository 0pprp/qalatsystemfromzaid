namespace BE_Company.DTO
{
    public class CustomersPutDTO
    {
        public int? CustomerID { get; set; }
        public int? UserUpdateID { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? ShopName { get; set; }
        public string? SaleName { get; set; }
        public string? Notes { get; set; }
    }
}
