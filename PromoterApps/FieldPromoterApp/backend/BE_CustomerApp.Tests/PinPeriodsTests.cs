using BE_CustomerApp.Domain.Work;

namespace BE_CustomerApp.Tests;

public class PinPeriodsTests
{
    private static readonly DateTime T0 = new(2026, 11, 14, 5, 0, 0, DateTimeKind.Utc);

    private static PinPeriods.Result Run(params double[] minutes) =>
        PinPeriods.Compute(minutes.Select((m, i) => new PinPeriods.Pin(i + 1, T0.AddMinutes(m))));

    [Fact]
    public void No_pins_no_work() => Assert.Equal(0, Run().WorkSeconds);

    [Fact]
    public void A_single_pin_starts_and_ends_a_zero_length_period()
    {
        var r = Run(0);
        Assert.Single(r.Periods);
        Assert.True(r.Pins[0].StartsPeriod && r.Pins[0].EndsPeriod);
        Assert.Equal(0, r.WorkSeconds);
    }

    [Fact]
    public void Gaps_of_15_minutes_or_more_split_periods_and_are_not_counted()
    {
        var r = Run(0, 5, 14.5, 29.5, 35, 50);
        // 0–14.5 | gap 15 | 29.5–35 | gap 15 | 50
        Assert.Equal(3, r.Periods.Count);
        Assert.Equal(new[] { 0, 0, 0, 1, 1, 2 }, r.Pins.Select(p => p.PeriodIndex));
        Assert.Equal(1200, r.WorkSeconds); // 14.5 + 5.5 minutes
        Assert.Equal(900, r.Pins[3].BreakSecondsBefore);
        Assert.True(r.Pins[2].EndsPeriod);
        Assert.Null(r.Pins[3].SecondsOnShop);
    }

    [Fact]
    public void Pins_under_3_minutes_after_the_previous_are_quick()
    {
        var r = Run(0, 2.99, 6, 9);
        Assert.Equal(new[] { false, true, false, false }, r.Pins.Select(p => p.Quick));
        Assert.Equal(1, r.QuickPins);
        Assert.Equal(9, r.WorkMinutes);
    }

    [Fact]
    public void Order_of_input_does_not_matter()
    {
        var r = PinPeriods.Compute([new PinPeriods.Pin(2, T0.AddMinutes(10)), new PinPeriods.Pin(1, T0)]);
        Assert.Equal(new long[] { 1, 2 }, r.Pins.Select(p => p.Id));
        Assert.Equal(600, r.Pins[1].SecondsOnShop);
    }
}
