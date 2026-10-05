namespace BE_DelegateWebApplication.Services.FollowerRating;

/// <summary>Mirrors BE_Company.Sales.Rating.CustomerRatingCalculator for follower list stats.</summary>
public enum FollowerCustomerRatingLevel
{
    Excellent = 1,
    Good = 2,
    Weak = 3,
    /// <summary>متلكئ — 180 إلى أقل من 200 يوم.</summary>
    Rejected = 4,
    Legal = 5,
    /// <summary>سيء — 200 يوم فأكثر. النقاط -10.</summary>
    Bad = 6
}

public static class FollowerCustomerRatingLabels
{
    public const string Excellent = "ممتاز";
    public const string Good = "جيد";
    public const string Weak = "ضعيف";
    public const string Rejected = "متلكئ";
    public const string Legal = "قانونية";
    public const string Bad = "سيء";

    public static string FromLevel(FollowerCustomerRatingLevel level) => level switch
    {
        FollowerCustomerRatingLevel.Excellent => Excellent,
        FollowerCustomerRatingLevel.Good => Good,
        FollowerCustomerRatingLevel.Weak => Weak,
        FollowerCustomerRatingLevel.Rejected => Rejected,
        FollowerCustomerRatingLevel.Legal => Legal,
        FollowerCustomerRatingLevel.Bad => Bad,
        _ => Weak
    };

    public static bool TryParseLevelKey(string? key, out FollowerCustomerRatingLevel level)
    {
        level = FollowerCustomerRatingLevel.Weak;
        var k = (key ?? "").Trim().ToLowerInvariant();
        if (k is "excellent" or "ممتاز") { level = FollowerCustomerRatingLevel.Excellent; return true; }
        if (k is "good" or "جيد") { level = FollowerCustomerRatingLevel.Good; return true; }
        if (k is "weak" or "ضعيف") { level = FollowerCustomerRatingLevel.Weak; return true; }
        if (k is "rejected" or "مرفوض" or "متلكئ") { level = FollowerCustomerRatingLevel.Rejected; return true; }
        if (k is "legal" or "قانونية") { level = FollowerCustomerRatingLevel.Legal; return true; }
        if (k is "bad" or "سيء") { level = FollowerCustomerRatingLevel.Bad; return true; }
        return false;
    }

    public static string ToLevelKey(FollowerCustomerRatingLevel level) => level switch
    {
        FollowerCustomerRatingLevel.Excellent => "excellent",
        FollowerCustomerRatingLevel.Good => "good",
        FollowerCustomerRatingLevel.Weak => "weak",
        FollowerCustomerRatingLevel.Rejected => "rejected",
        FollowerCustomerRatingLevel.Legal => "legal",
        FollowerCustomerRatingLevel.Bad => "bad",
        _ => "weak"
    };
}

public sealed record FollowerCustomerRatingFacts(
    int CustomerId,
    int? DelegateId,
    string? CustomerName,
    string? PhoneNumber,
    bool IsLegal,
    DateTime? DateSaleDevice,
    DateTime? LastPaymentDate,
    double AmountTotalSales,
    double ReceiptsTotal,
    double AmountRemaining,
    double AmountDaySales);

public sealed record FollowerCustomerRatingResult(
    int CustomerId,
    int? DelegateId,
    string? CustomerName,
    string? PhoneNumber,
    FollowerCustomerRatingLevel Level,
    string RatingLabel,
    int Score,
    DateTime? DateSaleDevice,
    double AmountTotalSales,
    double AmountDaySales,
    double AmountRemaining);

public static class FollowerCustomerRatingCalculator
{
    // Keep in lockstep with BE_Company CustomerRatingCalculator.
    public const double ExcellentRate = 7.1;
    public const double GoodRate = 6.25;
    public const double WeakRate = 1000.0 / 180.0;
    public const double LaggingMinRate = 1000.0 / 200.0;

    public const int ExcellentMaxDays = 140;
    public const int GoodMaxDays = 160;
    public const int WeakMaxDaysExclusive = 180;
    public const int LaggingMaxDaysExclusive = 200;

    public static FollowerCustomerRatingResult Evaluate(
        FollowerCustomerRatingFacts facts,
        DateTime asOfDate,
        bool usePhantomSaleDate = true)
    {
        // IsLegal is informational only — it does not affect payment-speed rating.
        // Follower surfaces default to phantom sale-date math (التقييم الوهمي).

        if (facts.AmountRemaining < 0 || facts.AmountTotalSales <= 0 || facts.DateSaleDevice is null)
            return Build(facts, FollowerCustomerRatingLevel.Weak, 0);

        var saleDate = usePhantomSaleDate
            ? PhantomSaleDateAdjust(facts.DateSaleDevice.Value.Date)
            : facts.DateSaleDevice.Value.Date;
        var asOf = asOfDate.Date;

        if (facts.AmountRemaining == 0)
        {
            if (facts.LastPaymentDate is null)
                return Build(facts, FollowerCustomerRatingLevel.Weak, 0);

            var daysToSettle = DiffDaysInclusive(saleDate, facts.LastPaymentDate.Value.Date);
            if (daysToSettle <= 0)
                return Build(facts, FollowerCustomerRatingLevel.Weak, 0);

            var (level, score) = ClassifySettledDays(daysToSettle);
            return Build(facts, level, score);
        }

        var daysSinceSale = DiffDaysInclusive(saleDate, asOf);
        if (daysSinceSale <= 0)
            return Build(facts, FollowerCustomerRatingLevel.Weak, 0);

        var rate = facts.ReceiptsTotal * 1000.0 / (facts.AmountTotalSales * daysSinceSale);
        var (activeLevel, activeScore) = ClassifyActiveRate(rate);
        return Build(facts, activeLevel, activeScore);
    }

