using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Services;

namespace BE_SalesEmployee.DelegatedManager.Services;

/// <summary>Body fields a caller may supply. Sender identity is never taken from here.</summary>
public sealed class CreateComplaintInput
{
    public string? Subject { get; init; }
    public string? Body { get; init; }
    public string? CityValue { get; init; }
    public string? CityName { get; init; }
    public string? SourceApp { get; init; }
    public string? SourceType { get; init; }
    public string? MetadataJson { get; init; }
}

public sealed class CreateComplaintResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public CentralComplaint? Complaint { get; init; }

    public static CreateComplaintResult Invalid(string error) => new() { Ok = false, Error = error };
    public static CreateComplaintResult Created(CentralComplaint complaint) => new() { Ok = true, Complaint = complaint };
}

public sealed class CentralComplaintsService
{
    public const int MaxSubjectLength = 256;
    public const string DefaultSourceApp = "sales-gateway";

    private readonly ICentralComplaintsStore _store;

    public CentralComplaintsService(ICentralComplaintsStore store)
    {
        _store = store;
    }

    /// <summary>Creates an inbox row from the authenticated caller. Body identity fields are ignored on purpose.</summary>
    public async Task<CreateComplaintResult> CreateAsync(
        GatewayUser sender,
        CreateComplaintInput input,
        CancellationToken ct = default)
    {
        var subject = input.Subject?.Trim() ?? "";
        var body = input.Body?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(subject))
        {
            return CreateComplaintResult.Invalid("العنوان مطلوب");
        }
        if (string.IsNullOrWhiteSpace(body))
        {
            return CreateComplaintResult.Invalid("نص الشكوى مطلوب");
        }
        if (subject.Length > MaxSubjectLength)
        {
            subject = subject[..MaxSubjectLength];
        }

        var cityValue = Trimmed(input.CityValue) ?? Trimmed(sender.CityValue);
        var cityName = Trimmed(input.CityName) ?? Trimmed(sender.CityName);

        var complaint = new CentralComplaint
        {
            Id = Guid.NewGuid(),
            SourceApp = Trimmed(input.SourceApp) ?? DefaultSourceApp,
            SourceType = Trimmed(input.SourceType),
            SenderUserId = Trimmed(sender.UserID),
            SenderUserName = Trimmed(sender.UserName),
            SenderDisplayName = Trimmed(sender.UserName) ?? Trimmed(sender.UserID) ?? "غير معروف",
            SenderRole = SalesRoles.ToModuleRole(sender.UserType) ?? Trimmed(sender.UserType) ?? "Unknown",
            CityValue = cityValue,
            CityName = cityName,
            Subject = subject,
            Body = body,
            MetadataJson = Trimmed(input.MetadataJson),
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await _store.CreateAsync(complaint, ct);
        return CreateComplaintResult.Created(created);
    }

    public Task<PagedResult<CentralComplaint>> InboxAsync(ComplaintInboxQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        return _store.QueryAsync(new ComplaintInboxQuery
        {
            Page = page,
            PageSize = pageSize,
            UnreadOnly = query.UnreadOnly,
            CityValue = query.CityValue,
            SourceApp = query.SourceApp,
            Search = query.Search
        }, ct);
    }

    public Task<CentralComplaint?> GetAsync(Guid id, CancellationToken ct = default) => _store.GetAsync(id, ct);

    public Task<CentralComplaint?> MarkReadAsync(Guid id, CancellationToken ct = default) =>
        _store.MarkReadAsync(id, DateTime.UtcNow, ct);

    public Task<int> MarkAllReadAsync(CancellationToken ct = default) =>
        _store.MarkAllReadAsync(DateTime.UtcNow, ct);

    public Task<int> UnreadCountAsync(CancellationToken ct = default) => _store.CountUnreadAsync(ct);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
