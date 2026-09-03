using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public static class SalesCompleteRules
    {
        public static string? ValidateForComplete(SalesDraftDTO sale)
        {
            if (sale.Status == SalesStatuses.Rejected || sale.EvaluationLevel == SalesEvaluationLevels.Rejected)
            {
                return "لا يمكن إتمام عملية بيع مرفوضة.";
            }

            if (sale.Status == SalesStatuses.Completed || sale.Status == "DocumentsReady" || sale.Status == "DocumentsPending")
            {
                return null;
            }

            if (!string.Equals(sale.Status, SalesStatuses.Pending, StringComparison.OrdinalIgnoreCase))
            {
                return "لا يمكن إتمام هذه العملية في حالتها الحالية.";
            }

            if (sale.DailyInstallment <= 0)
            {
                return "القسط اليومي يجب أن يكون أكبر من صفر.";
            }

            if (string.IsNullOrWhiteSpace(sale.FullName)
                || string.IsNullOrWhiteSpace(sale.Phone)
                || string.IsNullOrWhiteSpace(sale.Province)
                || string.IsNullOrWhiteSpace(sale.NationalCardNumber)
                || string.IsNullOrWhiteSpace(sale.Address)
                || string.IsNullOrWhiteSpace(sale.NearestLandmark)
                || string.IsNullOrWhiteSpace(sale.MukhtarName))
            {
                return "بيانات الزبون غير مكتملة.";
            }

            if (sale.Items == null || sale.Items.Count == 0)
            {
                return "لا توجد مواد في العملية.";
            }

            return null;
        }

        public static bool AlreadyCompleted(string? status) =>
            status is SalesStatuses.Completed or "DocumentsReady" or "DocumentsPending";

        public static string GoodsDescription(IEnumerable<SalesDraftItemDTO> items) =>
            string.Join("، ", items.Select(i => $"{i.ProductName} عدد {i.Quantity}"));
    }
}
