namespace BE_Company.DTO
{
    public sealed class FollowerListOptionDTO
    {
        public int ListId { get; set; }
        public string ListName { get; set; } = "";
        public string? ReceiptName { get; set; }
        public int? CityId { get; set; }
        public string? CityName { get; set; }
    }

    public sealed class FollowerUserListsPutDTO
    {
        public List<int> ListIds { get; set; } = [];
    }
}
