namespace BE_Company.DTO
{
    public class SuppliersGetDTO
    {
        public int? SupplierID { get; set; }
        public int? UserID { get; set; }
        public int? CityID { get; set; }
        public string? SupplierName { get; set; }
        public string? Address { get; set; }
        public double? Longitude { get; set; }
        public double? Latitude { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
        public string? SupplierImage { get; set; }
        public bool? SupplierState { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public string? UserName { get; set; }
        public string? CityName { get; set; }
        public double? AmountAccount { get; set; }
    }
}
