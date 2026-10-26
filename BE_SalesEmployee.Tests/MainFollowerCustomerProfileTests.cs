using BE_SalesEmployee.Controllers;
using BE_SalesEmployee.MainFollower.Services;
using BE_SalesEmployee.MainFollower.Stores;
using BE_SalesEmployee.Sales.Authorization;
using BE_SalesEmployee.Sales.Services;
using BE_SalesEmployee.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class MainFollowerCustomerProfileTests
{
    [Fact]
    public async Task Customer_profile_requires_mission_city_membership()
    {
        var store = new InMemoryCommitteeStore();
        var fake = new FakeProfileAggregator();
        var controller = Build(store, fake);
        var committees = new CommitteeService(store);
        var c = await committees.CreateCommitteeAsync("dm", "DM", "لجنة", ["mf1", "mf2"], default);
        await committees.CreateMissionAsync("dm", c.Id, ["DatabaseCompanyNajaf_DEMO"], null, default);
        controller.ControllerContext = PrincipalContext("mf1");

        var result = await controller.CustomerProfileById(
            missionId: 1,
            cityValue: "DatabaseCompanyBasra",
            customerId: 9,
            default);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("محافظة", bad.Value!.ToString()!, StringComparison.Ordinal);
        Assert.Equal(0, fake.Calls);
    }

    [Fact]
    public async Task Customer_profile_proxies_when_city_authorized()
    {
        var store = new InMemoryCommitteeStore();
        var committees = new CommitteeService(store);
        var c = await committees.CreateCommitteeAsync("dm", "DM", "لجنة", ["mf1", "mf2"], default);
        var mission = await committees.CreateMissionAsync(
            "dm", c.Id, ["DatabaseCompanyNajaf_DEMO"], null, default);

        var fake = new FakeProfileAggregator
        {
            Status = 200,
            Body = new { customerId = 42, customerName = "زبون" }
        };
        var controller = Build(store, fake);
        controller.ControllerContext = PrincipalContext("mf1");

        var result = await controller.CustomerProfileById(
            mission.Id,
            "DatabaseCompanyNajaf_DEMO",
            42,
            default);

        var ok = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        Assert.Equal(1, fake.Calls);
        Assert.Equal("sales-manager/customers/42/profile", fake.LastPath);
        Assert.Equal("DatabaseCompanyNajaf_DEMO", fake.LastCity);
    }

    private static MainFollowerController Build(ICommitteeStore store, FakeProfileAggregator fake)
    {
        var salesStore = new InMemorySaleReviewStore();
        var staffStore = new InMemoryStaffReviewStore();
        return new MainFollowerController(
            new CommitteeService(store),
            new SaleReviewService(store, salesStore),
            new StaffReviewService(store, staffStore),
            new CommitteeHistoryService(store, salesStore, staffStore),
            new MainFollowerShiftService(new InMemoryMainFollowerShiftStore()),
            fake);
    }

    private static ControllerContext PrincipalContext(string userName)
    {
        var identity = new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim("UserName", userName),
            new System.Security.Claims.Claim("UserType", SalesRoles.UserTypeMainFollower),
            new System.Security.Claims.Claim("UserID", "0"),
        ], "test");
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(identity)
            }
        };
    }

    private sealed class FakeProfileAggregator : ISalesManagerBranchAggregator
    {
        public int Status { get; set; } = 200;
        public object? Body { get; set; }
        public int Calls { get; private set; }
        public string? LastPath { get; private set; }
        public string? LastCity { get; private set; }

        public Task<(int Status, object? Body)> GetOneAsync(
            GatewayUser user, string cityValue, string companyPath, CancellationToken ct)
        {
            Calls++;
            LastPath = companyPath;
            LastCity = cityValue;
            return Task.FromResult((Status, Body));
        }

        public Task<(int Status, object? Body)> GetObjectFanoutAsync(GatewayUser user, string companyPath, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));

        public Task<(int Status, object? Body)> GetSalesRequestsPageFanoutAsync(
            GatewayUser user, string companyPath, int page, int pageSize, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));

        public Task<IReadOnlyList<AdminCity>> BranchesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<AdminCity>>([]);

        public Task<(int Status, object? Body)> GetAsync(GatewayUser user, string? cityValue, string companyPath, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> GetExactBranchArrayAsync(GatewayUser user, string cityValue, string companyPath, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> SumCountAsync(GatewayUser user, string? cityValue, string companyPath, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> PostFanoutAsync(GatewayUser user, string? cityValue, string companyPath, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> GetFileAsync(GatewayUser user, string cityValue, string companyPath, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> PostAsync(GatewayUser user, string cityValue, string companyPath, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> SendContentAsync(GatewayUser user, string cityValue, string companyPath, HttpMethod method, HttpContent? content, CancellationToken ct, string? actorRole = null) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> SearchCustomersAsync(GatewayUser user, string? query, string? cityValue, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> ExcelSearchAsync(GatewayUser user, string? cityValue, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> DashboardAsync(GatewayUser user, string? cityValue, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> EvaluateAcrossBranchesAsync(GatewayUser user, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> EvaluationHitsAcrossBranchesAsync(GatewayUser user, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
        public Task<(int Status, object? Body)> TransferProvinceAsync(GatewayUser user, string fromCityValue, int requestId, string jsonBody, CancellationToken ct) =>
            Task.FromResult<(int, object?)>((501, null));
    }
}
