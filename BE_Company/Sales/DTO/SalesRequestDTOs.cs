namespace BE_Company.Sales.DTO
{
    public sealed class SalesRequestCustomerDTO
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
    }

    public sealed class SalesRequestCreateDTO
    {
        public int TargetEmployeeId { get; set; }
        public string? TargetEmployeeName { get; set; }
        public int? ExistingCustomerId { get; set; }
        public string? CustomerSourceCityValue { get; set; }
        public SalesRequestCustomerDTO? Customer { get; set; }
        public string? Notes { get; set; }
        /// <summary>Optional ceiling: employee FinalSalePrice must not exceed this.</summary>
        public decimal? MaxSalePrice { get; set; }
    }

    public sealed class SalesRequestAssignDTO
    {
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public int? ExistingCustomerId { get; set; }
        public string? CustomerSourceCityValue { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? CustomerProvince { get; set; }
        public string? CustomerAddress { get; set; }
        public string? Notes { get; set; }
        public bool KeepNewCustomer { get; set; }
        /// <summary>Optional ceiling set/updated on assign.</summary>
        public decimal? MaxSalePrice { get; set; }
    }

    public sealed class SalesRequestAssignBatchDTO
    {
        public int EmployeeId { get; set; }
        public string? EmployeeName { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public List<int> RequestIds { get; set; } = [];
    }

    public sealed class SalesRequestAssignBatchFailureDTO
    {
        public int Id { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class SalesRequestAssignBatchResultDTO
    {
        public List<int> Succeeded { get; set; } = [];
        public List<SalesRequestAssignBatchFailureDTO> Failed { get; set; } = [];
        public int Total => Succeeded.Count + Failed.Count;
    }

    public sealed class SalesRequestImportRowDTO
    {
        public int RowNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? Phone { get; set; }
        public string? Province { get; set; }
        public string? Address { get; set; }
        public string? SaleType { get; set; }

        /// <summary>Resolved branch key from Excel province (required for multi-province import).</summary>
        public string? CityValue { get; set; }

        /// <summary>Human province/branch label matching <see cref="CityValue"/>.</summary>
        public string? CityName { get; set; }
    }

    public sealed class SalesRequestImportDTO
    {
        /// <summary>Target province for a grouped import batch (central manager / gateway).</summary>
        public string? CityValue { get; set; }

        public string? CityName { get; set; }
        public List<SalesRequestImportRowDTO> Rows { get; set; } = [];
    }

    public sealed class SalesRequestImportErrorDTO
    {
        public int RowNumber { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class SalesRequestImportResultDTO
    {
        public int Total { get; set; }
        public int Saved { get; set; }
        public int Failed { get; set; }
        public List<SalesRequestImportErrorDTO> Errors { get; set; } = [];
    }

    public sealed class SalesRequestRejectDTO
    {
        public string? Reason { get; set; }
    }

    public sealed class SalesRequestNoteDTO
    {
        public string? Note { get; set; }
        public string? Reason { get; set; }
    }

    public sealed class SalesRequestHistoryDTO
    {
        public int Id { get; set; }
        public int RequestId { get; set; }
        public string Event { get; set; } = string.Empty;
        public string? PreviousStatus { get; set; }
        public string? Status { get; set; }
        public int? ActorUserId { get; set; }
        public string? ActorName { get; set; }
        public string? ActorType { get; set; }
        public int? EmployeeId { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public sealed class SalesRequestDTO
    {
        public int Id { get; set; }
        public int CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public string? CreatedByUserType { get; set; }
        public int TargetEmployeeId { get; set; }
        public int EmployeeId => TargetEmployeeId;
        public string? TargetEmployeeName { get; set; }
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public string CustomerSourceType { get; set; } = "NewCustomer";
        /// <summary>Idempotency key for CRM_OF_ALDHAMAN order intake (null for non-CRM rows).</summary>
        public Guid? ExternalCrmOrderUuid { get; set; }
        /// <summary>New | Old — set by follower/delegate submit flows.</summary>
        public string? SaleRequestType { get; set; }
        public int? SourceListId { get; set; }
        public int? ExistingCustomerId { get; set; }
        public string? CustomerSourceCityValue { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerPhone { get; set; }
        public string? CustomerProvince { get; set; }
        public string? CustomerAddress { get; set; }
        public string? Notes { get; set; }
        /// <summary>Optional max FinalSalePrice the assigned employee may use. Null = no ceiling.</summary>
        public decimal? MaxSalePrice { get; set; }
        public string Status { get; set; } = "New";
        public DateTime CreatedAtUtc { get; set; }
        /// <summary>Latest status-transition time from SalesRequestHistory (card «تاريخ آخر حالة»).</summary>
        public DateTime? LastStatusAtUtc { get; set; }
        public DateTime? ViewedAtUtc { get; set; }
        public DateTime? ProcessingAtUtc { get; set; }
        public int? ConvertedToSaleId { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? AssignedAtUtc { get; set; }
        public int? AssignedByUserId { get; set; }
        public string? AssignedByName { get; set; }
        public string? PendingNote { get; set; }
        public string? PreparedForSaleNote { get; set; }
        public string? ReturnNote { get; set; }
        public DateTime? ManagerReadAtUtc { get; set; }
        public bool IsManagerRead => ManagerReadAtUtc != null;
        /// <summary>Filter gate: PendingFilter | OnHold | ReadyForSale | Rejected</summary>
        public string? FilterStatus { get; set; }
        public int? FilteredByUserId { get; set; }
        public string? FilteredByUserName { get; set; }
        public string? FilterNote { get; set; }
        public string? FilterRejectReason { get; set; }
        public DateTime? FilteredAtUtc { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAtUtc { get; set; }
        public int? DeletedByUserId { get; set; }
        public string? DeletedByName { get; set; }
        public string? DeleteReason { get; set; }
        /// <summary>Pending | Approved | Rejected — null when not under exception hold.</summary>
        public string? ExceptionHoldStatus { get; set; }
        public Guid? ActiveExceptionId { get; set; }
        public List<SalesRequestTimelineItemDTO> Timeline { get; set; } = [];
        public List<SalesRequestHistoryDTO> History { get; set; } = [];
        public List<SalesRequestNameTransferDTO> NameTransfers { get; set; } = [];
        public SalesRequestNameTransferDTO? LatestNameTransfer { get; set; }
    }

    public sealed class SalesRequestNameTransferDTO
    {
        public int Id { get; set; }
        public int SaleRequestId { get; set; }
        public int FromEmployeeId { get; set; }
        public string? FromEmployeeName { get; set; }
        public int ToEmployeeId { get; set; }
        public string? ToEmployeeName { get; set; }
        public string TransferReason { get; set; } = string.Empty;
        public DateTime TransferredAtUtc { get; set; }
        public int TransferredByUserId { get; set; }
        public string? TransferredByName { get; set; }
    }

    public sealed class SalesRequestSoftDeleteDTO
    {
        public string? Reason { get; set; }
        /// <summary>
        /// Required when the linked sale has payments: confirms deleting those payments + sale + stock restore.
        /// </summary>
        public bool ConfirmCascadePayments { get; set; }
    }

    public sealed class SalesRequestSoftDeleteImpactDTO
    {
        public int RequestId { get; set; }
        public bool HasLinkedDraft { get; set; }
        public int? LinkedDraftSaleId { get; set; }
        public string? DraftStatus { get; set; }
        public string? PostingStatus { get; set; }
        public bool IsPosted { get; set; }
        public int? CustomerId { get; set; }
        public int? CustomerSaleId { get; set; }
        public int PaymentCount { get; set; }
        public decimal PaymentTotal { get; set; }
        public bool RequiresPaymentConfirm { get; set; }
        public bool WillDeleteOfficialSale { get; set; }
        public bool WillRestoreInventory { get; set; }
        public string WarningAr { get; set; } = string.Empty;
    }

    public sealed class SalesRequestTransferDTO
    {
        public int ToEmployeeId { get; set; }
        public string? TransferReason { get; set; }
        public string? Reason
        {
            get => TransferReason;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    TransferReason = value;
                }
            }
        }
    }

    public sealed class SalesTransferPeerDTO
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
    }

    public sealed class SalesRequestTimelineItemDTO
    {
        public string Event { get; set; } = string.Empty;
        public DateTime? AtUtc { get; set; }
        public string? Detail { get; set; }
    }

    /// <summary>Paged manager board response — keeps 20k+ request branches under ~2s first paint.</summary>
    public sealed class SalesRequestManagerListPageDTO
    {
        public IReadOnlyList<SalesRequestDTO> Items { get; set; } = [];
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public bool HasMore { get; set; }
        public Dictionary<string, int> TabCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
