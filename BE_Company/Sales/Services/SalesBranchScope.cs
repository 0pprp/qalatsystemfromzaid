using BE_Company.Sales.Models;
using Microsoft.AspNetCore.Http;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Shared branch-tenancy rules aligned with <c>SalesFilterService</c> trusted-gateway scoping:
    /// the branch database connection is the tenancy boundary. Legacy <c>SalesRequests.CityValue</c>
    /// may be a short gateway key, SQL catalog name, or Arabic display label and must not be
    /// equality-matched against <see cref="SalesIdentity.BranchId"/> blindly.
    /// </summary>
    public static class SalesBranchScope
    {
        /// <summary>
        /// Trusted gateway already routed the call to the correct branch DB.
        /// </summary>
        public static bool TrustsBranchDatabase(SalesIdentity actor) => actor.IsGateway;

        /// <summary>
        /// True when the value looks like a gateway city short key (e.g. najaf-demo),
        /// not a catalog name, Arabic label, or other legacy display form.
        /// </summary>
        public static bool IsComparableBranchKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var text = value.Trim();
            if (SalesCityDisplay.IsInternalKey(text))
            {
                return false;
            }

            if (text.Contains(' ') || text.Contains('\u00A0'))
            {
                return false;
            }

            // Non-ASCII (e.g. Arabic province labels) are display/legacy, not comparable keys.
            foreach (var c in text)
            {
                if (c > 127)
                {
                    return false;
                }
            }

            return text.Contains('-')
                   || text.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        /// <summary>
        /// Enforces same-branch only when both sides are comparable short keys and differ.
        /// Skips when trusted gateway or when either side is legacy/non-comparable.
        /// </summary>
        public static void EnsureSameComparableBranch(SalesIdentity actor, string? requestCityValue)
        {
            if (TrustsBranchDatabase(actor))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(actor.BranchId) || string.IsNullOrWhiteSpace(requestCityValue))
            {
                return;
            }

            if (!IsComparableBranchKey(actor.BranchId) || !IsComparableBranchKey(requestCityValue))
            {
                return;
            }

            if (!string.Equals(actor.BranchId.Trim(), requestCityValue.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكن نقل طلب تابع لمحافظة أخرى.");
            }
        }
    }
}
