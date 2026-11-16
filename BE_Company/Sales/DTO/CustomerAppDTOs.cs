namespace BE_Company.Sales.DTO
{
    // Gateway-only contracts for the customer store app (BE_CustomerApp → BE_SalesEmployee → branch).
    // All amounts are IQD integers taken from the branch's existing *Denar view columns.

    public sealed class CustomerAppItemDTO
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    public sealed class CustomerAppAccountDTO
    {
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        /// <summary>Latest sale date (View_CustomersDelegate.DateSaleDevice), branch local time.</summary>
        public DateTime? SaleDate { get; set; }
        public List<CustomerAppItemDTO> Items { get; set; } = [];
        public long TotalAmount { get; set; }
        public long DailyInstallment { get; set; }
        public long PaidAmount { get; set; }
        public long RemainingAmount { get; set; }
        public int PaymentsCount { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }

    public sealed class CustomerAppAccountsResponseDTO
    {
        public string? CityValue { get; set; }
        public string? CityName { get; set; }
        public List<CustomerAppAccountDTO> Accounts { get; set; } = [];
    }

    public sealed class CustomerAppSaleDTO
    {
        public int SaleId { get; set; }
        public DateTime? Date { get; set; }
        public List<CustomerAppItemDTO> Items { get; set; } = [];
        public long TotalAmount { get; set; }
        public long DailyInstallment { get; set; }
    }

    public sealed class CustomerAppPaymentDTO
    {
        public int PaymentId { get; set; }
        public DateTime? Date { get; set; }
        public long Amount { get; set; }
    }

    /// <summary>
    /// Payment progress of the customer account created when a sales draft (SalesDrafts.SaleId, the id stored in
    /// SalesRequests.ConvertedToSaleId) was posted. Each posted sale gets its own CustomerID, so the account is the sale.
    /// </summary>
    public sealed class CustomerAppSaleProgressDTO
    {
        public int SaleId { get; set; }
        public int CustomerId { get; set; }
        public long TotalAmount { get; set; }
        public long PaidAmount { get; set; }
        public long RemainingAmount { get; set; }
        public int PaymentsCount { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }

    public sealed class CustomerAppAccountDetailDTO
    {
        public CustomerAppAccountDTO Account { get; set; } = new();
        public List<CustomerAppSaleDTO> Sales { get; set; } = [];
        public List<CustomerAppPaymentDTO> Payments { get; set; } = [];
    }

    public sealed class CustomerAppStatusRequestDTO
    {
        public List<Guid>? OrderUuids { get; set; }
    }

    public sealed class CustomerAppStatusItemDTO
    {
        public Guid OrderUuid { get; set; }
        public bool Found { get; set; }
        public int? SalesRequestId { get; set; }
        public string? Status { get; set; }
        public string? FilterStatus { get; set; }
        public bool IsDeleted { get; set; }
        public string? RejectionReason { get; set; }
        public int? ConvertedToSaleId { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    public sealed class CustomerAppStatusResponseDTO
    {
        public List<CustomerAppStatusItemDTO> Items { get; set; } = [];
    }
}
