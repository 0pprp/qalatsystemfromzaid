using BE_SalesEmployee.DelegatedManager.Models;

namespace BE_SalesEmployee.DelegatedManager.Services;

/// <summary>
/// Pushes ExceptionHoldStatus to the branch SalesRequests row via gateway fan-in.
/// </summary>
public interface IBranchExceptionHoldPoster
{
    /// <param name="holdStatus">Pending/Approved/Rejected, or null to clear the hold.</param>
    Task<bool> SyncHoldAsync(
        SalesExceptionRequest request,
        string? holdStatus,
        CancellationToken ct = default);
}

/// <summary>Test/demo no-op that records sync intents.</summary>
public sealed class IntentRecordingBranchExceptionHoldPoster : IBranchExceptionHoldPoster
{
    private readonly List<(Guid ExceptionId, int? SalesRequestId, string? Status)> _posts = new();

    public IReadOnlyList<(Guid ExceptionId, int? SalesRequestId, string? Status)> Posts => _posts;

    public Task<bool> SyncHoldAsync(
        SalesExceptionRequest request,
        string? holdStatus,
        CancellationToken ct = default)
    {
        _posts.Add((request.Id, request.SalesRequestId, holdStatus));
        return Task.FromResult(request.SalesRequestId is > 0);
    }
}
