namespace BE_SalesEmployee.DelegatedManager.Domain;

public static class ComplaintStatuses
{
    public const string Unread = "Unread";
    public const string Read = "Read";
}

public static class ExceptionStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";
}

public static class TargetApproverTypes
{
    public const string DelegatedManager = "DelegatedManager";
    public const string BranchManager = "BranchManager";
}

/// <summary>Server-side exception state machine. Invalid transitions return false.</summary>
public static class ExceptionStateMachine
{
    public static bool CanTransition(string? from, string? to)
    {
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return false;
        }

        if (string.Equals(from, to, StringComparison.Ordinal))
        {
            return false;
        }

        return from switch
        {
            ExceptionStatuses.Pending => to is ExceptionStatuses.Approved
                or ExceptionStatuses.Rejected
                or ExceptionStatuses.Cancelled,
            _ => false
        };
    }
}

/// <summary>Mobile update decision from release metadata vs installed versionCode.</summary>
public enum MobileUpdateKind
{
    None,
    Optional,
    Mandatory
}

public static class MobileUpdateRules
{
    public static MobileUpdateKind Evaluate(
        int currentVersionCode,
        int latestVersionCode,
        int minimumSupportedVersionCode,
        bool forceUpdate)
    {
        if (currentVersionCode >= latestVersionCode)
        {
            return MobileUpdateKind.None;
        }

        if (currentVersionCode < minimumSupportedVersionCode)
        {
            return MobileUpdateKind.Mandatory;
        }

        if (forceUpdate)
        {
            return MobileUpdateKind.Mandatory;
        }

        return MobileUpdateKind.Optional;
    }
}
