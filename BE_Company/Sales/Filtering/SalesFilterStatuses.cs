using BE_Company.Sales.Services;

namespace BE_Company.Sales.Filtering
{
    /// <summary>Filter gate before a sales request is visible to the assigned sales employee.</summary>
    public static class SalesFilterStatuses
    {
        public const string PendingFilter = "PendingFilter";
        public const string OnHold = "OnHold";
        public const string ReadyForSale = "ReadyForSale";
        public const string Rejected = "Rejected";

        public static string ArabicLabel(string? status) => status switch
        {
            PendingFilter => "طلبات البيع",
            OnHold => "معلق",
            ReadyForSale => "جاهز للبيع",
            Rejected => "مرفوض",
            _ => status ?? "",
        };

        public static bool IsKnown(string? status) =>
            status is PendingFilter or OnHold or ReadyForSale or Rejected;

        public static bool CanTransition(string? from, string to)
        {
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase) && to == OnHold)
            {
                return true;
            }

            return (from, to) switch
            {
                (PendingFilter, OnHold) => true,
                (PendingFilter, ReadyForSale) => true,
                (PendingFilter, Rejected) => true,
                (OnHold, ReadyForSale) => true,
                (OnHold, Rejected) => true,
                (OnHold, OnHold) => true,
                (Rejected, Rejected) => true,
                _ => false,
            };
        }

        /// <summary>Employee may see the request only in this filter state (after legacy backfill).</summary>
        public static bool IsVisibleToSalesEmployee(string? filterStatus) =>
            string.Equals(filterStatus, ReadyForSale, StringComparison.OrdinalIgnoreCase);
    }

    public static class SalesFilterValidation
    {
        public const int NoteMaxLength = 1000;
        public const int RejectReasonMinLength = 3;
        public const int RejectReasonMaxLength = 400;

        public static string? NormalizeOptionalNote(string? note)
        {
            if (string.IsNullOrWhiteSpace(note)) return null;
            var t = note.Trim();
            if (t.Length > NoteMaxLength)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, $"الملاحظة أطول من {NoteMaxLength} حرف.");
            }

            return t;
        }

        public static string RequireRejectReason(string? reason)
        {
            var t = (reason ?? string.Empty).Trim();
            if (t.Length < RejectReasonMinLength)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "سبب الرفض مطلوب.");
            }

            if (t.Length > RejectReasonMaxLength)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, $"سبب الرفض أطول من {RejectReasonMaxLength} حرف.");
            }

            return t;
        }
    }
}
