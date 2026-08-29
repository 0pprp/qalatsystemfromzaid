namespace BE_Company.DTO
{
    public class CustomersSalesPostDTO
    {
        public int? DelegateID { get; set; }  
        public int? StoreID { get; set; }   
        public int? UserCreateID { get; set; }
        public string? CustomerName { get; set; } 
        public string? PhoneNumber { get; set; }   
        public string? Address { get; set; } 
        public string? ShopName { get; set; }   
        public string? NearestFunctionPoint { get; set; }  
        public string? SaleName { get; set; }  
        public string? ReceiptName { get; set; }  
        public string? Notes { get; set; }   
        public DateTime? DateCreate { get; set; }  
        public double? AmountPriceTotal { get; set; }  
        public double? AmountPriceTotalFinal { get; set; }  
        public double? AmountDayTotal { get; set; }  
        public double? AmountDayTotalFinal { get; set; }   
        public double? DiscountAmountTotal { get; set; }  
        public double? DiscountAmountDay { get; set; }  
        public required List<ContentsSaleDTO> Contents { get; set; }
    }
}
