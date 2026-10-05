using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;

namespace BE_Company.Sales.Services
{
    public interface ISalesCascadeVoidService
    {
        Task<SalesRequestSoftDeleteImpactDTO> GetImpactAsync(SalesRequestDTO request, CancellationToken ct);

        /// <summary>
        /// Compensates linked draft/sale/payments/stock before the request is archived.
        /// Throws 409 with code payments_confirm_required when payments exist and confirm is false.
        /// </summary>
        Task VoidLinkedAsync(
            SalesIdentity manager,
            SalesRequestDTO request,
            bool confirmCascadePayments,
            string reason,
            CancellationToken ct);
    }
}
