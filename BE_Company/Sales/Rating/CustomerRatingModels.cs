namespace BE_Company.Sales.Rating;

public enum CustomerRatingLevel
{
    Excellent = 1,
    Good = 2,
    Weak = 3,
    /// <summary>متلكئ — 180 إلى أقل من 200 يوم (أو معدل يوحي بذلك).</summary>
    Rejected = 4,
    /// <summary>قانونية — legacy enum value; calculator never assigns it (IsLegal ignored).</summary>
    Legal = 5,
    /// <summary>سيء — 200 يوم فأكثر (أو معدل يوحي بذلك). النقاط -10.</summary>
    Bad = 6
}

public enum ListRiskIndicator
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3
}

public static class CustomerRatingLabels
{
    public const string Excellent = "ممتاز";
    public const string Good = "جيد";
    public const string Weak = "ضعيف";
    /// <summary>Was «مرفوض»; production label is متلكئ (180–&lt;200 days).</summary>
    public const string Rejected = "متلكئ";
    public const string Legal = "قانونية";
    public const string Bad = "سيء";

    public static string FromLevel(CustomerRatingLevel level) => level switch
    {
        CustomerRatingLevel.Excellent => Excellent,
        CustomerRatingLevel.Good => Good,
        CustomerRatingLevel.Weak => Weak,
        CustomerRatingLevel.Rejected => Rejected,
        CustomerRatingLevel.Legal => Legal,
        CustomerRatingLevel.Bad => Bad,
        _ => Weak
    };
}

public sealed record CustomerRatingFacts(
    int CustomerId,
    int? DelegateId,
    bool IsLegal,
    bool IsFakeSale,
    DateTime? DateSaleDevice,
    DateTime? LastPaymentDate,
    double AmountTotalSales,
    double ReceiptsTotal,
    double AmountRemaining,
    int ReceiptCount = 0,
    /// <summary>
    /// Main-accountant rating equation (default 1). Scales receipts for open-account rate
    /// and compresses settled days — ledger AmountPaid / ReceiptsTotal stay unchanged.
    /// </summary>
    double RatingEquationFactor = 1.0);

public sealed record CustomerRatingResult(
    int CustomerId,
    int? DelegateId,
    CustomerRatingLevel Level,
    string RatingLabel,
    int Score,
    string Reason,
    bool IsLegal,
    bool IsSettled,
    int? DaysToSettle,
    int? DaysSinceSale,
    double? PaymentRate,
    DateTime? DateSaleDevice,
    int ReceiptCount = 0);

public static class CustomerRatingCalculator
{
    // days ≈ 1000 / rate  (constant daily payment model)
    public const double ExcellentRate = 1000.0 / 150.0; // ~6.6667 → under 150 days
    public const double GoodRate = 6.25;              // 160 days
    public const double WeakRate = 1000.0 / 180.0;    // ~5.5556 → under 180 days
    public const double LaggingMinRate = 1000.0 / 200.0; // 5.0 → under 200 days

    public const int ExcellentMaxDays = 150;
    public const int GoodMaxDays = 160;
    public const int WeakMaxDaysExclusive = 180;
    public const int LaggingMaxDaysExclusive = 200; // متلكئ: 180..199 ; سيء: 200+

    public static double NormalizeEquationFactor(double factor) =>
        factor > 0 && !double.IsNaN(factor) && !double.IsInfinity(factor) ? factor : 1.0;