    /// <summary>Lockstep with BE_Company.Sales.Rating.PhantomSaleDate.</summary>
    private static DateTime PhantomSaleDateAdjust(DateTime d)
    {
        var floor = new DateTime(2026, 5, 1);
        var boostEnd = new DateTime(2026, 8, 31);
        if (d < floor) return floor;
        if (d <= boostEnd) return d.AddDays(30);
        return d;
    }

    public static int DiffDaysInclusive(DateTime from, DateTime to) =>
        (to.Date - from.Date).Days + 1;

    public static (FollowerCustomerRatingLevel Level, int Score) ClassifySettledDays(int days)
    {
        if (days <= ExcellentMaxDays) return (FollowerCustomerRatingLevel.Excellent, 10);
        if (days <= GoodMaxDays) return (FollowerCustomerRatingLevel.Good, 5);
        if (days < WeakMaxDaysExclusive) return (FollowerCustomerRatingLevel.Weak, 0);
        if (days < LaggingMaxDaysExclusive) return (FollowerCustomerRatingLevel.Rejected, -5);
        return (FollowerCustomerRatingLevel.Bad, -10);
    }

    public static (FollowerCustomerRatingLevel Level, int Score) ClassifyActiveRate(double rate)
    {
        if (rate >= ExcellentRate) return (FollowerCustomerRatingLevel.Excellent, 10);
        if (rate >= GoodRate) return (FollowerCustomerRatingLevel.Good, 5);
        if (rate >= WeakRate) return (FollowerCustomerRatingLevel.Weak, 0);
        if (rate >= LaggingMinRate) return (FollowerCustomerRatingLevel.Rejected, -5);
        return (FollowerCustomerRatingLevel.Bad, -10);
    }

    private static FollowerCustomerRatingResult Build(
        FollowerCustomerRatingFacts facts,
        FollowerCustomerRatingLevel level,
        int score) =>
        new(
            facts.CustomerId,
            facts.DelegateId,
            facts.CustomerName,
            facts.PhoneNumber,
            level,
            FollowerCustomerRatingLabels.FromLevel(level),
            score,
            facts.DateSaleDevice,
            facts.AmountTotalSales,
            facts.AmountDaySales,
            facts.AmountRemaining);
}

public sealed record FollowerListRatingAggregate(
    int CustomerCount,
    int ExcellentCount,
    int GoodCount,
    int WeakCount,
    int RejectedCount,
    int LegalCount,
    int BadCount,
    double AverageScore,
    string FinalRatingLabel);

public static class FollowerListRatingAggregator
{
    public static FollowerListRatingAggregate Aggregate(IEnumerable<FollowerCustomerRatingResult> ratings)
    {
        var list = ratings as IList<FollowerCustomerRatingResult> ?? ratings.ToList();
        var count = list.Count;
        if (count == 0)
            return new FollowerListRatingAggregate(0, 0, 0, 0, 0, 0, 0, 0, FollowerCustomerRatingLabels.Weak);

        var excellent = 0;
        var good = 0;
        var weak = 0;
        var rejected = 0;
        var bad = 0;
        var totalPoints = 0;
        for (var i = 0; i < list.Count; i++)
        {
            var r = list[i];
            totalPoints += r.Score;
            switch (r.Level)
            {
                case FollowerCustomerRatingLevel.Excellent: excellent++; break;
                case FollowerCustomerRatingLevel.Good: good++; break;
                case FollowerCustomerRatingLevel.Weak: weak++; break;
                case FollowerCustomerRatingLevel.Rejected: rejected++; break;
                case FollowerCustomerRatingLevel.Bad: bad++; break;
            }
        }

        var average = (double)totalPoints / count;
        return new FollowerListRatingAggregate(
            count, excellent, good, weak, rejected, LegalCount: 0, bad,
            Math.Round(average, 2),
            ClassifyAverage(average));
    }

    public static string ClassifyAverage(double averageScore)
    {
        if (averageScore >= 7) return FollowerCustomerRatingLabels.Excellent;
        if (averageScore >= 3) return FollowerCustomerRatingLabels.Good;
        if (averageScore >= 0) return FollowerCustomerRatingLabels.Weak;
        if (averageScore > -7.5) return FollowerCustomerRatingLabels.Rejected;
        return FollowerCustomerRatingLabels.Bad;
    }
}

/// <summary>WeakWeek inverted: active when week paid % of sale &gt; threshold.</summary>
public static class FollowerActiveCustomerRules
{
    public const double ActiveMinPercentExclusive = 3.8;

    public static bool IsActive(double weekPaid, double amountTotalSales) =>
        amountTotalSales > 0
        && (weekPaid * 100.0) / amountTotalSales > ActiveMinPercentExclusive;
}
