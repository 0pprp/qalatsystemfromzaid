using BE_Company.DTO;

namespace BE_Company.Sales.DTO
{
    public sealed class SalesPurchaseLineDTO
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public double ItemCostDenar { get; set; }
        public double TotalItemCostDenar { get; set; }
    }

    public sealed class SalesPurchaseCreateDTO
    {
        public int SupplierId { get; set; }
        public int StoreId { get; set; }
        public int BoxId { get; set; }
        public DateTime Date { get; set; }
        public string SupplierInvoiceNumber { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public double TotalAmountSpent { get; set; }
        public double FinalTotalItemCostDenar { get; set; }
        public double AmountTotalDenar { get; set; }
        public double RemainingAmountDenar { get; set; }
        public List<SalesPurchaseLineDTO> Contents { get; set; } = [];
    }

    public sealed class SalesPurchaseListItemDTO
    {
        public int BuyId { get; set; }
        public int? BoundNumber { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? StoreName { get; set; }
        public string? BoxName { get; set; }
        public string? ItemsNames { get; set; }
        public DateTime? DateCreate { get; set; }
        public int? NumberOfItemsBuys { get; set; }
        public double? TotalAmountDenar { get; set; }
        public double? AmountSpentDenar { get; set; }
        public string? SupplierInvoiceNumber { get; set; }
        public string? Notes { get; set; }
        public string? CreatedByUserName { get; set; }
        public string? CreatedByDisplayName { get; set; }
        public string? CreatedByUserType { get; set; }
        public string? CreatedByBranchId { get; set; }
        public string? CreatedByBranchName { get; set; }
        public DateTime? CreatedAtUtc { get; set; }
    }

    public sealed class SalesPurchaseLookupsDTO
    {
        public List<SuppliersGetDTO> Suppliers { get; set; } = [];
        public List<StoresDataGetDTO> Stores { get; set; } = [];
        public List<BoxsGetDTO> Boxes { get; set; } = [];
    }

    public sealed class SalesPurchaseItemOptionDTO
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public double ItemCostDenar { get; set; }
        public double ItemPriceDenar { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }

    public sealed class SalesPurchaseCreateCommand
    {
        public required BuysPostDTO Buy { get; init; }
        public required string SupplierInvoiceNumber { get; init; }
        public string? Notes { get; init; }
        public required string CreatedByUserName { get; init; }
        public required string CreatedByDisplayName { get; init; }
        public required string CreatedByUserType { get; init; }
        public required string BranchId { get; init; }
        public required string BranchName { get; init; }
        public int PreferredUserId { get; init; }
    }
}
