using System.Collections.Concurrent;
using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class BranchNoteIntent
{
    public required Guid ExceptionRequestId { get; init; }
    public required string CityValue { get; init; }
    public int? CustomerId { get; init; }
    public required string Note { get; init; }
    public required string ActorDisplayName { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

/// <summary>
/// Posts the shared customer note on approval. MVP records the intent only; the branch call is wired later.
/// </summary>
public interface IBranchNotePoster
{
    Task<bool> PostApprovalNoteAsync(SalesExceptionRequest request, CancellationToken ct = default);
}

public sealed class IntentRecordingBranchNotePoster : IBranchNotePoster
{
    private readonly ConcurrentQueue<BranchNoteIntent> _intents = new();

    public IReadOnlyCollection<BranchNoteIntent> Intents => _intents.ToArray();

    public Task<bool> PostApprovalNoteAsync(SalesExceptionRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CityValue) || request.CustomerId is null)
        {
            return Task.FromResult(false);
        }

        _intents.Enqueue(new BranchNoteIntent
        {
            ExceptionRequestId = request.Id,
            CityValue = request.CityValue,
            CustomerId = request.CustomerId,
            Note = BuildNote(request),
            ActorDisplayName = request.DecisionMakerDisplayName ?? "",
            CreatedAtUtc = DateTime.UtcNow
        });
        return Task.FromResult(true);
    }

    private static string BuildNote(SalesExceptionRequest request)
    {
        var approver = string.IsNullOrWhiteSpace(request.DecisionMakerDisplayName)
            ? "المدير المفوض"
            : request.DecisionMakerDisplayName;
        var note = $"تمت الموافقة على طلب استثناء من {approver}. السبب: {request.Reason}";
        return string.IsNullOrWhiteSpace(request.DecisionNote)
            ? note
            : $"{note} — ملاحظة القرار: {request.DecisionNote}";
    }
}
