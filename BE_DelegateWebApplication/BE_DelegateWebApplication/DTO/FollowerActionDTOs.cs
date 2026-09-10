namespace BE_DelegateWebApplication.DTO
{
    public sealed class FollowerNoteCreateDTO
    {
        public string? AsyncId { get; set; }
        public int ListId { get; set; }
        public string? NoteText { get; set; }
        /// <summary>Ignored — server sets identity from authenticated follower.</summary>
        public int? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedByRole { get; set; }
    }

    public sealed class FollowerCustomerNoteDTO
    {
        public int Id { get; set; }
        public int CustomerId { get; set; }
        public string NoteText { get; set; } = string.Empty;
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string CreatedByRole { get; set; } = "Follower";
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class FollowerDelegateNoteDTO
    {
        public int Id { get; set; }
        public int DelegateId { get; set; }
        public int? ListId { get; set; }
        public int CreatedByUserId { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public string CreatedByRole { get; set; } = "Follower";
        public string NoteText { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class FollowerSalesRequestCreateDTO
    {
        public string? AsyncId { get; set; }
        public int ListId { get; set; }
        /// <summary>0 / null = زبون جديد (لا يُنشأ في Customers).</summary>
        public int? CustomerId { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        /// <summary>Ignored — province is taken from authenticated follower city.</summary>
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        /// <summary>Ignored.</summary>
        public int? CreatedByUserId { get; set; }
        /// <summary>New | Old. Optional for follower (inferred from CustomerId).</summary>
        public string? SaleRequestType { get; set; }
    }

    public sealed class FollowerSalesRequestResultDTO
    {
        public int Id { get; set; }
        public string CustomerSourceType { get; set; } = "Follower";
        public string? SaleRequestType { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedByUserType { get; set; }
        public int CreatedByUserId { get; set; }
        public string Status { get; set; } = "New";
        public DateTime CreatedAtUtc { get; set; }
        public int? ExistingCustomerId { get; set; }
    }

    /// <summary>Same payload shape as follower; SaleRequestType required for delegates (New|Old).</summary>
    public sealed class DelegateSalesRequestCreateDTO
    {
        public string? AsyncId { get; set; }
        /// <summary>Defaults to authenticated DelegateId when 0.</summary>
        public int ListId { get; set; }
        public int? CustomerId { get; set; }
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        /// <summary>Ignored — server sets from delegate/customer.</summary>
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? Notes { get; set; }
        /// <summary>Required: New (Home) or Old (customer card).</summary>
        public string? SaleRequestType { get; set; }
    }

    public static class SaleRequestTypes
    {
        public const string New = "New";
        public const string Old = "Old";

        public static bool IsNew(string? v) =>
            string.Equals(v, New, StringComparison.OrdinalIgnoreCase);

        public static bool IsOld(string? v) =>
            string.Equals(v, Old, StringComparison.OrdinalIgnoreCase);

        public static string Normalize(string? v, bool hasExistingCustomer) =>
            IsOld(v) || (!IsNew(v) && hasExistingCustomer) ? Old : New;
    }

    public sealed class FollowerCustomerProfileDTO
    {
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public string? CityName { get; set; }
        public string? ShopName { get; set; }
        public string? StoreAddress { get; set; }
        public string? StorePhoneNumber { get; set; }
        public string? NearestFunctionPoint { get; set; }
        public string? Neighborhood { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? CustomerImage { get; set; }
        public string? CustomerImageUrl { get; set; }
        public string? DelegateName { get; set; }
        public int? ListDelegateId { get; set; }
        public double? CostTotalSales { get; set; }
        public double? AmountTotalSales { get; set; }
        public double? AmountDaySales { get; set; }
        public double? AmountRemaining { get; set; }
        public double? ReceiptsTotal { get; set; }
        public double? AmountReceverDay { get; set; }
        public string? ItemsNames { get; set; }
        public DateTime? DateSaleDevice { get; set; }
        /// <summary>ملاحظات مخزّنة على سجل الزبون (قراءة فقط).</summary>
        public string? CustomerSystemNotes { get; set; }
        public List<FollowerCustomerNoteDTO> Notes { get; set; } = [];
        public List<FollowerProfileImageDTO> Images { get; set; } = [];
    }

    public sealed class FollowerProfileImageDTO
    {
        public int? DocumentId { get; set; }
        /// <summary>DocumentType / Customer / Shop.</summary>
        public string Kind { get; set; } = "Customer";
        public string? Label { get; set; }
        public string? FileName { get; set; }
        public string? Url { get; set; }
    }

    /// <summary>Row from dbo.SalesCustomerDocuments (same source as company KYC gallery).</summary>
    public sealed class FollowerSalesDocumentRow
    {
        public int Id { get; set; }
        public int? SaleId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FileKey { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
    }
}
