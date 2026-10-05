namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Pure rules for compensating soft-delete when a completed request links a posted draft.
    /// </summary>
    public static class SalesCascadeVoidRules
    {
        public const string MissingOfficialSaleWarningAr =
            "المبيع الرسمي غير موجود (ربما حُذف مسبقاً). سيتم إبطال المسودة وأرشفة الطلب فقط.";

        public const string UnprovenSaleLinkWarningAr =
            "لا يوجد رابط مثبت بين المسودة والمبيع الرسمي. لن يُحذف أي مبيع/تسديد تلقائياً.";

        public const string AmbiguousPaymentsCode = "ambiguous_customer_payments";

        public const string AmbiguousPaymentsMessageAr =
            "لا يمكن حذف التسديدات تلقائياً: للزبون أكثر من مبيع رسمي. عالج التسديدات يدوياً ثم أعد المحاولة.";

        public static bool ShouldDeleteOfficialSale(int? resolvedCustomerSaleId) =>
            resolvedCustomerSaleId is > 0;

        /// <summary>
        /// When the draft stored a CustomerSaleId that no longer exists, never guess another
        /// sale for the same customer/phone — that would delete the wrong official sale.
        /// </summary>
        public static bool ShouldStopAfterPhantomStoredId(int? storedCustomerSaleId, bool storedIdExistsInOfficial) =>
            storedCustomerSaleId is > 0 && !storedIdExistsInOfficial;

        /// <summary>
        /// Destructive void must use only a proven draft.CustomerSaleId that still exists.
        /// Heuristic "customer's sole sale" / phone matching is forbidden for deletes.
        /// </summary>
        public static int? ResolveProvenOfficialSaleId(int? storedCustomerSaleId, bool storedIdExistsInOfficial)
        {
            if (storedCustomerSaleId is > 0 && storedIdExistsInOfficial)
            {
                return storedCustomerSaleId;
            }

            return null;
        }

        /// <summary>
        /// Only delete customer payments when every payment clearly belongs to the sale being voided:
        /// customer has exactly one official sale and that sale is the proven linked id.
        /// </summary>
        public static bool CanDeleteCustomerPaymentsForSale(
            int? provenCustomerSaleId,
            int officialSaleCountForCustomer) =>
            provenCustomerSaleId is > 0 && officialSaleCountForCustomer == 1;
    }
}
