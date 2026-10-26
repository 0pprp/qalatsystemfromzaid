namespace BE_SalesEmployee.Services;

/// <summary>
/// Unified timeout policy (F17/F19). Do not raise these to hide branch slowness.
/// Failed / timed-out branches must surface as incomplete (see SumCountAsync metadata).
/// </summary>
public static class BranchProxyTimeouts
{
    /// <summary>HttpClient.Timeout for BranchProxy — must exceed longest CancelAfter (120s excel).</summary>
    public const int HttpClientSeconds = 150;

    /// <summary>Default per-branch CancelAfter for short fan-out reads.</summary>
    public static readonly TimeSpan DefaultBranch = TimeSpan.FromSeconds(8);

    /// <summary>Soft multi-branch fan-out CancelAfter (FanoutSoftDeadline.MultiBranchTimeout).</summary>
    public static readonly TimeSpan MultiBranchSoft = TimeSpan.FromMilliseconds(1400);

    /// <summary>Long list / rating-enriched fan-out.</summary>
    public static readonly TimeSpan LongListBranch = TimeSpan.FromSeconds(25);

    /// <summary>Excel / evaluate / large file paths.</summary>
    public static readonly TimeSpan ExcelOrEvaluate = TimeSpan.FromSeconds(120);

    /// <summary>SQL command timeout guidance for branch company APIs (seconds).</summary>
    public const int SqlCommandGuidanceSeconds = 30;
}
