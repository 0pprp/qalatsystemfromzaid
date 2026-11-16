namespace BE_Company.Sales.DTO;

public sealed class SalesArchiveItemDto
{
    public int ArchiveId { get; set; }
    public int SaleId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string SaleDate { get; set; } = string.Empty;
    public decimal SalePrice { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string ListName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RefusalNote { get; set; }
    public string? CommitteeUser { get; set; }
    public int BundleId { get; set; }
    public int BundleNumber { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
}

public sealed class SalesArchiveBundleDto
{
    public int BundleId { get; set; }
    public int BundleNumber { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string PeriodStart { get; set; } = string.Empty;
    public string PeriodEnd { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public bool CanSend { get; set; }
    public string? RecipientName { get; set; }
    public int AtCommitteeCount { get; set; }
    public int ToArchiveCount { get; set; }
    public string? IntermediateName { get; set; }
    public string? IntermediatePhone { get; set; }
    public string? Note { get; set; }
    public DateTime? SentAt { get; set; }
    public bool HasPhoto { get; set; }
    public int PhotoCount { get; set; }
    public List<int> PhotoIds { get; set; } = [];
    public int ItemCount { get; set; }
    public int ApprovedCount { get; set; }
    public int RefusedCount { get; set; }
    public int PendingCount { get; set; }
    public List<SalesArchiveItemDto> Items { get; set; } = [];
}

public sealed class SalesArchiveBoardDto
{
    public List<SalesArchiveBundleDto> Current { get; set; } = [];
    public List<SalesArchiveBundleDto> Ready { get; set; } = [];
    public List<SalesArchiveBundleDto> InTransit { get; set; } = [];
    public List<SalesArchiveBundleDto> NeedsFix { get; set; } = [];
    public List<SalesArchiveBundleDto> Approved { get; set; } = [];
    public List<SalesArchiveBundleDto> AtCommittee { get; set; } = [];
    public List<SalesArchiveBundleDto> AtArchive { get; set; } = [];
}

public sealed class SalesArchiveIdsBody
{
    public List<int> ArchiveIds { get; set; } = [];
    public bool Accept { get; set; }
    public string? Note { get; set; }
}

public sealed class SalesArchiveReviewBody
{
    public List<SalesArchiveReviewDecisionBody> Decisions { get; set; } = [];
}

public sealed class SalesArchiveReviewDecisionBody
{
    public int ArchiveId { get; set; }
    public bool Approved { get; set; }
    public string? Note { get; set; }
}
