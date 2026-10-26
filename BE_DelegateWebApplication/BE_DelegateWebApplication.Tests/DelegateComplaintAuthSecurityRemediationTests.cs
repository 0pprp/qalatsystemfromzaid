using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using BE_DelegateWebApplication.Services;
using Xunit;

namespace BE_DelegateWebApplication.Tests;

public class DelegateComplaintAuthSecurityRemediationTests
{
    private sealed class AuthDelegates : IDelegateRepository
    {
        private readonly DelegateGetDTO? _auth;

        public AuthDelegates(DelegateGetDTO? auth) => _auth = auth;

        public Task<DelegateGetDTO?> GetDelegateLogin(string? asyncID)
        {
            if (_auth is null || string.IsNullOrWhiteSpace(asyncID))
            {
                return Task.FromResult<DelegateGetDTO?>(null);
            }

            return Task.FromResult<DelegateGetDTO?>(
                string.Equals(_auth.AsyncId, asyncID.Trim().TrimEnd('/'), StringComparison.Ordinal)
                    ? _auth
                    : null);
        }

        public Task<DelegateGetDTO?> GetDelegateCheckLogout(string? asyncID) =>
            Task.FromResult<DelegateGetDTO?>(null);

        public Task<DelegateInfoGetDTO?> GetDelegateTitle(int? delegateId) =>
            Task.FromResult<DelegateInfoGetDTO?>(null);

        public Task<IEnumerable<SelectDelegateGetDTO>?> GetDelegateSelect(int? delegateId) =>
            Task.FromResult<IEnumerable<SelectDelegateGetDTO>?>(Array.Empty<SelectDelegateGetDTO>());

        public Task<IEnumerable<SelectDelegateGetDTO>?> GetFollowerCityLists(int followerUserId) =>
            Task.FromResult<IEnumerable<SelectDelegateGetDTO>?>(Array.Empty<SelectDelegateGetDTO>());

        public Task<bool> IsFollowerListLinked(int fatherId, int childId) =>
            Task.FromResult(false);
    }

    [Fact]
    public async Task Visitor_without_asyncId_is_401()
    {
        var result = await DelegateComplaintAuth.AuthenticateAsync(new AuthDelegates(null), null, null);
        Assert.Equal(401, result.StatusCode);
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Wrong_asyncId_is_401()
    {
        var auth = new DelegateGetDTO { DelegateId = 1, AsyncId = "real-async", DelegateName = "A" };
        var result = await DelegateComplaintAuth.AuthenticateAsync(new AuthDelegates(auth), "forged", null);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task Session_delegate_mismatch_is_403()
    {
        var auth = new DelegateGetDTO { DelegateId = 10, AsyncId = "real-async", DelegateName = "A" };
        var result = await DelegateComplaintAuth.AuthenticateAsync(new AuthDelegates(auth), "real-async", "99");
        Assert.Equal(403, result.StatusCode);
        Assert.False(result.Ok);
    }

    [Fact]
    public async Task Matching_credential_succeeds()
    {
        var auth = new DelegateGetDTO { DelegateId = 10, AsyncId = "real-async", DelegateName = "A", UserId = 5 };
        var result = await DelegateComplaintAuth.AuthenticateAsync(new AuthDelegates(auth), "real-async", "10");
        Assert.True(result.Ok);
        Assert.Equal(10, result.Delegate!.DelegateId);
    }
}
