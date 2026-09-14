namespace BE_Company.Sales.Services;

/// <summary>
/// Maps internal login diagnostics to Production audit labels for Main Accountant.
/// Public API must still return a generic auth failure message.
/// </summary>
public static class MainAccountantAuditClassifier
{
    public const string ActiveLoginOk = "ACTIVE_LOGIN_OK";
    public const string DisabledExpected = "DISABLED_EXPECTED";
    public const string DuplicateAmbiguous = "DUPLICATE_AMBIGUOUS";
    public const string WrongRole = "WRONG_ROLE";
    public const string AuthFailure = "AUTH_FAILURE";
    public const string BranchUnavailable = "BRANCH_UNAVAILABLE";

    public static string Classify(LoginDiagnosticCode? code, bool branchUnavailable = false)
    {
        if (branchUnavailable)
        {
            return BranchUnavailable;
        }

        return code switch
        {
            LoginDiagnosticCode.LoginOk => ActiveLoginOk,
            LoginDiagnosticCode.UserDisabled => DisabledExpected,
            LoginDiagnosticCode.DuplicateAmbiguous => DuplicateAmbiguous,
            LoginDiagnosticCode.WrongRole => WrongRole,
            LoginDiagnosticCode.UserNotFound => AuthFailure,
            LoginDiagnosticCode.InvalidCredentials => AuthFailure,
            _ => AuthFailure
        };
    }
}
