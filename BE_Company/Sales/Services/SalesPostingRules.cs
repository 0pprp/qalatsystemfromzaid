namespace BE_Company.Sales.Services
{
    public static class SalesPostingStatuses
    {
        public const string Pending = "Pending";
        public const string Processing = "Processing";
        public const string Posted = "Posted";
        public const string Failed = "Failed";

        public static bool IsUnposted(string? status) =>
            string.IsNullOrWhiteSpace(status)
            || status is Pending or Processing or Failed;

        public static bool IsPosted(string? status) =>
            string.Equals(status, Posted, StringComparison.OrdinalIgnoreCase);
    }
}
