using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests;

public class SalesRequestExcelImportTests
{
    private static SalesIdentity Manager(
        string branchId = "najaf-demo",
        string branchName = "النجف",
        bool isGateway = true) => new()
    {
        EmployeeId = 90,
        EmployeeName = "مدير",
        BranchId = branchId,
        BranchName = branchName,
        Role = SalesRoles.SalesManager,
        UserType = SalesRoles.UserTypeSalesManager,
        IsGateway = isGateway
    };

    private static SalesRequestService Svc(FakeRequestRepository repo) =>
        new(repo, new FakeClock { UtcNow = DateTime.UtcNow });

    private static SalesRequestImportDTO Import(
        params SalesRequestImportRowDTO[] rows) => new() { Rows = rows.ToList() };

    private static SalesRequestImportRowDTO Row(
        int n,
        string name,
        string phone,
        string province,
        string cityValue,
        string cityName,
        string? address = null,
        string? saleType = null) => new()
    {
        RowNumber = n,
        CustomerName = name,
        Phone = phone,
        Province = province,
        Address = address,
        SaleType = saleType,
        CityValue = cityValue,
        CityName = cityName
    };

    [Theory]
    [InlineData("7804924373", "07804924373")]
    [InlineData("07804924373", "07804924373")]
    [InlineData("7.804924373E9", "07804924373")]
    [InlineData("+9647804924373", "07804924373")]
    public async Task Import_Normalizes_Phone_Via_SalesPhoneNormalizer(string input, string expected)
    {
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(), Import(
            Row(2, "صادق جعفر حنيو", input, "النجف", "najaf-demo", "النجف", "الكوفة")), default);

