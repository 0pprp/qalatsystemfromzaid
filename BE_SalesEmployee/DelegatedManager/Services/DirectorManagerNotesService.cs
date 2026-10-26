using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class TrustedDirectorNoteSender
{
    public string? SenderUserId { get; init; }
    public string? SenderUserName { get; init; }
    public string? SenderDisplayName { get; init; }
    public string? SenderRole { get; init; }
    public string? CityValue { get; init; }
    public string? CityName { get; init; }
}

public sealed class CreateDirectorNoteInput
{
    public string? Subject { get; init; }
    public string? Body { get; init; }
    public string? SourceApp { get; init; }
    public string? SourceType { get; init; }
    public string? MetadataJson { get; init; }
}

public sealed class CreateDirectorNoteResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public DirectorManagerNote? Note { get; init; }

    public static CreateDirectorNoteResult Invalid(string error) => new() { Ok = false, Error = error };
    public static CreateDirectorNoteResult Created(DirectorManagerNote note) => new() { Ok = true, Note = note };
}

/// <summary>Director (حيدر) manager-notes inbox. Isolated from CentralComplaints / المدير المفوض.</summary>
public sealed class DirectorManagerNotesService
{
    public const int MaxSubjectLength = 256;
    public const int MinBodyLength = 10;
    public const int MaxBodyLength = 2000;
    public const string DefaultSourceApp = "sales-gateway";
    public const string DefaultSubject = "ملاحظة إلى المدير";
    public const string DefaultSourceType = "manager_note";

    private readonly IDirectorManagerNoteStore _store;

    public DirectorManagerNotesService(IDirectorManagerNoteStore store)
    {
        _store = store;
    }

    public async Task<CreateDirectorNoteResult> CreateTrustedAsync(
        TrustedDirectorNoteSender sender,
        CreateDirectorNoteInput input,
        CancellationToken ct = default)
    {
        var body = input.Body?.Trim() ?? "";
        if (body.Length < MinBodyLength)
            return CreateDirectorNoteResult.Invalid($"نص الملاحظة قصير جدًا (الحد الأدنى {MinBodyLength} أحرف)");
        if (body.Length > MaxBodyLength)
            return CreateDirectorNoteResult.Invalid($"الحد الأقصى للملاحظة {MaxBodyLength} حرف");

        body = body.Replace('<', ' ').Replace('>', ' ');

        var subject = string.IsNullOrWhiteSpace(input.Subject) ? DefaultSubject : input.Subject.Trim();
        if (subject.Length > MaxSubjectLength)
            subject = subject[..MaxSubjectLength];

        var note = new DirectorManagerNote
        {
            Id = Guid.NewGuid(),
            SourceApp = Trimmed(input.SourceApp) ?? DefaultSourceApp,
            SourceType = Trimmed(input.SourceType) ?? DefaultSourceType,
            SenderUserId = Trimmed(sender.SenderUserId),
            SenderUserName = Trimmed(sender.SenderUserName),
            SenderDisplayName = Trimmed(sender.SenderDisplayName)
                                ?? Trimmed(sender.SenderUserName)
                                ?? Trimmed(sender.SenderUserId)
                                ?? "غير معروف",
            SenderRole = Trimmed(sender.SenderRole) ?? "Unknown",
            CityValue = Trimmed(sender.CityValue),
            CityName = Trimmed(sender.CityName),
            Subject = subject,
            Body = body,
            MetadataJson = Trimmed(input.MetadataJson),
            CreatedAtUtc = DateTime.UtcNow
        };

        var created = await _store.CreateAsync(note, ct);
        return CreateDirectorNoteResult.Created(created);
    }

    public Task<PagedResult<DirectorManagerNote>> InboxAsync(DirectorNoteInboxQuery query, CancellationToken ct = default)
    {
        var (page, pageSize) = PagingDefaults.Normalize(query.Page, query.PageSize);
        return _store.QueryAsync(new DirectorNoteInboxQuery
        {
            Page = page,
            PageSize = pageSize,
            UnreadOnly = query.UnreadOnly,
            CityValue = query.CityValue,
            SourceApp = query.SourceApp,
            Search = query.Search
        }, ct);
    }

    public Task<DirectorManagerNote?> GetAsync(Guid id, CancellationToken ct = default) => _store.GetAsync(id, ct);

    public Task<DirectorManagerNote?> MarkReadAsync(Guid id, CancellationToken ct = default) =>
        _store.MarkReadAsync(id, DateTime.UtcNow, ct);

    public Task<int> MarkAllReadAsync(CancellationToken ct = default) =>
        _store.MarkAllReadAsync(DateTime.UtcNow, ct);

    public Task<int> UnreadCountAsync(CancellationToken ct = default) => _store.CountUnreadAsync(ct);

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
