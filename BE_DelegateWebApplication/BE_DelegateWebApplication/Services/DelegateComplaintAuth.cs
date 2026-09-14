using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using Microsoft.AspNetCore.Http;

namespace BE_DelegateWebApplication.Services;

/// <summary>
/// Resolves complaint sender identity from AsyncId credential only.
/// Never trusts client-supplied DelegateId / SenderName / BranchLink.
/// </summary>
public static class DelegateComplaintAuth
{
    public const string AsyncIdHeader = "X-Async-Id";
    public const string ExpectedDelegateIdHeader = "X-Delegate-Id";

    public sealed class AuthResult
    {
        public int StatusCode { get; init; }
        public string? ErrorMessage { get; init; }
        public DelegateGetDTO? Delegate { get; init; }
        public bool Ok => StatusCode == StatusCodes.Status200OK && Delegate is not null;
    }

    /// <summary>
    /// Credential order: header X-Async-Id → query asyncId → body asyncId (legacy fallback).
    /// Optional X-Delegate-Id must match resolved DelegateId when present (session binding).
    /// </summary>
    public static async Task<AuthResult> AuthenticateAsync(
        IDelegateRepository delegates,
        string? asyncIdCredential,
        string? expectedDelegateIdHeader,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(asyncIdCredential))
        {
            return new AuthResult
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                ErrorMessage = "رمز المندوب مطلوب"
            };
        }

        var auth = await delegates.GetDelegateLogin(asyncIdCredential.Trim().TrimEnd('/'));
        if (auth is null || auth.DelegateId <= 0)
        {
            return new AuthResult
            {
                StatusCode = StatusCodes.Status401Unauthorized,
                ErrorMessage = "رمز المندوب غير صحيح"
            };
        }

        if (!string.IsNullOrWhiteSpace(expectedDelegateIdHeader)
            && int.TryParse(expectedDelegateIdHeader.Trim(), out var expectedId)
            && expectedId > 0
            && expectedId != auth.DelegateId)
        {
            // Session prefs claim a different DelegateId than AsyncId resolves to.
            return new AuthResult
            {
                StatusCode = StatusCodes.Status403Forbidden,
                ErrorMessage = "هوية الجلسة لا تطابق المندوب المصادق"
            };
        }

        return new AuthResult
        {
            StatusCode = StatusCodes.Status200OK,
            Delegate = auth
        };
    }

    public static string? ReadAsyncIdCredential(HttpRequest request, string? bodyAsyncId)
    {
        if (request.Headers.TryGetValue(AsyncIdHeader, out var headerVals))
        {
            var h = headerVals.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(h))
            {
                return h;
            }
        }

        if (request.Query.TryGetValue("asyncId", out var q) && !string.IsNullOrWhiteSpace(q))
        {
            return q.ToString();
        }

        return bodyAsyncId;
    }

    public static string? ReadExpectedDelegateId(HttpRequest request) =>
        request.Headers.TryGetValue(ExpectedDelegateIdHeader, out var vals)
            ? vals.FirstOrDefault()
            : null;

    /// <summary>Server Host only — never from client body LinkDelegate.</summary>
    public static string? BranchLinkFromRequest(HttpRequest request)
    {
        var host = request.Host.Value;
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        return host.Length <= 500 ? host : host[..500];
    }

    /// <summary>Maps authenticated delegate → persisted complaint identity fields.</summary>
    public static (int DelegateId, int? UserId, string SenderDisplayName, int? CityId) MapSender(
        DelegateGetDTO auth) =>
        (
            auth.DelegateId,
            auth.UserId is > 0 ? auth.UserId : null,
            string.IsNullOrWhiteSpace(auth.DelegateName) ? "مندوب" : auth.DelegateName.Trim(),
            auth.CityId is > 0 ? auth.CityId : null
        );
}