        Assert.Equal(1, result.Saved);
        Assert.Equal(0, result.Failed);
        Assert.Equal(expected, repo.Rows.Single().CustomerPhone);
    }

    [Fact]
    public async Task Import_Bulk_Each_Row_Keeps_Own_Phone()
    {
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(), Import(
            Row(2, "أ", "7804924373", "النجف", "najaf-demo", "النجف"),
            Row(3, "ب", "07801112233", "النجف", "najaf-demo", "النجف"),
            Row(4, "ج", "7812345678", "النجف", "najaf-demo", "النجف")), default);

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
        var result = await Svc(repo).ImportRowsAsync(Manager(), Import(
            Row(5, "بدون هاتف", "  ", "النجف", "najaf-demo", "النجف")), default);

        Assert.Equal(0, result.Saved);
        Assert.Equal(1, result.Failed);
        Assert.Empty(repo.Rows);
        Assert.Contains(result.Errors, e => e.RowNumber == 5 && e.Message.Contains("هاتف", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_Sets_NewCustomer_And_Leaves_SourceListId_Null()
    {
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(), Import(
            Row(2, "زبون", "07804924373", "النجف", "najaf-demo", "النجف", saleType: "ثلاجة")), default);

        Assert.Equal(1, result.Saved);
        var row = repo.Rows.Single();
        Assert.Equal(SalesRequestSources.NewCustomer, row.CustomerSourceType);
        Assert.Null(row.SourceListId);
        Assert.Contains("ثلاجة", row.Notes);
    }

    [Fact]
    public async Task Import_Mixed_Provinces_Persists_Each_Row_CityValue_And_CityName()
    {
        // Central/gateway manager on a host whose actor.BranchId is نجف must NOT stamp نجف on all rows.
        var repo = new FakeRequestRepository();
        var actor = Manager(branchId: "najaf-demo", branchName: "النجف", isGateway: true);
        var result = await Svc(repo).ImportRowsAsync(actor, Import(
            Row(2, "نجف", "07801111111", "النجف", "najaf-demo", "النجف"),
            Row(3, "بصرة", "07802222222", "البصرة", "basra-demo", "البصرة"),
            Row(4, "كرخ", "07803333333", "الكرخ", "karkh-demo", "الكرخ"),
            Row(5, "رصافة", "07804444444", "الرصافة", "rusafa-demo", "الرصافة"),
            Row(6, "كربلاء", "07805555555", "كربلاء", "karbala-demo", "كربلاء")), default);

        Assert.Equal(5, result.Saved);
        Assert.Equal(0, result.Failed);
        Assert.Equal(5, repo.Rows.Count);

        var byName = repo.Rows.ToDictionary(r => r.CustomerName);
        Assert.Equal("najaf-demo", byName["نجف"].CityValue);
        Assert.Equal("النجف", byName["نجف"].CityName);
        Assert.Equal("basra-demo", byName["بصرة"].CityValue);
        Assert.Equal("البصرة", byName["بصرة"].CityName);
        Assert.Equal("karkh-demo", byName["كرخ"].CityValue);
        Assert.Equal("الكرخ", byName["كرخ"].CityName);
        Assert.Equal("rusafa-demo", byName["رصافة"].CityValue);
        Assert.Equal("الرصافة", byName["رصافة"].CityName);
        Assert.Equal("karbala-demo", byName["كربلاء"].CityValue);
        Assert.Equal("كربلاء", byName["كربلاء"].CityName);

        Assert.DoesNotContain(repo.Rows.Where(r => r.CustomerName != "نجف"),
            r => string.Equals(r.CityValue, "najaf-demo", StringComparison.OrdinalIgnoreCase)
                 || (r.CityName ?? "").Contains("نجف", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_Does_Not_Stamp_Actor_BranchId_When_Row_City_Provided()
    {
        var repo = new FakeRequestRepository();
        var actor = Manager(branchId: "najaf-demo", branchName: "النجف", isGateway: true);
        var result = await Svc(repo).ImportRowsAsync(actor, Import(
            Row(2, "بصرة", "07802222222", "البصرة", "basra-demo", "البصرة")), default);

        Assert.Equal(1, result.Saved);
        var row = repo.Rows.Single();
        Assert.Equal("basra-demo", row.CityValue);
        Assert.Equal("البصرة", row.CityName);
        Assert.NotEqual(actor.BranchId, row.CityValue);
        Assert.NotEqual(actor.BranchName, row.CityName);
    }

    [Fact]
    public async Task Import_Uses_Batch_CityValue_When_Row_Omits_It()
    {
        var repo = new FakeRequestRepository();
        var dto = new SalesRequestImportDTO
        {
            CityValue = "basra-demo",
            CityName = "البصرة",
            Rows =
            [
                new SalesRequestImportRowDTO
                {
                    RowNumber = 2,
                    CustomerName = "بصرة",
                    Phone = "07802222222",
                    Province = "البصرة"
                }
            ]
        };
        var result = await Svc(repo).ImportRowsAsync(Manager(), dto, default);

        Assert.Equal(1, result.Saved);
        Assert.Equal("basra-demo", repo.Rows.Single().CityValue);
        Assert.Equal("البصرة", repo.Rows.Single().CityName);
    }

    [Fact]
    public async Task Import_Rejects_Blank_Province()
    {
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(), Import(
            new SalesRequestImportRowDTO
            {
                RowNumber = 7,
                CustomerName = "بدون محافظة",
                Phone = "07806666666",
                Province = "  ",
                CityValue = "basra-demo",
                CityName = "البصرة"
            }), default);

        Assert.Equal(0, result.Saved);
        Assert.Equal(1, result.Failed);
        Assert.Contains(result.Errors, e => e.RowNumber == 7 && e.Message.Contains("المحافظة", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_Rejects_Unknown_Province_Without_CityValue()
    {
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(), Import(
            new SalesRequestImportRowDTO
            {
                RowNumber = 8,
                CustomerName = "مجهول",
                Phone = "07807777777",
                Province = "محافظة غير موجودة"
            }), default);

        Assert.Equal(0, result.Saved);
        Assert.Equal(1, result.Failed);
        Assert.Contains(result.Errors, e =>
            e.RowNumber == 8 && e.Message.Contains("غير معروفة", StringComparison.Ordinal));
        Assert.Empty(repo.Rows);
    }

    [Fact]
    public async Task Import_Rejects_Silent_Najaf_Stamp_For_Non_Najaf_Province()
    {
        var repo = new FakeRequestRepository();
        var result = await Svc(repo).ImportRowsAsync(Manager(), Import(
            Row(9, "بصرة", "07808888888", "البصرة", "najaf-demo", "النجف")), default);

        Assert.Equal(0, result.Saved);
        Assert.Equal(1, result.Failed);
        Assert.Empty(repo.Rows);
        Assert.Contains(result.Errors, e =>
            e.RowNumber == 9 && e.Message.Contains("النجف", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_Branch_Limited_Manager_Cannot_Import_Foreign_Province()
    {
        var repo = new FakeRequestRepository();
        var actor = Manager(branchId: "najaf-demo", branchName: "النجف", isGateway: false);
        var result = await Svc(repo).ImportRowsAsync(actor, Import(
            Row(10, "بصرة", "07809999999", "البصرة", "basra-demo", "البصرة")), default);

        Assert.Equal(0, result.Saved);
        Assert.Equal(1, result.Failed);
        Assert.Empty(repo.Rows);
        Assert.Contains(result.Errors, e =>
            e.RowNumber == 10 && e.Message.Contains("صلاحيات", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Import_Branch_Limited_Manager_Can_Import_Own_Province()
    {
        var repo = new FakeRequestRepository();
        var actor = Manager(branchId: "najaf-demo", branchName: "النجف", isGateway: false);
        var result = await Svc(repo).ImportRowsAsync(actor, Import(
            Row(11, "نجف", "07801010101", "النجف", "najaf-demo", "النجف")), default);

        Assert.Equal(1, result.Saved);
        Assert.Equal("najaf-demo", repo.Rows.Single().CityValue);
    }

    [Fact]
    public void ForbiddenDefaultCityStamp_Detects_Najaf_Mismatch()
    {
        Assert.True(SalesRequestService.IsForbiddenDefaultCityStamp("najaf-demo", "النجف", "البصرة"));
        Assert.True(SalesRequestService.IsForbiddenDefaultCityStamp("DEMO_BRANCH_VALUE", "النجف - DEMO", "الكرخ"));
        Assert.False(SalesRequestService.IsForbiddenDefaultCityStamp("basra-demo", "البصرة", "البصرة"));
        Assert.False(SalesRequestService.IsForbiddenDefaultCityStamp("najaf-demo", "النجف", "النجف"));
    }

    [Fact]
    public void Production_Must_Not_Treat_DemoNajaf_As_Implicit_Import_City()
    {
        // Guard: CreateAsync still uses actor.BranchId for manual creates; Import must not.
        Assert.True(SalesRequestService.IsForbiddenDefaultCityStamp("najaf-demo", "النجف - DEMO", "الرصافة"));
        Assert.True(SalesRequestService.IsForbiddenDefaultCityStamp("najaf", "Najaf", "كربلاء"));
    }
}
