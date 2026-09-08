using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.IRepository
{
    public interface IFollowerActionsRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task<(int DelegateId, string? CustomerName, string? Phone, string? Address, string? CityName, int? UserId, string? SaleName)?> GetCustomerScopeAsync(int customerId, CancellationToken ct = default);
        Task<bool> EmployeeAppearsOnListAsync(int listId, int employeeId, CancellationToken ct = default);
        Task<FollowerCustomerNoteDTO> AddCustomerNoteAsync(FollowerCustomerNoteDTO note, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerCustomerNoteDTO>> ListCustomerNotesAsync(int customerId, int followerId, CancellationToken ct = default);
        Task<FollowerEmployeeNoteDTO> AddEmployeeNoteAsync(FollowerEmployeeNoteDTO note, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerEmployeeNoteDTO>> ListEmployeeNotesForFollowerAsync(int employeeId, int followerId, CancellationToken ct = default);
        Task<FollowerSalesRequestResultDTO> InsertSalesRequestAsync(FollowerSalesRequestResultDTO meta, string customerName, string? phone, string? province, string? address, string? notes, int? existingCustomerId, string? cityValue, string? cityName, CancellationToken ct = default);
    }
}
