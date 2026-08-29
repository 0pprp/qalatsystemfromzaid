using Microsoft.VisualBasic;

namespace BE_Company.DTO
{
    public class BuysPostDTO
    {
        public int? SupplierID { get; set; }
        public int? StoreID { get; set; }
        public int? BoxID { get; set; }
        public int? UserCreateID { get; set; }
        public DateTime? Date { get; set; }
        public double? TotalAmountSpent { get; set; }
        public double? FinalTotalItemCostDenar { get; set; }
        public double? AmountTotalDenar { get; set; }
        public double? RemainingAmountDenar { get; set; }
        public required List<ContentsBuyDTO> Contents { get; set; }
    }
}
