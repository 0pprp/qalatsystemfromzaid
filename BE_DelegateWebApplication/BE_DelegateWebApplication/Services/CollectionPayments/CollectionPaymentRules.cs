namespace BE_DelegateWebApplication.Services.CollectionPayments
{
    /// <summary>
    /// Shared intake / posting eligibility rules for collection delegate payments.
    /// Business day clock: Asia/Baghdad (UTC+3).
    /// Posting is immediate on accept (no 16:00 Baghdad gate).
    /// </summary>
    public static class CollectionPaymentRules
    {
        public const double MinAmountIqd = 2000d;
        public const string MinAmountMessage = "أقل مبلغ مسموح للتسديد هو 2,000 د.ع";
        public static readonly TimeSpan BaghdadOffset = TimeSpan.FromHours(3);
        /// <summary>Legacy constant — gate removed; kept for callers/tests that still reference it.</summary>
        public static readonly TimeSpan PostingTimeBaghdad = TimeSpan.FromHours(16);
        /// <summary>Deploy verify marker — posting eligibility is immediate at create.</summary>
        public const string EligibilityMode = "ImmediateAtCreatedUtc";
        public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromHours(2);
        public static readonly TimeSpan MaxPastSkew = TimeSpan.FromDays(30);

        public static DateTime ToBaghdad(DateTime utc) =>
            DateTime.SpecifyKind(utc.ToUniversalTime().Add(BaghdadOffset), DateTimeKind.Unspecified);

        public static DateTime ToUtcFromBaghdad(DateTime baghdadLocal) =>
            DateTime.SpecifyKind(baghdadLocal.Add(-BaghdadOffset), DateTimeKind.Utc);

        /// <summary>
        /// Baghdad collection business date (03:00 boundary), date-only Unspecified.
        /// </summary>
        public static DateTime BaghdadBusinessDate(DateTime utc)
        {
            var bg = ToBaghdad(utc);
            var day = new DateTime(bg.Year, bg.Month, bg.Day);
            if (bg.Hour < 3)
            {
                day = day.AddDays(-1);
            }

            return day;
        }

        /// <summary>Same customer + same IQD amount on same Baghdad business day (anti double-collect).</summary>
        public const string SameDaySameAmountMessage =
            "تم تسجيل تسديد بنفس المبلغ لهذا الزبون اليوم — مرفوض لمنع التكرار";

        /// <summary>
        /// Returned with Success=true when a same-day same-amount payment already exists.
        /// Device must clear the local queue row (not mark PermanentFailure).
        /// </summary>
        public const string SameDaySameAmountAcknowledgedMessage =
            "التسديد مسجل مسبقاً بنفس المبلغ لهذا الزبون اليوم";

        public const double AmountMatchToleranceIqd = 1d;

        public static bool IsSameAmountIqd(double a, double b) =>
            Math.Abs(a - b) < AmountMatchToleranceIqd;

        public static bool IsSameBusinessDay(DateTime utcA, DateTime utcB) =>
            BaghdadBusinessDate(utcA) == BaghdadBusinessDate(utcB);

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
        /// Eligible immediately at CreatedAtUtc (16:00 Baghdad posting gate removed).
        /// </summary>
        public static DateTime ComputeEligibleForPostingAtUtc(DateTime createdAtUtc)
        {
            return DateTime.SpecifyKind(createdAtUtc.ToUniversalTime(), DateTimeKind.Utc);
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
