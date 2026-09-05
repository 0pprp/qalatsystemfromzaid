namespace BE_Company.Sales.Authorization
{
    /// <summary>
    /// English sales-module roles mapped onto existing Arabic UserType values.
    /// Existing company types stay unchanged.
    /// </summary>
    public static class SalesRoles
    {
        public const string SalesEmployee = "SalesEmployee";
        public const string SalesManager = "SalesManager";

        public const string UserTypeSalesEmployee = "موظف مبيعات";
        public const string UserTypeSalesManager = "مدير مبيعات";

        public const string UserTypeMainAccountant = "محاسب رئيسي";
        public const string UserTypeSubAccountant = "محاسب فرعي";
        public const string UserTypeBranchManager = "مدير فرع";
        public const string UserTypeFollower = "متابع";
        public const string UserTypeDelegate = "مندوب";
        public const string RequestCreator = "RequestCreator";

        public static bool IsSalesEmployee(string? userType) =>
            string.Equals(userType, UserTypeSalesEmployee, StringComparison.Ordinal);

        public static bool IsSalesManager(string? userType) =>
            string.Equals(userType, UserTypeSalesManager, StringComparison.Ordinal);

        public static bool IsBranchManager(string? userType) =>
            string.Equals(userType, UserTypeBranchManager, StringComparison.Ordinal);

        public static bool IsFollower(string? userType) =>
            !string.IsNullOrWhiteSpace(userType)
            && (string.Equals(userType, UserTypeFollower, StringComparison.Ordinal)
                || userType.StartsWith(UserTypeFollower, StringComparison.Ordinal));

        public static bool IsDelegate(string? userType) =>
            !string.IsNullOrWhiteSpace(userType)
            && (string.Equals(userType, UserTypeDelegate, StringComparison.Ordinal)
                || (userType.Contains(UserTypeDelegate, StringComparison.Ordinal)
                    && !IsSalesEmployee(userType)));

        public static bool CanCreateSalesRequest(string? userType) =>
            IsSalesManager(userType) || IsBranchManager(userType) || IsFollower(userType) || IsDelegate(userType);

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
            if (CanCreateSalesRequest(userType))
            {
                return RequestCreator;
            }
            return null;
        }
    }
}
