namespace BE_Company.Sales.Services
{
    public static class SalesPurchaseRules
    {
        public static string NormalizeInvoiceNumber(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return string.Join(" ", value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        public static bool HasInvoiceNumber(string? value) =>
            !string.IsNullOrWhiteSpace(NormalizeInvoiceNumber(value));
    }
}
