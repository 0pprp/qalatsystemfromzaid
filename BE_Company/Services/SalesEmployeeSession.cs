using BE_Company.Sales.Authorization;

namespace BE_Company.Services
{
    public static class SalesEmployeeSession
    {
        public const string ClaimName = "SessionVersion";
        public const string Code = "SESSION_REPLACED";
        public const string Message = "تم تسجيل الدخول إلى هذا الحساب من جهاز آخر.";

        public static bool AppliesTo(string? userType) => SalesRoles.IsSalesEmployee(userType);

        public static int ParseClaim(string? raw) =>
            int.TryParse(raw, out var version) ? version : 0;

        public static bool IsCurrent(int tokenVersion, int currentVersion) =>
            tokenVersion == currentVersion;
    }
}
