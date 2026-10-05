using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services;

public static class LegalFollowupStatuses
{
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";

    public static string Arabic(string? status) => status switch
    {
        Completed => "تمت المتابعة",
        _ => "قيد العمل"
    };
}

public sealed class LegalFollowupCaseDTO
{
    public int Id { get; set; }
    public int SaleId { get; set; }
    public int? SalesRequestId { get; set; }
    public int? CustomerId { get; set; }
    public int FollowerUserId { get; set; }
    public string FollowerName { get; set; } = "";
    public string Status { get; set; } = LegalFollowupStatuses.InProgress;
    public string StatusLabel => LegalFollowupStatuses.Arabic(Status);
    public string CustomerName { get; set; } = "";
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? ShopImageKey { get; set; }
    public string? ShopImageUrl { get; set; }
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Note { get; set; }
    public string? CompletionNote { get; set; }
    public IReadOnlyList<LegalFollowupDocumentDTO> Documents { get; set; } = [];
}

public sealed class LegalFollowupDocumentDTO
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = "";
    public string TypeLabel { get; set; } = "";
    public string? FileName { get; set; }
    public string? DownloadUrl { get; set; }
}

public sealed class LegalFollowupCreateRequest
{
    public int SalesRequestId { get; set; }
    public int FollowerUserId { get; set; }
    public string? Note { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public int? ExistingCustomerId { get; set; }
}

public sealed class LegalFollowupCompleteRequest
{
    public string? Note { get; set; }
}

public interface ILegalFollowupService
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<LegalFollowupCaseDTO>> ListAsync(string? status, CancellationToken ct = default);
    Task<LegalFollowupCaseDTO?> GetAsync(int id, CancellationToken ct = default);
    Task<LegalFollowupCaseDTO> CreateAsync(LegalFollowupCreateRequest request, int actorUserId, string? actorName, CancellationToken ct = default);
    Task<IReadOnlyList<LegalFollowupCaseDTO>> ListForFollowerAsync(int followerUserId, string? status, CancellationToken ct = default);
    Task<LegalFollowupCaseDTO?> GetForFollowerAsync(int id, int followerUserId, CancellationToken ct = default);
    Task<LegalFollowupCaseDTO> CompleteForFollowerAsync(int id, int followerUserId, string? note, CancellationToken ct = default);
    Task<(string FileName, byte[] Bytes, string ContentType)?> ReadCaseDocumentAsync(int caseId, int documentId, CancellationToken ct = default);
}
