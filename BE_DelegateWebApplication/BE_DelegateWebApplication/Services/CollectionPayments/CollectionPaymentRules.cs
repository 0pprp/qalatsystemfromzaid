namespace BE_DelegateWebApplication.Services.CollectionPayments
{
    /// <summary>
    /// Shared intake / posting eligibility rules for collection delegate payments.
    /// Business day clock: Asia/Baghdad (UTC+3). Posting gate: 16:00 Baghdad.
    /// </summary>
    public static class CollectionPaymentRules
    {
        public const double MinAmountIqd = 2000d;
        public const string MinAmountMessage = "أقل مبلغ مسموح للتسديد هو 2,000 د.ع";
        public static readonly TimeSpan BaghdadOffset = TimeSpan.FromHours(3);
        public static readonly TimeSpan PostingTimeBaghdad = TimeSpan.FromHours(16);
        public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromHours(2);
        public static readonly TimeSpan MaxPastSkew = TimeSpan.FromDays(30);

        public static DateTime ToBaghdad(DateTime utc) =>
            DateTime.SpecifyKind(utc.ToUniversalTime().Add(BaghdadOffset), DateTimeKind.Unspecified);

        public static DateTime ToUtcFromBaghdad(DateTime baghdadLocal) =>
            DateTime.SpecifyKind(baghdadLocal.Add(-BaghdadOffset), DateTimeKind.Utc);

        public static bool TryValidateAmount(double? amount, out string? error)
        {
            if (amount is null || amount <= 0)
            {
                error = "لا يمكن ترك المبلغ فارغًا أو يساوي صفر";
                return false;
            }

            if (amount < MinAmountIqd)
            {
                error = MinAmountMessage;
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Uses CreatedAtUtc (business event time), never ReceivedAtUtc, for the 16:00 rule.
        /// Before 16:00 Baghdad → eligible at that day's 16:00.
        /// At/after 16:00 → eligible immediately at CreatedAtUtc.
        /// </summary>
        public static DateTime ComputeEligibleForPostingAtUtc(DateTime createdAtUtc)
        {
            var utc = DateTime.SpecifyKind(createdAtUtc.ToUniversalTime(), DateTimeKind.Utc);
            var baghdad = ToBaghdad(utc);
            if (baghdad.TimeOfDay < PostingTimeBaghdad)
            {
                var gate = baghdad.Date.Add(PostingTimeBaghdad);
                return ToUtcFromBaghdad(gate);
            }

            return utc;
        }

        public static bool IsEligibleForPosting(DateTime eligibleForPostingAtUtc, DateTime utcNow) =>
            utcNow.ToUniversalTime() >= eligibleForPostingAtUtc.ToUniversalTime();

        public static bool TryNormalizeCreatedAtUtc(DateTime? clientCreatedAtUtc, DateTime serverUtcNow, out DateTime createdAtUtc, out string? error)
        {
            if (clientCreatedAtUtc is null)
            {
                createdAtUtc = DateTime.SpecifyKind(serverUtcNow, DateTimeKind.Utc);
                error = null;
                return true;
            }

            createdAtUtc = DateTime.SpecifyKind(clientCreatedAtUtc.Value.ToUniversalTime(), DateTimeKind.Utc);
            if (createdAtUtc > serverUtcNow.Add(MaxFutureSkew))
            {
                error = "وقت إنشاء التسديد غير صالح (مستقبلي)";
                return false;
            }

            if (createdAtUtc < serverUtcNow.Add(-MaxPastSkew))
            {
                error = "وقت إنشاء التسديد قديم جدًا";
                return false;
            }

            error = null;
            return true;
        }

        public static bool IsValidClientPaymentId(string? clientPaymentId) =>
            !string.IsNullOrWhiteSpace(clientPaymentId)
            && Guid.TryParse(clientPaymentId.Trim(), out _);
    }
}
