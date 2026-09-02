namespace BE_Company.Sales.Services
{
    public interface IIraqClock
    {
        DateTime UtcNow { get; }
    }

    public sealed class SystemIraqClock : IIraqClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public static class IraqTimeService
    {
        public static readonly TimeSpan BaghdadOffset = TimeSpan.FromHours(3);
        public static readonly TimeSpan CutoffTime = TimeSpan.FromHours(3);

        public static DateTime ToIraq(DateTime utc) =>
            DateTime.SpecifyKind(utc.ToUniversalTime().Add(BaghdadOffset), DateTimeKind.Unspecified);

        public static DateTime ToUtcFromIraq(DateTime iraqLocal) =>
            DateTime.SpecifyKind(iraqLocal.Add(-BaghdadOffset), DateTimeKind.Utc);

        public static DateTime IraqNow(IIraqClock clock) => ToIraq(clock.UtcNow);

        public static DateTime BusinessDateIraq(DateTime iraqLocal)
        {
            var date = iraqLocal.Date;
            if (iraqLocal.TimeOfDay < CutoffTime)
            {
                date = date.AddDays(-1);
            }

            return date;
        }

        public static DateTime CutoffIraq(DateTime iraqLocal) =>
            BusinessDateIraq(iraqLocal).AddDays(1).Add(CutoffTime);

        public static DateTime CutoffUtc(DateTime utcNow)
        {
            var iraq = ToIraq(utcNow);
            return ToUtcFromIraq(CutoffIraq(iraq));
        }

        public static bool IsExpired(DateTime cutoffAtUtc, DateTime utcNow) =>
            utcNow >= cutoffAtUtc;
    }
}
