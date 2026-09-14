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
    /// Prefers a single active credential match; never QueryFirstOrDefault roulette.
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

        var active = rows.Where(r => r.IsActive).ToList();
        if (active.Count == 0)
        {
            return (LoginDiagnosticCode.UserDisabled, null);
        }

        var passwordMatches = active
            .Where(r => string.Equals(r.Password, password ?? "", StringComparison.Ordinal))
            .ToList();

        if (passwordMatches.Count == 0)
        {
            return (LoginDiagnosticCode.InvalidCredentials, null);
        }

        if (passwordMatches.Count > 1)
        {
            // Ambiguous only when multiple active rows share username+password.
            return (LoginDiagnosticCode.DuplicateAmbiguous, null);
        }

        var user = passwordMatches[0];
        if (!roleAllowed(user.UserType))
        {
            return (LoginDiagnosticCode.WrongRole, null);
        }

        return (LoginDiagnosticCode.LoginOk, user);
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
