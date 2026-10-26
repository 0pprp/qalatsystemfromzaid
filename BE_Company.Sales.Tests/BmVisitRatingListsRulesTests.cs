using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// BM visit entry ratings must use the same list visibility as المراقب
/// «متابعة المندوبين» — not every DelegateState=1 row.
/// </summary>
public class BmVisitRatingListsRulesTests
{
    [Fact]
    public void SelectForVisitRating_KeepsOnlyMonitorBoardActiveLists()
    {
        var asOf = new DateTime(2026, 10, 25);
        var rows = new (int Id, string Name, DateTime? LastPayment)[]
        {
            (1, "نشطة", asOf.AddDays(-10)),
            (2, "قديمة", asOf.AddDays(-50)),
            (3, "بدون تسديد", null),
            (4, "حد الأربعين", asOf.AddDays(-40)),
        };

        var selected = BmVisitRatingListsRules.SelectForVisitRating(rows, asOf);

        Assert.Equal(new[] { 1, 4 }, selected.Select(x => x.Id).OrderBy(x => x));
    }

    [Fact]
    public void SelectForVisitRating_EmptyInput_ReturnsEmpty()
    {
        Assert.Empty(BmVisitRatingListsRules.SelectForVisitRating([], DateTime.UtcNow));
    }
}
