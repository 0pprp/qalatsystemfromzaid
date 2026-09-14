using BE_DelegateWebApplication.Services.CollectionPayments;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class DelegateBusinessDateRulesTests
{
    /// <summary>Mirrors Flutter IraqDate.businessDate (Baghdad UTC+3, roll at 03:00).</summary>
    public static DateOnly BusinessDate(DateTime utcNow)
    {
        var baghdad = utcNow.ToUniversalTime().AddHours(3);
        var localDay = DateOnly.FromDateTime(baghdad);
        if (baghdad.Hour < 3)
        {
            return localDay.AddDays(-1);
        }

        return localDay;
    }

    [Theory]
    [InlineData(2026, 9, 16, 23, 59, "2026-09-16")] // 02:59 Baghdad Thu → Wed
    [InlineData(2026, 9, 17, 0, 0, "2026-09-17")] // 03:00 Baghdad
    [InlineData(2026, 9, 17, 0, 1, "2026-09-17")]
    [InlineData(2026, 9, 17, 20, 59, "2026-09-17")] // 23:59 Baghdad
    public void BusinessDate_Baghdad_03_Boundary(int y, int m, int d, int h, int min, string expected)
    {
        var utc = new DateTime(y, m, d, h, min, 0, DateTimeKind.Utc);
        Assert.Equal(expected, BusinessDate(utc).ToString("yyyy-MM-dd"));
    }

    [Fact]
    public void CreatedAtUtc_Preserved_As_PaymentDate_Contract()
    {
        // Offline Wed payment uploaded Thu still uses client CreatedAtUtc (not ReceivedAtUtc).
        var createdWed = new DateTime(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(CollectionPaymentRules.TryNormalizeCreatedAtUtc(
            createdWed,
            new DateTime(2026, 9, 17, 4, 0, 0, DateTimeKind.Utc),
            out var normalized,
            out _));
        Assert.Equal(createdWed, normalized);
    }
}
