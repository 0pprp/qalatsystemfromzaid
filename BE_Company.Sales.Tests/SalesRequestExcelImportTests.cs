using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SalesRequestExcelImportTests
{
    private static SalesIdentity Manager() => new()
    {
        EmployeeId = 90,
        EmployeeName = "مدير",
        BranchId = "najaf-demo",
        BranchName = "النجف",
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager
    };

    private static SalesRequestService Svc(FakeRequestRepository repo) =>
        new(repo, new FakeClock { UtcNow = DateTime.UtcNow });

    [Theory]
    [InlineData("7804924373", "07804924373")]
    [InlineData("07804924373", "07804924373")]
    [InlineData("7.804924373E9", "07804924373")]
    [InlineData("+9647804924373", "07804924373")]
    public async Task Import_Normalizes_Phone_Via_SalesPhoneNormalizer(string input, string expected)
    {
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(),
        [
            new SalesRequestImportRowDTO
            {
                RowNumber = 2,
                CustomerName = "صادق جعفر حنيو",
                Phone = input,
                Province = "النجف",
                Address = "الكوفة"
            }
        ], default);

        Assert.Equal(1, result.Saved);
        Assert.Equal(0, result.Failed);
        Assert.Equal(expected, repo.Rows.Single().CustomerPhone);
    }

    [Fact]
    public async Task Import_Bulk_Each_Row_Keeps_Own_Phone()
    {
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(),
        [
            new SalesRequestImportRowDTO { RowNumber = 2, CustomerName = "أ", Phone = "7804924373" },
            new SalesRequestImportRowDTO { RowNumber = 3, CustomerName = "ب", Phone = "07801112233" },
            new SalesRequestImportRowDTO { RowNumber = 4, CustomerName = "ج", Phone = "7812345678" }
        ], default);

        Assert.Equal(3, result.Saved);
        Assert.Equal(0, result.Failed);
        Assert.Equal(
            new[] { "07804924373", "07801112233", "07812345678" },
            repo.Rows.OrderBy(r => r.Id).Select(r => r.CustomerPhone).ToArray());
    }

    [Fact]
    public async Task Import_Rejects_Empty_Phone()
    {
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(),
        [
            new SalesRequestImportRowDTO { RowNumber = 5, CustomerName = "بدون هاتف", Phone = "  " }
        ], default);

        Assert.Equal(0, result.Saved);
        Assert.Equal(1, result.Failed);
        Assert.Empty(repo.Rows);
        Assert.Contains(result.Errors, e => e.RowNumber == 5 && e.Message.Contains("هاتف", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_Sets_NewCustomer_And_Leaves_SourceListId_Null()
    {
        // SourceListId is for follower/delegate list identity — not Excel rows.
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(),
        [
            new SalesRequestImportRowDTO
            {
                RowNumber = 2,
                CustomerName = "زبون",
                Phone = "07804924373",
                SaleType = "ثلاجة"
            }
        ], default);

        Assert.Equal(1, result.Saved);
        var row = repo.Rows.Single();
        Assert.Equal(SalesRequestSources.NewCustomer, row.CustomerSourceType);
        Assert.Null(row.SourceListId);
        Assert.Contains("ثلاجة", row.Notes);
    }
}
