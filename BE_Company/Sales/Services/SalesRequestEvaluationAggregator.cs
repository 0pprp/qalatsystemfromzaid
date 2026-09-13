using BE_Company.Sales.Rating;

namespace BE_Company.Sales.Services
{
    public static class SalesRequestEvaluationAggregator
    {
        public static (CustomerRatingLevel Level, int Score)? WorstOf(
            IEnumerable<(CustomerRatingLevel Level, int Score)> scores)
        {
            (CustomerRatingLevel Level, int Score)? worst = null;
            foreach (var item in scores)
            {
                if (worst is null || item.Score < worst.Value.Score)
                {
                    worst = item;
                }
            }

            return worst;
        }

        public static (CustomerRatingLevel Level, int Score)? Overall(
            (CustomerRatingLevel Level, int Score)? triple,
            (CustomerRatingLevel Level, int Score)? phone,
            (CustomerRatingLevel Level, int Score)? kinship)
        {
            var list = new List<(CustomerRatingLevel, int)>();
            if (triple is { } t) list.Add(t);
            if (phone is { } p) list.Add(p);
            if (kinship is { } k) list.Add(k);
            return list.Count == 0 ? null : WorstOf(list);
        }

        public static string LabelOf((CustomerRatingLevel Level, int Score)? value) =>
            value is null ? "لا يوجد تطابق" : CustomerRatingLabels.FromLevel(value.Value.Level);
    }
}
