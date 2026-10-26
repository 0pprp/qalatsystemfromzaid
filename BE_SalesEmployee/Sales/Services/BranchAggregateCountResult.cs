using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace BE_SalesEmployee.Sales.Services;

/// <summary>
/// Explicit completeness metadata for multi-branch aggregates (F12 / F24 extraction).
/// Presentation/API maps this object; never treat partial sums as full totals.
/// </summary>
public sealed class BranchAggregateCountResult
{
    [JsonPropertyName("count")]
    public int Count { get; init; }

    [JsonPropertyName("complete")]
    public bool Complete { get; init; }

    [JsonPropertyName("succeededBranches")]
    public int SucceededBranches { get; init; }

    [JsonPropertyName("requestedBranches")]
    public int RequestedBranches { get; init; }

    [JsonPropertyName("failedBranches")]
    public string[] FailedBranches { get; init; } = [];

    [JsonPropertyName("partial")]
    public bool Partial => !Complete;

    public static BranchAggregateCountResult From(
        int total,
        int succeeded,
        int requested,
        ConcurrentBag<string> failed) =>
        new()
        {
            Count = total,
            SucceededBranches = succeeded,
            RequestedBranches = requested,
            FailedBranches = failed.ToArray(),
            Complete = failed.IsEmpty && succeeded == requested
        };
}
