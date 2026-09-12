namespace BE_Company.Sales.Rating;

public enum CustomerRatingLevel
{
    Excellent = 1,
    Good = 2,
    Weak = 3,
    Rejected = 4,
    Legal = 5
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
    public const string Rejected = "مرفوض";
    public const string Legal = "قانونية";

    public static string FromLevel(CustomerRatingLevel level) => level switch
    {
        CustomerRatingLevel.Excellent => Excellent,
        CustomerRatingLevel.Good => Good,
        CustomerRatingLevel.Weak => Weak,
        CustomerRatingLevel.Rejected => Rejected,
        CustomerRatingLevel.Legal => Legal,
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
    double AmountRemaining);

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
    DateTime? DateSaleDevice);

public static class CustomerRatingCalculator
{
    public const double ExcellentRate = 7.1;
    public const double GoodRate = 6.25;
    public const double WeakRate = 5.5;

    public static CustomerRatingResult Evaluate(CustomerRatingFacts facts, DateTime asOfDate)
    {
        if (facts.IsLegal)
        {
            return Build(facts, CustomerRatingLevel.Legal, -10, "قانونية",
                isSettled: facts.AmountRemaining == 0,
                daysToSettle: null,
                daysSinceSale: null,
                paymentRate: null);
        }

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

        var saleDate = facts.DateSaleDevice.Value.Date;
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

            var (level, score) = ClassifySettledDays(daysToSettle);
            var reason = $"{CustomerRatingLabels.FromLevel(level)} - أغلق الحساب خلال {daysToSettle} يوم";
            return Build(facts, level, score, reason, isSettled: true, daysToSettle, null, null);
        }

        var daysSinceSale = DiffDaysInclusive(saleDate, asOf);
        if (daysSinceSale <= 0)
        {
            return Build(facts, CustomerRatingLevel.Weak, 0, "بيانات التسديد غير مكتملة",
                isSettled: false, null, daysSinceSale, null);
        }

        var rate = facts.ReceiptsTotal * 1000.0 / (facts.AmountTotalSales * daysSinceSale);
        var (activeLevel, activeScore) = ClassifyActiveRate(rate);
        var activeReason = $"{CustomerRatingLabels.FromLevel(activeLevel)} - معدل التسديد {rate:0.##}%";
        return Build(facts, activeLevel, activeScore, activeReason,
            isSettled: false, null, daysSinceSale, rate);
    }

    public static int DiffDaysInclusive(DateTime from, DateTime to) =>
        (to.Date - from.Date).Days + 1;

    public static (CustomerRatingLevel Level, int Score) ClassifySettledDays(int days)
    {
        if (days <= 140) return (CustomerRatingLevel.Excellent, 10);
        if (days <= 160) return (CustomerRatingLevel.Good, 5);
        if (days < 180) return (CustomerRatingLevel.Weak, 0);
        return (CustomerRatingLevel.Rejected, -5);
    }

    public static (CustomerRatingLevel Level, int Score) ClassifyActiveRate(double rate)
    {
        if (rate >= ExcellentRate) return (CustomerRatingLevel.Excellent, 10);
        if (rate >= GoodRate) return (CustomerRatingLevel.Good, 5);
        if (rate >= WeakRate) return (CustomerRatingLevel.Weak, 0);
        return (CustomerRatingLevel.Rejected, -5);
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
            facts.DateSaleDevice);
}
