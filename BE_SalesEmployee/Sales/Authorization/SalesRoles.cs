using Microsoft.AspNetCore.Authorization;

namespace BE_SalesEmployee.Sales.Authorization
{
    public static class SalesRoles
    {
        public const string SalesEmployee = "SalesEmployee";
        public const string SalesManager = "SalesManager";
        public const string SalesFilterEmployee = "SalesFilterEmployee";
        public const string DelegatedManager = "DelegatedManager";
        public const string MainFollower = "MainFollower";
        public const string Director = "Director";
        public const string UserTypeSalesEmployee = "موظف مبيعات";
        public const string UserTypeSalesManager = "مدير مبيعات";
        public const string UserTypeSalesFilterEmployee = "موظف فلترة المبيعات";
        public const string UserTypeDelegatedManager = "مدير مفوض";
        public const string UserTypeMainFollower = "متابع رئيسي";
        public const string UserTypeDirector = "المدير";

        public static bool IsSalesEmployee(string? userType) =>
            string.Equals(userType, UserTypeSalesEmployee, StringComparison.Ordinal);

        public static bool IsSalesManager(string? userType) =>
            string.Equals(userType, UserTypeSalesManager, StringComparison.Ordinal);

        public static bool IsSalesFilterEmployee(string? userType) =>
            string.Equals(userType, UserTypeSalesFilterEmployee, StringComparison.Ordinal);

        public static bool IsDelegatedManager(string? userType) =>
            string.Equals(userType, UserTypeDelegatedManager, StringComparison.Ordinal);

        public static bool IsMainFollower(string? userType) =>
            string.Equals(userType, UserTypeMainFollower, StringComparison.Ordinal);

        public static bool IsDirector(string? userType) =>
            string.Equals(userType, UserTypeDirector, StringComparison.Ordinal);

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
            if (IsSalesFilterEmployee(userType))
            {
                return SalesFilterEmployee;
            }
            if (IsDelegatedManager(userType))
            {
                return DelegatedManager;
            }
            if (IsMainFollower(userType))
            {
                return MainFollower;
            }
            if (IsDirector(userType))
            {
                return Director;
            }
            return null;
        }
    }

    public static class SalesPolicies
    {
        public const string AnySales = "Sales.Any";
        public const string SalesEmployee = "Sales.Employee";
        public const string SalesManager = "Sales.Manager";
        public const string SalesFilterEmployee = "Sales.FilterEmployee";
        public const string DelegatedManager = "Sales.DelegatedManager";
        public const string MainFollower = "Sales.MainFollower";
        public const string Director = "Sales.Director";
        public const string ReadGps = "Sales.ReadGps";
        public const string ReadOtherSalesEmployees = "Sales.ReadOtherSalesEmployees";
        /// <summary>Authenticated sales manager or delegated manager (exception create/list vs decide).</summary>
        public const string SalesManagerOrDelegatedManager = "Sales.ManagerOrDelegatedManager";
        /// <summary>Central sales manager or director (director reuses SM sales-requests UI).</summary>
        public const string SalesManagerOrDirector = "Sales.ManagerOrDirector";
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
