using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.IRepository
{
    public interface ITrustReceiptRepository
    {
        Task<int> CreateAsync(TrustReceiptDTO dto);
        Task<bool> UpdateAsync(TrustReceiptDTO dto);
        Task<bool> DeleteAsync(int id, int? updatedByUserId);
        Task<TrustReceiptDTO?> GetByIdAsync(int id);
        Task<PagedResultDTO<TrustReceiptDTO>> GetPagedAsync(string? searchTerm, int pageNumber, int pageSize, int? delegateId);
    }
}
