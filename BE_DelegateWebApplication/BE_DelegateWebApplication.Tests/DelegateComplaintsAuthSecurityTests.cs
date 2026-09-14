using BE_DelegateWebApplication.Controllers;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public sealed class DelegateComplaintsAuthSecurityTests
{
    private sealed class FakeDelegates : IDelegateRepository
    {
        public Dictionary<string, DelegateGetDTO> ByAsyncId { get; } = new(StringComparer.Ordinal);

        public Task<DelegateGetDTO?> GetDelegateLogin(string? asyncID)
        {
            if (string.IsNullOrWhiteSpace(asyncID))
            {
                return Task.FromResult<DelegateGetDTO?>(null);
            }

            return Task.FromResult(
                ByAsyncId.TryGetValue(asyncID.Trim().TrimEnd('/'), out var d) ? d : null);
        }

        public Task<DelegateGetDTO?> GetDelegateCheckLogout(string? asyncID) =>
            GetDelegateLogin(asyncID);

        public Task<DelegateInfoGetDTO?> GetDelegateTitle(int? delegateId) =>
            Task.FromResult<DelegateInfoGetDTO?>(null);

        public Task<IEnumerable<SelectDelegateGetDTO>?> GetDelegateSelect(int? delegateId) =>
            Task.FromResult<IEnumerable<SelectDelegateGetDTO>?>(null);

        public Task<IEnumerable<SelectDelegateGetDTO>?> GetFollowerCityLists(int followerUserId) =>
            Task.FromResult<IEnumerable<SelectDelegateGetDTO>?>(null);

        public Task<bool> IsFollowerListLinked(int fatherId, int childId) =>
            Task.FromResult(false);
    }

    [Fact]
    public async Task Valid_AsyncId_Resolves_Server_Side_Identity()
    {
        var repo = new FakeDelegates();
        repo.ByAsyncId["secret-north"] = new DelegateGetDTO
        {
            DelegateId = 42,
            UserId = 7,
            CityId = 3,
            DelegateName = "مندوب الشمالية",
            AsyncId = "secret-north"
        };

        var result = await DelegateComplaintAuth.AuthenticateAsync(repo, "secret-north", expectedDelegateIdHeader: "42");
        Assert.True(result.Ok);
        var sender = DelegateComplaintAuth.MapSender(result.Delegate!);
        Assert.Equal(42, sender.DelegateId);
        Assert.Equal(7, sender.UserId);
        Assert.Equal(3, sender.CityId);
        Assert.Equal("مندوب الشمالية", sender.SenderDisplayName);
    }

    [Fact]
    public async Task Invalid_AsyncId_Is_401()
    {
        var repo = new FakeDelegates();
        var result = await DelegateComplaintAuth.AuthenticateAsync(repo, "wrong", null);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
        Assert.Null(result.Delegate);
    }

    [Fact]
    public async Task Empty_AsyncId_Is_401()
    {
        var repo = new FakeDelegates();
        var result = await DelegateComplaintAuth.AuthenticateAsync(repo, "  ", null);
        Assert.Equal(StatusCodes.Status401Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task Expected_DelegateId_Mismatch_Is_403()
    {
        var repo = new FakeDelegates();
        repo.ByAsyncId["secret-a"] = new DelegateGetDTO
        {
            DelegateId = 10,
            DelegateName = "مندوب أ"
        };

        // Attacker uses AsyncId of A but claims session DelegateId of B.
        var result = await DelegateComplaintAuth.AuthenticateAsync(repo, "secret-a", "99");
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Null(result.Delegate);
    }

    [Fact]
    public async Task Spoof_Body_DelegateId_Cannot_Override_Resolved_Identity()
    {
        var repo = new FakeDelegates();
        repo.ByAsyncId["secret-a"] = new DelegateGetDTO
        {
            DelegateId = 10,
            DelegateName = "مندوب أ",
            CityId = 1
        };
        repo.ByAsyncId["secret-b"] = new DelegateGetDTO
        {
            DelegateId = 99,
            DelegateName = "مندوب ب",
            CityId = 2
        };

        // Client authenticates as A; any body DelegateId=99 is irrelevant — MapSender uses auth only.
        var result = await DelegateComplaintAuth.AuthenticateAsync(repo, "secret-a", null);
        Assert.True(result.Ok);
        var sender = DelegateComplaintAuth.MapSender(result.Delegate!);
        Assert.Equal(10, sender.DelegateId);
        Assert.Equal("مندوب أ", sender.SenderDisplayName);
        Assert.NotEqual(99, sender.DelegateId);
    }

    [Fact]
    public void CreateComplaintBody_Contract_Has_No_Sender_Identity_Fields()
    {
        var props = typeof(DelegateComplaintsController.CreateComplaintBody)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("Message", props);
        Assert.DoesNotContain("DelegateId", props);
        Assert.DoesNotContain("SenderName", props);
        Assert.DoesNotContain("SenderDisplayName", props);
        Assert.DoesNotContain("BranchLink", props);
        Assert.DoesNotContain("CityId", props);
    }

    [Fact]
    public void BranchLink_Taken_From_Host_Not_Client_Value()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString("najaf-demo.example.com");
        // Client may try to send BranchLink in body — controller never reads it.
        const string clientSpoofBody = "https://evil.example/api/";

        var fromHost = DelegateComplaintAuth.BranchLinkFromRequest(httpContext.Request);
        Assert.Equal("najaf-demo.example.com", fromHost);
        Assert.NotEqual(clientSpoofBody, fromHost);
        Assert.DoesNotContain("evil", fromHost!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Header_AsyncId_Preferred_Over_Body_Credential()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[DelegateComplaintAuth.AsyncIdHeader] = "from-header";
        var chosen = DelegateComplaintAuth.ReadAsyncIdCredential(
            httpContext.Request, bodyAsyncId: "from-body");
        Assert.Equal("from-header", chosen);
    }

    [Fact]
    public void MapSender_Never_Uses_Client_Display_Name()
    {
        var auth = new DelegateGetDTO { DelegateId = 5, DelegateName = "مندوب الشمالية" };
        const string clientSpoofName = "مدير الشركة";
        var mapped = DelegateComplaintAuth.MapSender(auth);
        Assert.Equal("مندوب الشمالية", mapped.SenderDisplayName);
        Assert.NotEqual(clientSpoofName, mapped.SenderDisplayName);
    }
}
