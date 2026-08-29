namespace BE_Company.DTO
{
    public class ExchangesItemsPutDTO
    {
        public int? ExchangeItemID { get; set; }
        public int? UserUpdateID { get; set; }
        public string? ExchangeItemName { get; set; }
        public int? LimitAmount { get; set; }
    }
}
