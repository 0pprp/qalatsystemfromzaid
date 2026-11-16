using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class ListDaily30dTests
    {
        [Fact]
        public void Daily30d_HasThirtyDays_OldestFirst_EmptyDaysZero()
        {
            var today = new DateTime(2026, 11, 15);
            var rows = new[]
            {
                (new DateTime(2026, 11, 15), 250_000d, 5),
                (new DateTime(2026, 10, 17), 100_000d, 2),
            };

            var days = CustomerShopLocationService.BuildDaily30d(rows, today);

            Assert.Equal(30, days.Count);
            Assert.Equal(new DateTime(2026, 10, 17), days[0].Date);
            Assert.Equal(today, days[^1].Date);
            Assert.Equal(100_000d, days[0].Amount);
            Assert.Equal(2, days[0].Payments);
            Assert.Equal(250_000d, days[^1].Amount);
            Assert.Equal(0d, days[1].Amount);
            Assert.Equal(0, days[1].Payments);
            Assert.Equal(350_000d, days.Sum(d => d.Amount));
        }

        [Fact]
        public void Daily30d_IgnoresTimeOfDay_AndMergesSameDay()
        {
            var today = new DateTime(2026, 11, 15, 13, 40, 0);
            var rows = new[]
            {
                (new DateTime(2026, 11, 14, 9, 0, 0), 10d, 1),
                (new DateTime(2026, 11, 14, 0, 0, 0), 5d, 1),
            };

            var days = CustomerShopLocationService.BuildDaily30d(rows, today);

            Assert.Equal(new DateTime(2026, 11, 15), days[^1].Date);
            Assert.Equal(15d, days[^2].Amount);
            Assert.Equal(2, days[^2].Payments);
        }
    }
}
