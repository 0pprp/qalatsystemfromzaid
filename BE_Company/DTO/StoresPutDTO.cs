namespace BE_Company.DTO
{
    public class StoresPutDTO
    {
        public int? StoreID { get; set; }
        public int? UserUpdateID { get; set; }
        public string? StoreName { get; set; }
        public string? StorePlace { get; set; }
        public string? Notes { get; set; }
    }
}
