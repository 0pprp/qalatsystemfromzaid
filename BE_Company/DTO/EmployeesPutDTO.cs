namespace BE_Company.DTO
{
    public class EmployeesPutDTO
    {
        public int? EmployeeID { get; set; }
        public int? UserUpdateID { get; set; }
        public string? EmployeeName { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Notes { get; set; }
    }
}
