namespace BE_Company.DTO
{
    public class ExchangesItemsGetDTO
    {
        public int? ExchangeItemID { get; set; }
        public int? UserID { get; set; }
        public int? CityID { get; set; }
        public string? ExchangeItemName { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public double? LimitAmount { get; set; }
        public bool? ExchangeItemsState { get; set; }
        public string? UserName { get; set; }
        public string? CityName { get; set; }
        public double? AmountAccount { get; set; }
    }
}
