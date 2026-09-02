using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface ISalesEmployeeRepository
    {
        Task EnsureTablesAsync();
        Task<SalesEmployeeShiftDTO?> GetActiveShift(int userID, DateTime nowIraq);
        Task<SalesEmployeeShiftDTO> StartShift(int userID, DateTime nowIraq);
        Task<SalesEmployeeTrackSyncResultDTO> SyncTrackPoints(int userID, SalesEmployeeTrackSyncDTO dto);
        Task<SalesCustomerRatingGetDTO?> SaveRating(int userID, SalesCustomerRatingPostDTO dto);
        Task<IEnumerable<SalesEmployeeSearchCustomerDTO>> SearchCustomers(string? textSearch);
    }
}
