using BE_Company.Sales.Authorization;

namespace BE_Company.Sales.Services;

/// <summary>
/// «طلبات البيع» of one list's delegate on متابعة المندوبين: outcome buckets + new/old customer detection.
/// Pure — unit tested.
/// </summary>
public static class DelegateSalesOrdersRules
{
    public const string OutcomeSold = "sold";
    public const string OutcomeRejected = "rejected";
    public const string OutcomeOpen = "open";

    /// <summary>Sold beats everything; then any rejection (request, filter, deleted, rejected draft); else still open.</summary>
    public static string Outcome(string? requestStatus, string? filterStatus, bool isDeleted, string? draftStatus)
    {
        if (Is(requestStatus, "Completed") || Is(draftStatus, SalesStatuses.Completed))
        {
            return OutcomeSold;
        }

        if (Is(requestStatus, "Rejected") || Is(filterStatus, "Rejected") || isDeleted
            || Is(draftStatus, SalesStatuses.Rejected))
        {
            return OutcomeRejected;
        }

        return OutcomeOpen;
    }

    public static string StatusLabel(string outcome, string? requestStatus, string? filterStatus) => outcome switch
    {
        OutcomeSold => "صار مبيع",
        OutcomeRejected => "مرفوض",
        _ => (requestStatus ?? "") switch
        {
            "New" when Is(filterStatus, "PendingFilter") => "بانتظار الفلترة",
            "New" when Is(filterStatus, "OnHold") => "معلق",
            "New" => "جديد",
            "Assigned" => "محوّل لموظف",
            "Viewed" => "تمت المشاهدة",
            "Pending" => "معلق",
            "PreparedForSale" => "جاهز للبيع",
            "InProgress" => "قيد البيع",
            "ConvertedToSale" => "قيد البيع",
            "Inspected" => "تم الكشف",
            "Returned" => "معاد",
            "LegalFollowup" => "متابعة قانونية",
            _ => "قيد العمل"
        }
    };

    /// <summary>Evaluation buckets in display order (sales employee evaluation 1–5, 0 = not evaluated yet).</summary>
    public static readonly IReadOnlyList<(int Level, string Label)> EvaluationBuckets =
    [
        (SalesEvaluationLevels.Excellent, "ممتاز"),
        (SalesEvaluationLevels.VeryGood, "جيد جداً"),
        (SalesEvaluationLevels.Good, "جيد"),
        (SalesEvaluationLevels.Accepted, "مقبول"),
        (SalesEvaluationLevels.Rejected, "مرفوض"),
        (0, "بدون تقييم")
    ];

    public static int EvaluationBucket(int? level) =>
        level is int l && SalesEvaluationLevels.IsKnown(l) ? l : 0;

    public sealed record ExistingCustomer(int CustomerId, string? Name, string? Phone);

    /// <summary>Lookup over every branch customer: normalized triple name and phone.</summary>
    public sealed class CustomerIndex
    {
        private readonly Dictionary<string, List<ExistingCustomer>> _byTriple = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ExistingCustomer>> _byPhone = new(StringComparer.Ordinal);

        public CustomerIndex(IEnumerable<ExistingCustomer> customers)
        {
            foreach (var c in customers)
            {
                var t = TripleKey(c.Name);
                if (t != null)
                {
                    Add(_byTriple, t, c);
                }

                var p = SalesPhoneNormalizer.ForMatch(c.Phone);
                if (!string.IsNullOrWhiteSpace(p))
                {
                    Add(_byPhone, p, c);
                }
            }
        }

        /// <summary>
        /// An earlier customer (id below the sale's own customer, never the sale's own row) with the same
        /// phone or the same first three name parts. Phone wins when both exist.
        /// </summary>
        public (ExistingCustomer Customer, string Reason)? FindEarlier(string? name, string? phone, int? ownCustomerId)
        {
            bool Earlier(ExistingCustomer c) =>
                c.CustomerId != ownCustomerId && (ownCustomerId is not > 0 || c.CustomerId < ownCustomerId);

            var p = SalesPhoneNormalizer.ForMatch(phone);
            if (!string.IsNullOrWhiteSpace(p) && _byPhone.TryGetValue(p, out var byPhone))
            {
                var hit = byPhone.Where(Earlier).OrderBy(c => c.CustomerId).FirstOrDefault();
                if (hit != null)
                {
                    return (hit, "نفس رقم الهاتف");
                }
            }

            var t = TripleKey(name);
            if (t != null && _byTriple.TryGetValue(t, out var byName))
            {
                var hit = byName.Where(Earlier).OrderBy(c => c.CustomerId).FirstOrDefault();
                if (hit != null)
                {
                    return (hit, "نفس الاسم الثلاثي");
                }
            }

            return null;
        }

        private static void Add(Dictionary<string, List<ExistingCustomer>> map, string key, ExistingCustomer c)
        {
            if (!map.TryGetValue(key, out var list))
            {
                map[key] = list = [];
            }

            list.Add(c);
        }
    }

    /// <summary>First three normalized name tokens, or null when the name has fewer than three.</summary>
    public static string? TripleKey(string? name)
    {
        var tokens = SalesRequestNameSimilarity.MatchingTokens(name);
        return tokens.Count < 3 ? null : $"{tokens[0]}|{tokens[1]}|{tokens[2]}";
    }

    /// <summary>Sale request typed «مبيع قديم» or tied to an existing customer is old without matching.</summary>
    public static bool IsDeclaredOld(string? saleRequestType, int? existingCustomerId) =>
        existingCustomerId is > 0 || Is(saleRequestType, SaleRequestTypes.Old);

    public static bool InMonth(DateTime? iraqDate, DateTime iraqToday) =>
        iraqDate is DateTime d && d.Year == iraqToday.Year && d.Month == iraqToday.Month;

    private static bool Is(string? a, string b) =>
        string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
}
