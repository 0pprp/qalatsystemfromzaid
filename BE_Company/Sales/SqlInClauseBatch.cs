namespace BE_Company.Sales;

/// <summary>
/// Safe chunking for SQL Server IN (@Ids) queries.
/// SQL Server allows at most 2100 parameters per request; Dapper expands each
/// array element to one parameter, so batches must stay well below that limit.
/// </summary>
public static class SqlInClauseBatch
{
    /// <summary>
    /// Default batch size with margin under the 2100 SQL parameter ceiling
    /// (leaves room for other command parameters).
    /// </summary>
    public const int DefaultSafeSize = 900;

    public const int SqlServerMaxParameters = 2100;

    public static IEnumerable<int[]> Chunk(
        IEnumerable<int> ids,
        int batchSize = DefaultSafeSize)
    {
        if (batchSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be positive.");
        }

        if (batchSize >= SqlServerMaxParameters)
        {
            throw new ArgumentOutOfRangeException(
                nameof(batchSize),
                $"Batch size must be below SQL Server max parameters ({SqlServerMaxParameters}).");
        }

        List<int>? buffer = null;
        var seen = new HashSet<int>();
        foreach (var id in ids)
        {
            if (id <= 0 || !seen.Add(id))
            {
                continue;
            }

            buffer ??= new List<int>(Math.Min(batchSize, 64));
            buffer.Add(id);
            if (buffer.Count >= batchSize)
            {
                yield return buffer.ToArray();
                buffer = null;
            }
        }

        if (buffer is { Count: > 0 })
        {
            yield return buffer.ToArray();
        }
    }
}
