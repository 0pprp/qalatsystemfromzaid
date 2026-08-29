namespace BE_Company.DTO
{
    public class StatisticsAppGetDTO
    {
        public int? NumberOfStores { get; set; } = 0;
        public int? NumberOfItems { get; set; } = 0;
        public int? NumberOfSuppliers { get; set; } = 0;
        public int? NumberOfPurchases { get; set; } = 0;
        public int? NumberOfDelegates { get; set; } = 0;
        public int? NumberOfCustomers { get; set; } = 0;
        public int? NumberOfSales { get; set; } = 0;
        public int? NumberOfPayments { get; set; } = 0;
        public int? NumberOfCashBoxes { get; set; } = 0;
        public int? NumberOfAdditionsToBox { get; set; } = 0;
        public int? NumberOfWithdrawalsFromBox { get; set; } = 0;
        public int? NumberOfTransfersBetweenBoxes { get; set; } = 0;
    }
}
