namespace BE_Company.DTO
{
    public class UsersGetDTO
    {
        public int? UserID { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? UserType { get; set; }
        public string? UserImage { get; set; }
    }
}
