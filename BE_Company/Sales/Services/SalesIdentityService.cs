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

            var userType = http.Items["UserType"] as string ?? http.User.FindFirst("UserType")?.Value;
            var role = SalesRoles.ToModuleRole(userType);
            if (role == null)
            {
                return null;
            }

            var userIdRaw = http.Items["UserID"] as string ?? http.User.FindFirst("UserID")?.Value;
            _ = int.TryParse(userIdRaw, out var employeeId);
            var authSource = http.User.FindFirst(SalesGatewayKeyHandler.AuthSourceClaim)?.Value;
            var isGatewayManager = string.Equals(authSource, SalesGatewayKeyHandler.AuthSourceGateway, StringComparison.Ordinal)
                                   && SalesRoles.IsSalesManager(userType);
            if (employeeId <= 0 && !isGatewayManager)
            {
                return null;
            }

            var employeeName = http.User.FindFirst("UserName")?.Value ?? string.Empty;
            var requireDemo = _configuration.GetValue("SalesManagement:RequireDemoDatabase", true);
            var branchId = _configuration["SalesManagement:BranchId"];
            var branchName = _configuration["SalesManagement:BranchName"];
            if (string.IsNullOrWhiteSpace(branchId))
            {
                var cs = _configuration.GetConnectionString("DataBaseConnection");
                branchId = string.IsNullOrWhiteSpace(cs)
                    ? (requireDemo ? "najaf-demo" : "branch")
                    : new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs).InitialCatalog;
            }
            if (string.IsNullOrWhiteSpace(branchName))
            {
                branchName = requireDemo ? "النجف - DEMO" : branchId;
            }

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
