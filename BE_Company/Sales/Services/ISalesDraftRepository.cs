using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public interface ISalesDraftRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct);
        Task<SalesDraftDTO> CreateAsync(SalesDraftDTO draft, CancellationToken ct);
        Task UpdateCheckoutAsync(SalesDraftDTO draft, CancellationToken ct);
        Task<IReadOnlyList<SalesDraftDTO>> GetByEmployeeAsync(int employeeId, CancellationToken ct);
        Task<SalesDraftDTO?> GetByIdAsync(int saleId, int employeeId, CancellationToken ct);
    }
}
