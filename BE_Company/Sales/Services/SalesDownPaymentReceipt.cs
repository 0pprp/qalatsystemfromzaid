namespace BE_Company.Sales.Services
{
    public static class SalesDownPaymentReceipt
    {
        public static bool ShouldRecord(decimal downPayment, int? existingPaymentId) =>
            downPayment > 0 && existingPaymentId is not > 0;

        public static decimal Remaining(decimal saleTotal, decimal receivedPayments) =>
            saleTotal - receivedPayments;
    }
}
