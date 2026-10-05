namespace BE_DelegateWebApplication.Services;

public static class FollowerFieldFollowupRules
{
    public const string StatusPending = "Pending";
    public const string StatusCompleted = "Completed";

    public static bool IsValidCompletionNote(string? note) =>
        !string.IsNullOrWhiteSpace(note) && note.Trim().Length >= 1;

    public static string NormalizeNote(string? note) => (note ?? string.Empty).Trim();
}

public sealed class FollowerFieldFollowupRow
{
    public int Id { get; set; }
    public int FollowerUserId { get; set; }
    public int ListId { get; set; }
    public string? ListName { get; set; }
    public int CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = FollowerFieldFollowupRules.StatusPending;
    public string? CompletionNote { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public interface IFollowerFieldFollowupService
{
    Task<FollowerFieldFollowupRow> AddAsync(
        int followerUserId,
        int listId,
        string? listName,
        int customerId,
        string? customerName,
        string? phone,
        CancellationToken ct);

    Task<IReadOnlyList<FollowerFieldFollowupRow>> ListPendingAsync(int followerUserId, CancellationToken ct);

    Task<FollowerFieldFollowupRow> CompleteAsync(int id, int followerUserId, string? note, CancellationToken ct);

    /// <summary>Add (if needed) then complete with note — for home list cards.</summary>
    Task<FollowerFieldFollowupRow> RecordCompleteAsync(
        int followerUserId,
        int listId,
        string? listName,
        int customerId,
        string? customerName,
        string? phone,
        string? note,
        CancellationToken ct);

    Task<IReadOnlyList<FollowerFieldFollowupLatestNote>> ListLatestCompletedNotesAsync(
        int followerUserId,
        int listId,
        CancellationToken ct);
}

public sealed class FollowerFieldFollowupLatestNote
{
    public int CustomerId { get; set; }
    public string? Note { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
