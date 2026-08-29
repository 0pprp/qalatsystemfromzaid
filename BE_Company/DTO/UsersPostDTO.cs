namespace BE_Company.DTO
{
    public class UsersPostDTO
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public int? UserCreateID { get; set; }
        public string? UserType { get; set; }
        public IFormFile? UserImage { get; set; }
    }
}
