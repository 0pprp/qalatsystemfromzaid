namespace BE_Company.Sales.Services;

/// <summary>
/// Internal login classification. Never expose to public clients.
/// </summary>
public enum LoginDiagnosticCode
{
    LoginOk = 0,
    UserNotFound = 1,
    UserDisabled = 2,
    InvalidCredentials = 3,
    WrongRole = 4,
    DuplicateAmbiguous = 5,
}

public sealed class LoginCandidate
{
    public int UserId { get; init; }
    public string UserName { get; init; } = "";
    public string Password { get; init; } = "";
    public string UserType { get; init; } = "";
    public bool IsActive { get; init; }
}

public static class UserLoginDiagnostics
{
    public static bool IsActiveUserState(object? userState)
    {
        if (userState is null)
        {
            return false;
        }

        if (userState is bool b)
        {
            return b;
        }

        if (userState is byte by)
        {
            return by != 0;
        }

        if (userState is short s)
        {
            return s != 0;
        }

        if (userState is int i)
        {
            return i != 0;
        }

        var text = Convert.ToString(userState)?.Trim() ?? "";
        return text.Equals("true", StringComparison.OrdinalIgnoreCase)
               || text.Equals("1", StringComparison.OrdinalIgnoreCase)
               || text.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Deterministic selection among rows sharing a username.
    /// Ambiguity is evaluated only among active rows that are eligible for this endpoint's roles
    /// and match the supplied credentials — never QueryFirstOrDefault roulette.
    /// </summary>
    public static (LoginDiagnosticCode Code, LoginCandidate? User) Classify(
        IReadOnlyList<LoginCandidate> rows,
        string? password,
        Func<string?, bool> roleAllowed)
    {
        if (rows.Count == 0)
        {
            return (LoginDiagnosticCode.UserNotFound, null);
        }

        var eligible = rows.Where(r => roleAllowed(r.UserType)).ToList();
        if (eligible.Count == 0)
        {
            return (LoginDiagnosticCode.WrongRole, null);
        }

        var activeEligible = eligible.Where(r => r.IsActive).ToList();
        if (activeEligible.Count == 0)
        {
            return (LoginDiagnosticCode.UserDisabled, null);
        }

        var passwordMatches = activeEligible
            .Where(r => string.Equals(r.Password, password ?? "", StringComparison.Ordinal))
            .ToList();

        if (passwordMatches.Count == 0)
        {
            return (LoginDiagnosticCode.InvalidCredentials, null);
        }

        if (passwordMatches.Count > 1)
        {
            return (LoginDiagnosticCode.DuplicateAmbiguous, null);
        }

        return (LoginDiagnosticCode.LoginOk, passwordMatches[0]);
    }

    public static bool IsAdminLoginRole(string? userType) =>
        string.Equals(userType, Authorization.SalesRoles.UserTypeMainAccountant, StringComparison.Ordinal)
        || string.Equals(userType, Authorization.SalesRoles.UserTypeBranchManager, StringComparison.Ordinal);

    public static bool IsEmployeeLoginRole(string? userType) =>
        string.Equals(userType, Authorization.SalesRoles.UserTypeSubAccountant, StringComparison.Ordinal)
        || string.Equals(userType, Authorization.SalesRoles.UserTypeBranchManager, StringComparison.Ordinal)
        || string.Equals(userType, Authorization.SalesRoles.UserTypeSalesEmployee, StringComparison.Ordinal)
        || string.Equals(userType, Authorization.SalesRoles.UserTypeSalesManager, StringComparison.Ordinal)
        || string.Equals(userType, Authorization.SalesRoles.UserTypeSalesFilterEmployee, StringComparison.Ordinal);
}
