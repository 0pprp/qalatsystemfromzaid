using BE_Company.Sales.DTO;
using BE_Company.Sales.Rating;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Rates each official sale by the app's customer payment score
    /// (ممتاز / جيد / ضعيف / متلكئ / سيء), not by a staff shop rating.
    /// </summary>
    public static class SalesOverviewPayment
    {
        public readonly record struct SaleLine(int CustomerId, string Seller, decimal SalePrice);

        public static bool IsClassified(CustomerRatingResult result) =>
            result.Reason.Contains(" - ", StringComparison.Ordinal);

        public static void Apply(
            SalesManagerDashboardDTO dto,
            IReadOnlyList<SaleLine> sales,
            IReadOnlyDictionary<int, CustomerRatingFacts> facts,
            DateTime asOf)
        {
            var sellers = new Dictionary<string, SellerBucket>(StringComparer.Ordinal);
            var bands = new Dictionary<string, BandBucket>(StringComparer.Ordinal);
            var scored = 0;
            decimal scoreSum = 0;

            foreach (var sale in sales)
            {
                var seller = string.IsNullOrWhiteSpace(sale.Seller) ? "بدون اسم" : sale.Seller.Trim();
                if (!sellers.TryGetValue(seller, out var bucket))
                {
                    bucket = new SellerBucket(seller);
                    sellers[seller] = bucket;
                }

                bucket.SalesCount++;
                bucket.SalePrice += sale.SalePrice;

                if (!facts.TryGetValue(sale.CustomerId, out var fact))
                {
                    continue;
                }

                var rated = CustomerRatingCalculator.Evaluate(fact, asOf);
                if (!IsClassified(rated))
                {
                    continue;
                }

                scored++;
                scoreSum += rated.Score;
                bucket.Scored++;
                bucket.ScoreSum += rated.Score;
                AddBand(bands, rated, sale.SalePrice);
                switch (rated.Level)
                {
                    case CustomerRatingLevel.Excellent:
                        dto.ExcellentSales++;
                        bucket.Excellent++;
                        break;
                    case CustomerRatingLevel.Good:
                        dto.GoodSales++;
                        bucket.Good++;
                        break;
                    case CustomerRatingLevel.Weak:
                        dto.WeakSales++;
                        bucket.Weak++;
                        break;
                    case CustomerRatingLevel.Rejected:
                        dto.LaggingSales++;
                        bucket.Lagging++;
                        break;
                    case CustomerRatingLevel.Bad:
                        dto.BadSales++;
                        bucket.Bad++;
                        break;
                }
            }

            dto.ScoredSalesCount = scored;
            dto.ScoreSum = scoreSum;
            dto.UnscoredSalesCount = Math.Max(0, sales.Count - scored);
            dto.AverageSaleScore = SalesOverviewMath.AverageScore(scoreSum, scored);
            dto.SaleScores = bands.Values
                .Select(band => new SalesOverviewScoreDTO
                {
                    Label = band.Label,
                    Score = band.Score,
                    Count = band.Count,
                    SalePrice = band.SalePrice
                })
                .OrderByDescending(x => x.Score)
                .ToList();
            dto.Sellers = sellers.Values
                .Select(bucket => new SalesOverviewPersonDTO
                {
                    Name = bucket.Name,
                    SalesCount = bucket.SalesCount,
                    SalePrice = bucket.SalePrice,
                    ScoredSales = bucket.Scored,
                    ScoreSum = bucket.ScoreSum,
                    AverageScore = SalesOverviewMath.AverageScore(bucket.ScoreSum, bucket.Scored),
                    ExcellentSales = bucket.Excellent,
                    GoodSales = bucket.Good,
                    WeakSales = bucket.Weak,
                    LaggingSales = bucket.Lagging,
                    BadSales = bucket.Bad
                })
                .OrderByDescending(x => x.SalePrice)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .ToList();
        }

        private static void AddBand(Dictionary<string, BandBucket> bands, CustomerRatingResult rated, decimal salePrice)
        {
            if (!bands.TryGetValue(rated.RatingLabel, out var band))
            {
                band = new BandBucket(rated.RatingLabel, rated.Score);
                bands[rated.RatingLabel] = band;
            }

            band.Count++;
            band.SalePrice += salePrice;
        }

        private sealed class BandBucket(string label, int score)
        {
            public string Label { get; } = label;
            public int Score { get; } = score;
            public int Count { get; set; }
            public decimal SalePrice { get; set; }
        }

        private sealed class SellerBucket(string name)
        {
            public string Name { get; } = name;
            public int SalesCount { get; set; }
            public decimal SalePrice { get; set; }
            public int Scored { get; set; }
            public decimal ScoreSum { get; set; }
            public int Excellent { get; set; }
            public int Good { get; set; }
            public int Weak { get; set; }
            public int Lagging { get; set; }
            public int Bad { get; set; }
        }
    }
}
