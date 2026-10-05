using BE_SalesEmployee.Services;
using Xunit;

namespace BE_SalesEmployee.Tests;

/// <summary>
/// Guards the production regression where BranchProxy HttpClient timed out at 20s
/// while evaluate/excel CancelAfter is 90s, yielding 502 on multi-province evaluate.
/// </summary>
public class BranchProxyTimeoutGuardTests
{
    // Keep in sync with SalesManagerBranchAggregator.ExcelSearchTimeout.
    public const int EvaluateCancelAfterSeconds = 90;

    [Fact]
    public void Branch_proxy_http_timeout_outlives_evaluate_cancel_after()
    {
        Assert.True(
            BranchProxyTimeouts.HttpClientSeconds >= EvaluateCancelAfterSeconds,
            $"BranchProxy HttpClient timeout ({BranchProxyTimeouts.HttpClientSeconds}s) must be >= evaluate CancelAfter ({EvaluateCancelAfterSeconds}s).");
    }
}