    public static CustomerRatingResult Evaluate(
        CustomerRatingFacts facts,
        DateTime asOfDate,
        bool usePhantomSaleDate = false)
    {
        // IsLegal is informational only — it does not affect payment-speed rating.
        var equation = NormalizeEquationFactor(facts.RatingEquationFactor);
        var equationNote = Math.Abs(equation - 1.0) > 0.0001
            ? $" (معادلة التقييم ×{equation:0.##})"
            : string.Empty;

        if (facts.AmountRemaining < 0)
        {
            return Build(facts, CustomerRatingLevel.Weak, 0, "الباقي سالب ويحتاج مراجعة",
                isSettled: false, null, null, null);
        }

        if (facts.AmountTotalSales <= 0 || facts.DateSaleDevice is null)
        {
            return Build(facts, CustomerRatingLevel.Weak, 0, "لا توجد بيانات بيع كافية للتقييم",
                isSettled: facts.AmountRemaining == 0, null, null, null);
        }

        // Ledger DateSaleDevice stays real; phantom adjusts math-only sale date.
        var saleDate = usePhantomSaleDate
            ? (PhantomSaleDate.Adjust(facts.DateSaleDevice) ?? facts.DateSaleDevice.Value.Date)
            : facts.DateSaleDevice.Value.Date;
        var asOf = asOfDate.Date;

        if (facts.AmountRemaining == 0)
        {
            if (facts.LastPaymentDate is null)
            {
                return Build(facts, CustomerRatingLevel.Weak, 0, "بيانات التسديد غير مكتملة",
                    isSettled: true, null, null, null);
            }

            var daysToSettle = DiffDaysInclusive(saleDate, facts.LastPaymentDate.Value.Date);
            if (daysToSettle <= 0)
            {
                return Build(facts, CustomerRatingLevel.Weak, 0, "بيانات التسديد غير مكتملة",
                    isSettled: true, daysToSettle, null, null);
            }

            // Virtual only: compress calendar days by equation (analogous to boosting receipts).
            var effectiveDays = Math.Max(1, (int)Math.Ceiling(daysToSettle / equation));
            var (level, score) = ClassifySettledDays(effectiveDays);
            var reason = equationNote.Length == 0
                ? $"{CustomerRatingLabels.FromLevel(level)} - أغلق الحساب خلال {daysToSettle} يوم"
                : $"{CustomerRatingLabels.FromLevel(level)} - أغلق خلال {daysToSettle} يوم → فعلي للتقييم {effectiveDays} يوم{equationNote}";
            return Build(facts, level, score, reason, isSettled: true, daysToSettle, null, null);
        }

        var daysSinceSale = DiffDaysInclusive(saleDate, asOf);
        if (daysSinceSale <= 0)
        {
            return Build(facts, CustomerRatingLevel.Weak, 0, "بيانات التسديد غير مكتملة",
                isSettled: false, null, daysSinceSale, null);
        }

        // Virtual receipts for rating only — real ledger ReceiptsTotal / AmountRemaining unchanged.
        var effectiveReceipts = facts.ReceiptsTotal * equation;
        var rate = effectiveReceipts * 1000.0 / (facts.AmountTotalSales * daysSinceSale);
        var (activeLevel, activeScore) = ClassifyActiveRate(rate);
        var projectedDays = rate > 0 ? (1000.0 / rate) : double.PositiveInfinity;
        var activeReason =
            $"{CustomerRatingLabels.FromLevel(activeLevel)} - معدل التسديد {rate:0.##}% (≈{projectedDays:0.#} يوم للتصفير){equationNote}";
        return Build(facts, activeLevel, activeScore, activeReason,
            isSettled: false, null, daysSinceSale, rate);
    }

    public static int DiffDaysInclusive(DateTime from, DateTime to) =>
        (to.Date - from.Date).Days + 1;

    public static (CustomerRatingLevel Level, int Score) ClassifySettledDays(int days)
    {
        if (days <= ExcellentMaxDays) return (CustomerRatingLevel.Excellent, 10);
        if (days <= GoodMaxDays) return (CustomerRatingLevel.Good, 5);
        if (days < WeakMaxDaysExclusive) return (CustomerRatingLevel.Weak, 0);
        if (days < LaggingMaxDaysExclusive) return (CustomerRatingLevel.Rejected, -5); // متلكئ 180–199
        return (CustomerRatingLevel.Bad, -10); // سيء 200+
    }

    public static (CustomerRatingLevel Level, int Score) ClassifyActiveRate(double rate)
    {
        if (rate >= ExcellentRate) return (CustomerRatingLevel.Excellent, 10);
        if (rate >= GoodRate) return (CustomerRatingLevel.Good, 5);
        if (rate >= WeakRate) return (CustomerRatingLevel.Weak, 0);
        if (rate >= LaggingMinRate) return (CustomerRatingLevel.Rejected, -5); // متلكئ
        return (CustomerRatingLevel.Bad, -10); // سيء — يصفّر بـ200 يوم فأكثر
    }

    /// <summary>
    /// Parallel evaluate for large cohorts (branch / 100k-customer production loads).
    /// Sequential below threshold to avoid thread-pool overhead on small lists.
    /// </summary>
    public static IReadOnlyList<CustomerRatingResult> EvaluateMany(
        IReadOnlyList<CustomerRatingFacts> facts,
        DateTime asOfDate,
        int parallelThreshold = 2_000,
        bool usePhantomSaleDate = false)
    {
        if (facts.Count == 0) return Array.Empty<CustomerRatingResult>();
        if (facts.Count < parallelThreshold)
        {
            var sequential = new CustomerRatingResult[facts.Count];
            for (var i = 0; i < facts.Count; i++)
                sequential[i] = Evaluate(facts[i], asOfDate, usePhantomSaleDate);
            return sequential;
        }

        var results = new CustomerRatingResult[facts.Count];
        Parallel.For(0, facts.Count, i => results[i] = Evaluate(facts[i], asOfDate, usePhantomSaleDate));
        return results;
    }

    private static CustomerRatingResult Build(
        CustomerRatingFacts facts,
        CustomerRatingLevel level,
        int score,
        string reason,
        bool isSettled,
        int? daysToSettle,
        int? daysSinceSale,
        double? paymentRate) =>
        new(
            facts.CustomerId,
            facts.DelegateId,
            level,
            CustomerRatingLabels.FromLevel(level),
            score,
            reason,
            facts.IsLegal,
            isSettled,
            daysToSettle,
            daysSinceSale,
            paymentRate,
            facts.DateSaleDevice,
            facts.ReceiptCount);
}
