namespace BE_Company.DTO
{
    public class ChangePaymentDateDTO
    {
        public required List<int> Ids { get; set; }
        public DateTime? NewDate { get; set; }
    }
}
