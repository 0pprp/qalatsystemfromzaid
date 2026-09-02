using BE_Company.Sales.Authorization;
using BE_Company.Sales.Models;

namespace BE_Company.Sales.Services
{
    public sealed class SalesIdentityService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public SalesIdentityService(IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public SalesIdentity? FromAuthenticatedUser()
        {
            var http = _httpContextAccessor.HttpContext;
            if (http == null)
            {
                return null;
            }

            var userIdRaw = http.Items["UserID"] as string ?? http.User.FindFirst("UserID")?.Value;
            if (!int.TryParse(userIdRaw, out var employeeId) || employeeId <= 0)
            {
                return null;
            }

            var userType = http.Items["UserType"] as string ?? http.User.FindFirst("UserType")?.Value;
            var role = SalesRoles.ToModuleRole(userType);
            if (role == null)
            {
                return null;
            }

            var employeeName = http.User.FindFirst("UserName")?.Value ?? string.Empty;
            var branchId = _configuration["SalesManagement:BranchId"] ?? "najaf-demo";
            var branchName = _configuration["SalesManagement:BranchName"] ?? "النجف - DEMO";

            return new SalesIdentity
            {
                EmployeeId = employeeId,
                EmployeeName = employeeName,
                BranchId = branchId,
                BranchName = branchName,
                Role = role,
                UserType = userType,
                IsSalesShiftStarted = null
            };
        }
    }
}
