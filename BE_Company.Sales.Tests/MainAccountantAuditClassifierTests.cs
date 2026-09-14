using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public sealed class MainAccountantAuditClassifierTests
{
    [Theory]
    [InlineData(LoginDiagnosticCode.LoginOk, "ACTIVE_LOGIN_OK")]
    [InlineData(LoginDiagnosticCode.UserDisabled, "DISABLED_EXPECTED")]
    [InlineData(LoginDiagnosticCode.DuplicateAmbiguous, "DUPLICATE_AMBIGUOUS")]
    [InlineData(LoginDiagnosticCode.WrongRole, "WRONG_ROLE")]
    [InlineData(LoginDiagnosticCode.UserNotFound, "AUTH_FAILURE")]
    [InlineData(LoginDiagnosticCode.InvalidCredentials, "AUTH_FAILURE")]
    public void Maps_Diagnostic_To_Audit_Label(LoginDiagnosticCode code, string expected) =>
        Assert.Equal(expected, MainAccountantAuditClassifier.Classify(code));

    [Fact]
    public void Branch_Unavailable_Wins() =>
        Assert.Equal(
            "BRANCH_UNAVAILABLE",
            MainAccountantAuditClassifier.Classify(LoginDiagnosticCode.LoginOk, branchUnavailable: true));
}
