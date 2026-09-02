using Microsoft.AspNetCore.Authorization;

namespace BE_SalesEmployee.Sales.Authorization
{
    public static class SalesRoles
    {
        public const string SalesEmployee = "SalesEmployee";
        public const string SalesManager = "SalesManager";
        public const string UserTypeSalesEmployee = "موظف مبيعات";
        public const string UserTypeSalesManager = "مدير مبيعات";

        public static bool IsSalesEmployee(string? userType) =>
            string.Equals(userType, UserTypeSalesEmployee, StringComparison.Ordinal);

        public static bool IsSalesManager(string? userType) =>
            string.Equals(userType, UserTypeSalesManager, StringComparison.Ordinal);

        public static bool IsAnySales(string? userType) =>
            IsSalesEmployee(userType) || IsSalesManager(userType);

        public static string? ToModuleRole(string? userType)
        {
            if (IsSalesEmployee(userType))
            {
                return SalesEmployee;
            }
            if (IsSalesManager(userType))
            {
                return SalesManager;
            }
            return null;
        }
    }

    public static class SalesPolicies
    {
        public const string AnySales = "Sales.Any";
        public const string SalesEmployee = "Sales.Employee";
        public const string SalesManager = "Sales.Manager";
        public const string ReadGps = "Sales.ReadGps";
        public const string ReadOtherSalesEmployees = "Sales.ReadOtherSalesEmployees";
    }

    public sealed class SalesRoleRequirement : IAuthorizationRequirement
    {
        public SalesRoleRequirement(params string[] moduleRoles)
        {
            ModuleRoles = moduleRoles;
        }

        public IReadOnlyList<string> ModuleRoles { get; }
    }

    public sealed class SalesRoleHandler : AuthorizationHandler<SalesRoleRequirement>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            SalesRoleRequirement requirement)
        {
            var userType = context.User.FindFirst("UserType")?.Value;
            var mapped = SalesRoles.ToModuleRole(userType);
            if (mapped != null && requirement.ModuleRoles.Contains(mapped, StringComparer.Ordinal))
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }

    public static class SalesPermissionMatrix
    {
        public static bool CanSearchAllBranches(string? userType) => SalesRoles.IsAnySales(userType);
        public static bool CanWriteOwnBranchOnly(string? userType) => SalesRoles.IsAnySales(userType);
        public static bool CanReadGps(string? userType) => SalesRoles.IsSalesManager(userType);
        public static bool CanReadOtherSalesEmployees(string? userType) => SalesRoles.IsSalesManager(userType);
        public static bool CanReadAllBranchSales(string? userType) => SalesRoles.IsSalesManager(userType);
    }
}
