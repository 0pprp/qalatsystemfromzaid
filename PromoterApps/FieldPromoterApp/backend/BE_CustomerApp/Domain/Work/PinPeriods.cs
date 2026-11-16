namespace BE_CustomerApp.Domain.Work;

/// <summary>
/// «فترات العمل»: an agent's working time is measured only from the times shops were pinned (the agent never sees this).
/// <list type="bullet">
/// <item>The time spent on a shop is the time since the previous pin of the same period.</item>
/// <item>A pin less than <see cref="QuickGap"/> after the previous one is flagged (too quick to be a real visit).</item>
/// <item>When <see cref="BreakGap"/> or more passes without a pin, the previous pin ends a period and the next pin starts a new one;
/// the gap is not work time.</item>
/// <item>A period lasts from its first pin to its last; the day's work time is the sum of the periods.</item>
/// </list>
/// </summary>
public static class PinPeriods
{
    public static readonly TimeSpan QuickGap = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan BreakGap = TimeSpan.FromMinutes(15);

    public sealed record Pin(long Id, DateTime At);

    /// <param name="SecondsOnShop">Seconds since the previous pin of the same period (null for a period's first pin).</param>
    /// <param name="BreakSecondsBefore">For a period's first pin after an earlier period: the break since that period ended.</param>
    public sealed record PinResult(long Id, DateTime At, int PeriodIndex, bool StartsPeriod, bool EndsPeriod, int? SecondsOnShop, bool Quick,
        int? BreakSecondsBefore);

    public sealed record Period(int Index, DateTime StartAt, DateTime EndAt, int Seconds, int Pins, int QuickPins);

    public sealed record Result(IReadOnlyList<PinResult> Pins, IReadOnlyList<Period> Periods, int WorkSeconds, int QuickPins)
    {
        public int WorkMinutes => WorkSeconds / 60;
    }

    public static Result Compute(IEnumerable<Pin> pins)
    {
        var ordered = pins.OrderBy(p => p.At).ThenBy(p => p.Id).ToList();
        var results = new List<PinResult>(ordered.Count);
        var periodIndex = 0;
        for (var i = 0; i < ordered.Count; i++)
        {
            var p = ordered[i];
            TimeSpan? gap = i == 0 ? null : p.At - ordered[i - 1].At;
            var starts = gap is null || gap >= BreakGap;
            if (starts && i > 0) periodIndex++;
            var ends = i == ordered.Count - 1 || ordered[i + 1].At - p.At >= BreakGap;
            int? onShop = starts ? null : (int)gap!.Value.TotalSeconds;
            results.Add(new PinResult(p.Id, p.At, periodIndex, starts, ends, onShop, onShop is { } s && s < QuickGap.TotalSeconds,
                starts && gap is { } g ? (int)g.TotalSeconds : null));
        }
        var periods = results.GroupBy(r => r.PeriodIndex).Select(g =>
        {
            var start = g.First().At;
            var end = g.Last().At;
            return new Period(g.Key, start, end, (int)(end - start).TotalSeconds, g.Count(), g.Count(x => x.Quick));
        }).ToList();
        return new Result(results, periods, periods.Sum(p => p.Seconds), results.Count(r => r.Quick));
    }
}
