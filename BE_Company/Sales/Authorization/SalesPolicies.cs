using Microsoft.AspNetCore.Authorization;

namespace BE_Company.Sales.Authorization
{
    public static class SalesPolicies
    {
        public const string AnySales = "Sales.Any";
        public const string SalesEmployee = "Sales.Employee";
        public const string SalesManager = "Sales.Manager";
        public const string SearchAllBranches = "Sales.SearchAllBranches";
        public const string WriteOwnBranch = "Sales.WriteOwnBranch";
        public const string ReadGps = "Sales.ReadGps";
        public const string ReadOtherSalesEmployees = "Sales.ReadOtherSalesEmployees";
        public const string ReadAllBranchSales = "Sales.ReadAllBranchSales";
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

    /// <summary>
    /// Permission matrix for later GPS / inventory / sales-write endpoints.
    /// SalesEmployee: search-all via search API only; writes on own branch; no GPS; no other employees.
    /// SalesManager: later can view employees, all-branch sales, GPS. GPS is not implemented now.
    /// </summary>
    public static class SalesPermissionMatrix
    {
        public static bool CanSearchAllBranches(string? userType) => SalesRoles.IsAnySales(userType);

        public static bool CanWriteOwnBranchOnly(string? userType) => SalesRoles.IsAnySales(userType);

        public static bool CanReadGps(string? userType) => SalesRoles.IsSalesManager(userType);

        public static bool CanReadOtherSalesEmployees(string? userType) => SalesRoles.IsSalesManager(userType);

        public static bool CanReadAllBranchSales(string? userType) => SalesRoles.IsSalesManager(userType);
    }
}
