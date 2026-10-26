using BE_Company.Sales.Services;

namespace BE_Company.Services.CollectionPayments
{
    /// <summary>
    /// Collection payment posting eligibility (mirrors DelegateWeb).
    /// Immediate at CreatedAtUtc — 16:00 Baghdad gate removed.
    /// </summary>
    public static class CollectionPaymentPostingRules
    {
        /// <summary>Legacy constant — gate removed.</summary>
        public static readonly TimeSpan PostingTimeBaghdad = TimeSpan.FromHours(16);
        /// <summary>Deploy verify marker — posting eligibility is immediate at create.</summary>
        public const string EligibilityMode = "ImmediateAtCreatedUtc";
        public const double MinAmountIqd = 2000d;

        public static DateTime ComputeEligibleForPostingAtUtc(DateTime createdAtUtc)
        {
            return DateTime.SpecifyKind(createdAtUtc.ToUniversalTime(), DateTimeKind.Utc);
        }

        public static bool IsEligible(DateTime eligibleForPostingAtUtc, DateTime utcNow) =>
            utcNow.ToUniversalTime() >= eligibleForPostingAtUtc.ToUniversalTime();
    }
}
