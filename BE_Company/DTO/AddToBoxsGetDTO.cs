namespace BE_Company.DTO
{
    public class AddToBoxsGetDTO
    {
        public int? AddToBoxID { get; set; }
        public int? BoxID { get; set; }
        public double? Amount { get; set; }
        public string? Notes { get; set; }
        public int? UserID { get; set; }
        public int? SupplierID { get; set; }
        public int? DelegateID { get; set; }
        public DateTime? DateCreate { get; set; }
        public DateTime? DateModify { get; set; }
        public int? CustomerPaymentID { get; set; }
        public int? EmployeeID { get; set; }
        public int? DocumentID { get; set; }
        public int? CustomerID { get; set; }
        public int? TransferBoxID { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public int? CustomerPaymentBalanceID { get; set; }
        public int? CustomerIDPayment { get; set; }
        public bool? AccountZero { get; set; }
        public string? BoxName { get; set; }
        public double? AmountDenar { get; set; }
        public string? UserName { get; set; }
        public string? SupplierName { get; set; }
        public string? DelegateName { get; set; }
        public string? EmployeeName { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerNamePayment { get; set; }
    }
}
