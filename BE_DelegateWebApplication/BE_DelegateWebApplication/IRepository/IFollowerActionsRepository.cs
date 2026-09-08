using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.IRepository
{
    public interface IFollowerActionsRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task<(int DelegateId, string? CustomerName, string? Phone, string? Address, string? CityName, int? UserId, string? SaleName)?> GetCustomerScopeAsync(int customerId, CancellationToken ct = default);
        Task<string?> GetDelegateNameAsync(int delegateId, CancellationToken ct = default);
        Task<FollowerCustomerNoteDTO> AddCustomerNoteAsync(FollowerCustomerNoteDTO note, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerCustomerNoteDTO>> ListCustomerNotesAsync(int customerId, int followerId, CancellationToken ct = default);
        Task<FollowerDelegateNoteDTO> AddDelegateNoteAsync(FollowerDelegateNoteDTO note, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerDelegateNoteDTO>> ListDelegateNotesForFollowerAsync(int delegateId, int followerId, CancellationToken ct = default);
        Task<FollowerSalesRequestResultDTO> InsertSalesRequestAsync(FollowerSalesRequestResultDTO meta, string customerName, string? phone, string? province, string? address, string? notes, int? existingCustomerId, string? cityValue, string? cityName, CancellationToken ct = default);
    }
}
