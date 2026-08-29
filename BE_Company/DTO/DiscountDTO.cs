namespace BE_Company.DTO
{
    public class DiscountDTO
    {
        public int? CustomerSaleID { get; set; }
        public int? UserUpdateID { get; set; }
        public double? DiscountAmountTotalDenar { get; set; }
        public double? DiscountAmountTotalDayDenar { get; set; }
        public DateTime? DateCreate { get; set; }
    }
}
