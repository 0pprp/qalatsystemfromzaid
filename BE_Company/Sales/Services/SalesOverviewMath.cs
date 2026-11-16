using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public static class SalesOverviewMath
    {
        public static decimal? GrowthPercent(decimal current, decimal previous)
        {
            if (previous == 0m)
            {
                return current == 0m ? 0m : null;
            }

            return Math.Round((current - previous) / previous * 100m, 1, MidpointRounding.AwayFromZero);
        }

        public static decimal Average(decimal total, int count) =>
            count <= 0 ? 0m : Math.Round(total / count, 0, MidpointRounding.AwayFromZero);

        /// <summary>Sale score is 0–10. A score of 7 or more is the high band used when the sale is entered.</summary>
        public const int HighScoreMin = 7;

        public static decimal? AverageScore(decimal sum, int count) =>
            count <= 0 ? null : Math.Round(sum / count, 1, MidpointRounding.AwayFromZero);

        public static decimal ConversionPercent(int converted, int created) =>
            created <= 0 ? 0m : Math.Round(converted * 100m / created, 1, MidpointRounding.AwayFromZero);

        /// <summary>Share of the month sale price that has been collected. Never above 100.</summary>
        public static decimal CollectedPercent(decimal collected, decimal salePrice)
        {
            if (salePrice <= 0m || collected <= 0m)
            {
                return 0m;
            }

            var capped = collected >= salePrice ? salePrice : collected;
            return Math.Round(capped / salePrice * 100m, 1, MidpointRounding.AwayFromZero);
        }

        public static void NormalizePeriod(
            ref int year,
            ref int month,
            DateTime iraqNow,
            out DateTime from,
            out DateTime toExclusive,
            out DateTime previousFrom,
            out int daysInMonth)
        {
            if (year < 2000 || year > 2100)
            {
                year = iraqNow.Year;
            }

            if (month < 1 || month > 12)
            {
                month = iraqNow.Month;
            }

            daysInMonth = DateTime.DaysInMonth(year, month);
            from = new DateTime(year, month, 1);
            toExclusive = from.AddMonths(1);
            previousFrom = from.AddMonths(-1);
        }

        public static string CategorizeItem(string? name)
        {
            var text = (name ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return "أخرى";
            }

            if (Has(text, "دراجة", "بايك", "بايسكل", "سكوتر", "bike", "ebike"))
            {
                return "دراجات";
            }

            if (Has(text, "هاتف", "جوال", "موبايل", "ايفون", "آيفون", "أيفون", "سامسونج", "شاومي", "ريدمي", "phone", "iphone"))
            {
                return "هواتف";
            }

            if (Has(text, "لابتوب", "حاسوب", "كمبيوتر", "laptop"))
            {
                return "حواسيب";
            }

            if (Has(text, "شاشة", "تلفزيون", "تلفاز"))
            {
                return "شاشات";
            }

            return "أخرى";
        }

        public static List<SalesOverviewCategoryDTO> CategoriesFrom(IEnumerable<SalesOverviewItemDTO> items)
        {
            return items
                .GroupBy(i => string.IsNullOrWhiteSpace(i.Category) ? CategorizeItem(i.ItemName) : i.Category)
                .Select(g => new SalesOverviewCategoryDTO
                {
                    Category = g.Key,
                    Quantity = g.Sum(x => x.Quantity),
                    SalesCount = g.Sum(x => x.SalesCount),
                    SalePrice = g.Sum(x => x.SalePrice)
                })
                .OrderByDescending(x => x.SalePrice)
                .ThenBy(x => x.Category)
                .ToList();
        }

        private static bool Has(string text, params string[] parts)
        {
            foreach (var part in parts)
            {
                if (text.Contains(part, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
