using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public static class SalesCustomerIdentity
    {
        public static string PreferName(string? confirmed, params string?[] alternatives)
        {
            var current = (confirmed ?? string.Empty).Trim();
            foreach (var alt in alternatives)
            {
                var other = (alt ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(other))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(current))
                {
                    current = other;
                    continue;
                }

                current = MoreCompleteSamePerson(current, other);
            }

            return current;
        }

        public static void ApplyDisplayName(SalesDraftDTO sale)
        {
            sale.FullName = PreferName(sale.FullName, sale.AccountCustomerName, sale.RequestCustomerName);
            sale.AccountCustomerName = null;
            sale.RequestCustomerName = null;
        }

        public static string? PreferText(string? confirmed, params string?[] alternatives)
        {
            if (!string.IsNullOrWhiteSpace(confirmed))
            {
                return confirmed.Trim();
            }

            return alternatives
                .Select(value => value?.Trim())
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        }

        private static string MoreCompleteSamePerson(string left, string right)
        {
            var a = CollapseWs(left);
            var b = CollapseWs(right);
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            {
                return left.Length >= right.Length ? left : right;
            }

            if (IsMoreComplete(b, a))
            {
                return right;
            }

            return left;
        }

        private static bool IsMoreComplete(string longer, string shorter)
        {
            if (longer.Length <= shorter.Length)
            {
                return false;
            }

            return longer.StartsWith(shorter + " ", StringComparison.OrdinalIgnoreCase);
        }

        private static string CollapseWs(string value) =>
            string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
