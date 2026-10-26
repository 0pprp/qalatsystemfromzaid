using System.IO;
using System.Linq;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

/// <summary>
/// Intake HTTP must accept the request and return quickly.
/// Box posting belongs to BE_Company hosted PostEligible sweep — never block the مندوب on a 120s SP.
/// </summary>
public sealed class CollectionPaymentIntakeNoSyncPostEligibleTests
{
    [Fact]
    public void PostPaymentIdempotent_Source_DoesNotInvokePostEligibleCatchUp()
    {
        var repo = LocateRepositorySource();
        var text = File.ReadAllText(repo);

        Assert.Contains("PostPaymentIdempotentAsync", text);
        Assert.DoesNotContain("await TryPostEligibleCatchUpAsync(", text);
        Assert.DoesNotContain("CustomersPaymentsRequest_PostEligible", text);
    }

    private static string LocateRepositorySource()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "BE_DelegateWebApplication",
                "Repository",
                "CustomersPaymentsRequestsRepository.cs");
            if (File.Exists(candidate))
                return candidate;

            var nested = Path.Combine(
                dir.FullName,
                "BE_DelegateWebApplication",
                "BE_DelegateWebApplication",
                "Repository",
                "CustomersPaymentsRequestsRepository.cs");
            if (File.Exists(nested))
                return nested;

            dir = dir.Parent;
        }

        // Fallback relative to test assembly location (bin/Debug/netX/)
        var fromAsm = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "BE_DelegateWebApplication",
            "Repository",
            "CustomersPaymentsRequestsRepository.cs"));
        if (File.Exists(fromAsm))
            return fromAsm;

        throw new FileNotFoundException("CustomersPaymentsRequestsRepository.cs not found");
    }
}
