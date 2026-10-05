namespace BE_SalesEmployee.Sales.Services;

/// <summary>
/// Soft-deadline merge for multi-branch Sales Manager list fan-out.
/// Goal: first paint around 1s for cheap lists — longer window for rating-enriched lists.
/// </summary>
public static class FanoutSoftDeadline
{
    /// <summary>Return whatever branches finished within this window.</summary>
    public static readonly TimeSpan SoftWait = TimeSpan.FromMilliseconds(900);

    /// <summary>If soft wait yields nothing, wait this much longer once.</summary>
    public static readonly TimeSpan ExtendIfEmpty = TimeSpan.FromMilliseconds(1100);

    /// <summary>Per-branch CancelAfter when fanning out to many cities.</summary>
    public static readonly TimeSpan MultiBranchTimeout = TimeSpan.FromMilliseconds(1400);

    /// <summary>Soft wait for employees/followers (rating attach + optional GPS proxy).</summary>
    public static readonly TimeSpan LongListSoftWait = TimeSpan.FromSeconds(4);

    /// <summary>Extra wait if soft wait still empty for long lists.</summary>
    public static readonly TimeSpan LongListExtendIfEmpty = TimeSpan.FromSeconds(8);

    /// <summary>Per-branch CancelAfter for rating-enriched list fan-out (wait-all mode).</summary>
    public static readonly TimeSpan LongListBranchTimeout = TimeSpan.FromSeconds(25);

    public static async Task<List<TItem>> CollectAsync<TChunk, TItem>(
        IReadOnlyList<Task<TChunk>> tasks,
        TimeSpan softWait,
        TimeSpan extendIfEmpty,
        Func<TChunk, bool> hasData,
        Func<TChunk, IEnumerable<TItem>> flatten,
        CancellationToken ct)
    {
        if (tasks.Count == 0)
        {
            return [];
        }

        var whenAll = Task.WhenAll(tasks);
        if (softWait <= TimeSpan.Zero)
        {
            await whenAll.ConfigureAwait(false);
            return FlattenCompleted(tasks, flatten);
        }

        await Task.WhenAny(whenAll, Task.Delay(softWait, ct)).ConfigureAwait(false);

        if (!whenAll.IsCompleted && !AnyCompletedHasData(tasks, hasData))
        {
            var extend = extendIfEmpty <= TimeSpan.Zero ? TimeSpan.Zero : extendIfEmpty;
            if (extend > TimeSpan.Zero)
            {
                await Task.WhenAny(whenAll, Task.Delay(extend, ct)).ConfigureAwait(false);
            }
        }

        return FlattenCompleted(tasks, flatten);
    }

    /// <summary>Convenience overload when chunk is already an enumerable of items.</summary>
    public static Task<List<TItem>> CollectAsync<TItem>(
        IReadOnlyList<Task<IEnumerable<TItem>>> tasks,
        TimeSpan softWait,
        TimeSpan extendIfEmpty,
        Func<IEnumerable<TItem>, bool> hasData,
        CancellationToken ct) =>
        CollectAsync(tasks, softWait, extendIfEmpty, hasData, chunk => chunk, ct);

    private static bool AnyCompletedHasData<TChunk>(
        IReadOnlyList<Task<TChunk>> tasks,
        Func<TChunk, bool> hasData)
    {
        foreach (var task in tasks)
        {
            if (task.IsCompletedSuccessfully && hasData(task.Result))
            {
                return true;
            }
        }

        return false;
    }

    private static List<TItem> FlattenCompleted<TChunk, TItem>(
        IReadOnlyList<Task<TChunk>> tasks,
        Func<TChunk, IEnumerable<TItem>> flatten)
    {
        var merged = new List<TItem>();
        foreach (var task in tasks)
        {
            if (!task.IsCompletedSuccessfully)
            {
                continue;
            }

            merged.AddRange(flatten(task.Result));
        }

        return merged;
    }
}
