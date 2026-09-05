using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public sealed class SalesPriceSnapshot
    {
        public decimal DefaultTotalSalePrice { get; init; }
        public decimal DefaultDailyInstallment { get; init; }
        public decimal DefaultDownPayment { get; init; }
        public decimal? OverrideTotalSalePrice { get; init; }
        public decimal? OverrideDailyInstallment { get; init; }
        public decimal? OverrideDownPayment { get; init; }

        public decimal FinalTotalSalePrice => OverrideTotalSalePrice ?? DefaultTotalSalePrice;
        public decimal FinalDailyInstallment => OverrideDailyInstallment ?? DefaultDailyInstallment;
        public decimal FinalDownPayment => OverrideDownPayment ?? DefaultDownPayment;
    }

    public interface ISalesPricingService
    {
        decimal ComputeFinalSalePrice(decimal baseSalePrice, int evaluationLevel);
        string ResolveStatus(int evaluationLevel);
        SalesPriceSnapshot ComputeCheckout(
            decimal defaultTotalSalePrice,
            decimal defaultDailyInstallment,
            decimal? overrideTotalSalePrice,
            decimal? overrideDailyInstallment,
            decimal? overrideDownPayment);
    }

    public sealed class SalesPricingService : ISalesPricingService
    {
        public const decimal DownPaymentRate = 0.05m;

        public decimal ComputeFinalSalePrice(decimal baseSalePrice, int evaluationLevel)
        {
            // Evaluation is no longer used for pricing.
            _ = evaluationLevel;
            return baseSalePrice;
        }

        public string ResolveStatus(int evaluationLevel)
        {
            _ = evaluationLevel;
            return SalesStatuses.Pending;
        }

        public SalesPriceSnapshot ComputeCheckout(
            decimal defaultTotalSalePrice,
            decimal defaultDailyInstallment,
            decimal? overrideTotalSalePrice,
            decimal? overrideDailyInstallment,
            decimal? overrideDownPayment)
        {
            var total = Math.Round(defaultTotalSalePrice, 0, MidpointRounding.AwayFromZero);
            var daily = Math.Round(defaultDailyInstallment, 0, MidpointRounding.AwayFromZero);
            var down = Math.Round(total * DownPaymentRate, 0, MidpointRounding.AwayFromZero);
            return new SalesPriceSnapshot
            {
                DefaultTotalSalePrice = total,
                DefaultDailyInstallment = daily,
                DefaultDownPayment = down,
                OverrideTotalSalePrice = PositiveOrNull(overrideTotalSalePrice),
                OverrideDailyInstallment = PositiveOrNull(overrideDailyInstallment),
                OverrideDownPayment = overrideDownPayment is null
                    ? null
                    : Math.Round(overrideDownPayment.Value, 0, MidpointRounding.AwayFromZero)
            };
        }

        private static decimal? PositiveOrNull(decimal? value) =>
            value is > 0 ? Math.Round(value.Value, 0, MidpointRounding.AwayFromZero) : null;
    }
}
