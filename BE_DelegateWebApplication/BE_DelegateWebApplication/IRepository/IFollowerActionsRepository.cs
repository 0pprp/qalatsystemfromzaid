using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication.IRepository
{
    public interface IFollowerActionsRepository
    {
        Task EnsureSchemaAsync(CancellationToken ct = default);
        Task<(int DelegateId, string? CustomerName, string? Phone, string? Address, string? CityName, int? UserId, string? SaleName)?> GetCustomerScopeAsync(int customerId, CancellationToken ct = default);
        Task<string?> GetDelegateNameAsync(int delegateId, CancellationToken ct = default);
        /// <summary>City/province of the authenticated follower (UsersSelectedCities → Cities).</summary>
        Task<string?> GetFollowerCityNameAsync(int followerUserId, CancellationToken ct = default);
        /// <summary>City/province of an assigned list (Delegates.CityID → Cities).</summary>
        Task<string?> GetListCityNameAsync(int listDelegateId, CancellationToken ct = default);
        Task<string?> GetCityNameByIdAsync(int cityId, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerCustomerPaymentDTO>> ListCustomerPaymentsAsync(int customerId, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerSalesDocumentRow>> ListCustomerSalesDocumentsAsync(int customerId, string? customerName, string? phone, CancellationToken ct = default);
        Task<FollowerSalesDocumentRow?> GetSalesDocumentAsync(int documentId, CancellationToken ct = default);
        Task<(string? ShopImageKey, int? SaleId)?> GetCustomerShopImageAsync(int customerId, string? customerName, string? phone, CancellationToken ct = default);
        Task<(string FileName, byte[] Bytes, string ContentType)?> ReadSalesDocumentFileAsync(int documentId, CancellationToken ct = default);
        Task<(string FileName, byte[] Bytes, string ContentType)?> ReadShopImageFileAsync(string shopImageKey, CancellationToken ct = default);
        /// <summary>Failure diagnostics for document path resolution (no secrets).</summary>
        string DescribeDocumentPathResolution(string fileKey);
        Task<FollowerCustomerNoteDTO> AddCustomerNoteAsync(FollowerCustomerNoteDTO note, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerCustomerNoteDTO>> ListCustomerNotesAsync(int customerId, int followerId, CancellationToken ct = default);
        Task<FollowerDelegateNoteDTO> AddDelegateNoteAsync(FollowerDelegateNoteDTO note, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerDelegateNoteDTO>> ListDelegateNotesForFollowerAsync(int delegateId, int followerId, CancellationToken ct = default);
        Task<FollowerSalesRequestResultDTO> InsertSalesRequestAsync(
            FollowerSalesRequestResultDTO meta,
            string customerName,
            string? phone,
            string? province,
            string? address,
            string? notes,
            int? existingCustomerId,
            string? cityValue,
            string? cityName,
            string customerSourceType,
            string saleRequestType,
            int? sourceListId = null,
            int? shopRating = null,
            string? shopRatingNote = null,
            decimal? shopStockEstimatedValue = null,
            decimal? estimatedDailyRevenue = null,
            CancellationToken ct = default);

        /// <summary>
        /// Lists sales requests originated by this follower.
        /// tab: all | inProgress | rejected | completed
        /// </summary>
        Task<IReadOnlyList<FollowerSalesRequestListItemDTO>> ListSalesRequestsForFollowerAsync(
            int followerId,
            string? tab,
            CancellationToken ct = default);

        /// <summary>
        /// Lists sales requests originated by this delegate (CustomerSourceType = Delegate).
        /// tab: all | inProgress | rejected | completed
        /// </summary>
        Task<IReadOnlyList<FollowerSalesRequestListItemDTO>> ListSalesRequestsForDelegateAsync(
            int delegateId,
            string? tab,
            CancellationToken ct = default);
    }
}
