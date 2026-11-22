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
        public const string Monitor = "Monitor";
        /// <summary>Gateway JWT for تطبيق إدارة الفرع (BranchManagerAppUsers).</summary>
        public const string BranchManager = "BranchManager";
        public const string UserTypeSalesEmployee = "موظف مبيعات";
        public const string UserTypeSalesManager = "مدير مبيعات";
        public const string UserTypeSalesFilterEmployee = "موظف فلترة المبيعات";
        public const string UserTypeDelegatedManager = "مدير مفوض";
        public const string UserTypeMainFollower = "متابع رئيسي";
        public const string UserTypeDirector = "المدير";
        public const string UserTypeMonitor = "المراقب";
        /// <summary>Current name of the central monitor. Legacy <see cref="UserTypeMonitor"/> is the same role.</summary>
        public const string UserTypeMonitorSupervisor = "مسؤول المراقبين";
        public const string UserTypeCommitteeHead = "مسؤول لجنة المراقبة";
        public const string UserTypeCommitteeAssistant = "مساعد مسؤول لجنة المراقبة";
        /// <summary>مسؤول الارشيف — receives paper archives from the committees, all branches.</summary>
        public const string UserTypeArchiveManager = "مسؤول الارشيف";
        public const string ArchiveManager = "ArchiveManager";
        /// <summary>محاسب القانونية — company-side legal desk (legal lists, grouping, تسديدات), all branches.</summary>
        public const string UserTypeLegalAccountant = "محاسب القانونية";
        public const string LegalAccountant = "LegalAccountant";
        /// <summary>مدير القانونية — users of the legal app (cases, lawyers, registers).</summary>
        public const string UserTypeLegalTeam = "مدير القانونية";
        public const string LegalTeam = "LegalTeam";
        public const string UserTypeBranchManager = "مدير فرع";
        public const string CommitteeHead = "CommitteeHead";
        public const string CommitteeAssistant = "CommitteeAssistant";

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

        public static bool IsMonitor(string? userType) =>
            string.Equals(userType, UserTypeMonitor, StringComparison.Ordinal)
            || string.Equals(userType, UserTypeMonitorSupervisor, StringComparison.Ordinal);

        public static bool IsCommitteeHead(string? userType) =>
            string.Equals(userType, UserTypeCommitteeHead, StringComparison.Ordinal);

        public static bool IsCommitteeAssistant(string? userType) =>
            string.Equals(userType, UserTypeCommitteeAssistant, StringComparison.Ordinal);

        public static bool IsCommittee(string? userType) =>
            IsCommitteeHead(userType) || IsCommitteeAssistant(userType);

        public static bool IsArchiveManager(string? userType) =>
            string.Equals(userType, UserTypeArchiveManager, StringComparison.Ordinal);

        public static bool IsLegalAccountant(string? userType) =>
            string.Equals(userType, UserTypeLegalAccountant, StringComparison.Ordinal);

        public static bool IsLegalTeam(string? userType) =>
            string.Equals(userType, UserTypeLegalTeam, StringComparison.Ordinal);

        /// <summary>Who may see القانونية customers and legal lists on the branches.</summary>
        public static bool IsLegalViewer(string? userType) =>
            IsDirector(userType) || IsLegalAccountant(userType) || IsLegalTeam(userType);

        public static bool IsBranchManager(string? userType) =>
            string.Equals(userType, UserTypeBranchManager, StringComparison.Ordinal);

        public static bool IsDirectorOrMonitor(string? userType) =>
            IsDirector(userType) || IsMonitor(userType);

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
            if (IsMonitor(userType))
            {
                return Monitor;
            }
            if (IsCommitteeHead(userType))
            {
                return CommitteeHead;
            }
            if (IsCommitteeAssistant(userType))
            {
                return CommitteeAssistant;
            }
            if (IsBranchManager(userType))
            {
                return BranchManager;
            }
            if (IsArchiveManager(userType))
            {
                return ArchiveManager;
            }
            if (IsLegalAccountant(userType))
            {
                return LegalAccountant;
            }
            if (IsLegalTeam(userType))
            {
                return LegalTeam;
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
        public const string Monitor = "Sales.Monitor";
        /// <summary>Gateway JWT for تطبيق إدارة الفرع (BranchManagerAppUsers.Id in UserID).</summary>
        public const string BranchManager = "Sales.BranchManager";
        /// <summary>Director or monitor — shared «عمل المتابعين» surface.</summary>
        public const string DirectorOrMonitor = "Sales.DirectorOrMonitor";
        /// <summary>المدير + مسؤول المراقبين only (no لجان المراقبة) — e.g. app announcements.</summary>
        public const string DirectorOrSupervisor = "Sales.DirectorOrSupervisor";
        /// <summary>مسؤول المراقبين only — price-board edits (لجان المراقبة have no price board).</summary>
        public const string PriceBoardEditor = "Sales.PriceBoardEditor";
        public const string ReadGps = "Sales.ReadGps";
        public const string ReadOtherSalesEmployees = "Sales.ReadOtherSalesEmployees";
        /// <summary>Authenticated sales manager or delegated manager (exception create/list vs decide).</summary>
        public const string SalesManagerOrDelegatedManager = "Sales.ManagerOrDelegatedManager";
        /// <summary>Central sales manager, director, or monitor (monitor reuses SM sales UI with full write).</summary>
        public const string SalesManagerOrDirector = "Sales.ManagerOrDirector";
        /// <summary>Paper archive chain: لجنة المراقبة (head/assistant) and مسؤول الارشيف.</summary>
        public const string ArchiveFlow = "Sales.ArchiveFlow";
        /// <summary>Company-side legal desk: محاسب القانونية + المدير.</summary>
        public const string LegalDesk = "Legal.Desk";
        /// <summary>Legal app: مدير القانونية + المدير.</summary>
        public const string LegalApp = "Legal.App";
        /// <summary>Read-only views shared by both sides (overview, registers).</summary>
        public const string LegalAny = "Legal.Any";
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
