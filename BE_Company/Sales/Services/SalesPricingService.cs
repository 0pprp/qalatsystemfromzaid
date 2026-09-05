using BE_Company.Sales.Authorization;

namespace BE_Company.Sales.Services
{
    public interface ISalesPricingService
    {
        decimal ComputeFinalSalePrice(decimal baseSalePrice, int evaluationLevel);
        string ResolveStatus(int evaluationLevel);
    }

    public sealed class SalesPricingService : ISalesPricingService
    {
        public decimal ComputeFinalSalePrice(decimal baseSalePrice, int evaluationLevel)
        {
            if (SalesEvaluationLevels.BlocksSale(evaluationLevel))
            {
                return 0;
            }

            return baseSalePrice;
        }

        public string ResolveStatus(int evaluationLevel)
        {
            return SalesEvaluationLevels.BlocksSale(evaluationLevel)
                ? SalesStatuses.Rejected
                : SalesStatuses.Pending;
        }
    }
}
