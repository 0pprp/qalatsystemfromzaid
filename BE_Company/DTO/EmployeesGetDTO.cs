namespace BE_Company.DTO
{
    public class EmployeesGetDTO
    {
        public int? EmployeeID { get; set; }
        public int? UserID { get; set; }
        public int? CityID { get; set; }
        public string? EmployeeName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? AcademicAchievement { get; set; }
        public string? CV { get; set; }
        public string? Attachments { get; set; }
        public DateTime? DateOfJoin { get; set; }
        public string? EmployeeImage { get; set; }
        public string? Notes { get; set; }
        public bool? EmployeeState { get; set; }
        public double? Salary { get; set; }
        public bool? AsyncState { get; set; }
        public string? AsyncID { get; set; }
        public string? UserName { get; set; }
        public string? CityName { get; set; }
        public double? SalaryDenar { get; set; }
        public double? AmountAccount { get; set; }
        public double? FinalSalaryDenar { get; set; }
    }
}
