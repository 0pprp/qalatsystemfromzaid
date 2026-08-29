namespace BE_Company.DTO
{
    public class TransferBoxsGetDTO
    {
        public int? TransferBoxID { get; set; }
        public int? FromBoxID { get; set; }
        public int? ToBoxID { get; set; }
        public int? UserID { get; set; }
        public double? Amount { get; set; }
        public string? Notes { get; set; }
        public DateTime? DateModify { get; set; }
        public DateTime? DateCreate { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public string? FromBoxName { get; set; }
        public string? ToBoxName { get; set; }
        public string? UserName { get; set; }
        public double? AmountDenar { get; set; }
    }
}
