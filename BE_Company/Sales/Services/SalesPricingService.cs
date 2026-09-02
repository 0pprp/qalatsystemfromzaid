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
            if (evaluationLevel == SalesEvaluationLevels.Rejected)
            {
                return 0;
            }

            if (evaluationLevel == SalesEvaluationLevels.Accepted)
            {
                return baseSalePrice * 2;
            }

            return baseSalePrice;
        }

        public string ResolveStatus(int evaluationLevel)
        {
            return evaluationLevel == SalesEvaluationLevels.Rejected
                ? SalesStatuses.Rejected
                : SalesStatuses.Pending;
        }
    }
}
