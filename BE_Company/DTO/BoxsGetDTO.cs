namespace BE_Company.DTO
{
    public class BoxsGetDTO
    {
        public int? BoxID { get; set; }
        public string? BoxName { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public bool? BoxState { get; set; }
        public double? AmountDenar { get; set; }
    }
}
