namespace BE_SalesEmployee.Services;

/// <summary>
/// Shared BranchProxy HttpClient timeout. Must stay above evaluate/excel CancelAfter (90s).
/// </summary>
public static class BranchProxyTimeouts
{
    public const int HttpClientSeconds = 120;
}
