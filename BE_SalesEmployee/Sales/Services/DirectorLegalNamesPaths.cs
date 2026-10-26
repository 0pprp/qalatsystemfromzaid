using System.Globalization;
using System.Text;

namespace BE_SalesEmployee.Sales.Services;

/// <summary>BE_Company CustomerDecisions paths for monitor/director legal-names read proxies.</summary>
public static class DirectorLegalNamesPaths
{
    public static string WeakWeekPayers() => "CustomerDecisions/WeakWeekPayers";

    public static string LegalConversionRequests(string? status, int take, int skip)
    {
        var q = new StringBuilder("CustomerDecisions/LegalConversionRequests?");
        q.Append(CultureInfo.InvariantCulture, $"take={take}&skip={skip}");
        if (!string.IsNullOrWhiteSpace(status))
            q.Append(CultureInfo.InvariantCulture, $"&status={Uri.EscapeDataString(status.Trim())}");
        return q.ToString();
    }

    public static string LegalConversionApprove(int requestId) =>
        $"CustomerDecisions/LegalConversionRequests/{requestId}/Approve";

    public static string LegalConversionReject(int requestId) =>
        $"CustomerDecisions/LegalConversionRequests/{requestId}/Reject";

    public static string WaiverAccountantRequests(string? status, int take, int skip)
    {
        var q = new StringBuilder("CustomerDecisions/WaiverAccountantRequests?");
        q.Append(CultureInfo.InvariantCulture, $"take={take}&skip={skip}");
        if (!string.IsNullOrWhiteSpace(status))
            q.Append(CultureInfo.InvariantCulture, $"&status={Uri.EscapeDataString(status.Trim())}");
        return q.ToString();
    }

    public static string Decisions(string? decisionType, DateTime? fromDate, DateTime? toDate, int? customerId)
    {
        var q = new StringBuilder("CustomerDecisions/Decisions?");
        var first = true;
        void Add(string key, string value)
        {
            q.Append(first ? "" : "&");
            first = false;
            q.Append(CultureInfo.InvariantCulture, $"{key}={Uri.EscapeDataString(value)}");
        }

        if (!string.IsNullOrWhiteSpace(decisionType))
            Add("decisionType", decisionType.Trim());
        if (fromDate.HasValue)
            Add("fromDate", fromDate.Value.ToString("o", CultureInfo.InvariantCulture));
        if (toDate.HasValue)
            Add("toDate", toDate.Value.ToString("o", CultureInfo.InvariantCulture));
        if (customerId is > 0)
            Add("customerID", customerId.Value.ToString(CultureInfo.InvariantCulture));

        var s = q.ToString();
        return s.EndsWith('?') ? s[..^1] : s;
    }
}
