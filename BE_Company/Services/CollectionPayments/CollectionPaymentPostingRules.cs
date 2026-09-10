using BE_Company.Sales.Services;

namespace BE_Company.Services.CollectionPayments
{
    /// <summary>
    /// 16:00 Asia/Baghdad posting gate for collection payments (mirrors DelegateWeb rules).
    /// Eligibility is based on CreatedAtUtc, never ReceivedAtUtc.
    /// </summary>
    public static class CollectionPaymentPostingRules
    {
        public static readonly TimeSpan PostingTimeBaghdad = TimeSpan.FromHours(16);
        public const double MinAmountIqd = 2000d;

        public static DateTime ComputeEligibleForPostingAtUtc(DateTime createdAtUtc)
        {
            var utc = DateTime.SpecifyKind(createdAtUtc.ToUniversalTime(), DateTimeKind.Utc);
            var baghdad = IraqTimeService.ToIraq(utc);
            if (baghdad.TimeOfDay < PostingTimeBaghdad)
            {
                var gate = baghdad.Date.Add(PostingTimeBaghdad);
                return IraqTimeService.ToUtcFromIraq(gate);
            }

            return utc;
        }

        public static bool IsEligible(DateTime eligibleForPostingAtUtc, DateTime utcNow) =>
            utcNow.ToUniversalTime() >= eligibleForPostingAtUtc.ToUniversalTime();
    }
}
