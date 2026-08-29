namespace BE_Company.DTO
{
    public class SuppliersPutDTO
    {
        public int? SupplierID { get; set; }
        public string? SupplierName { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
        public int? UserUpdateID { get; set; }
    }
}
