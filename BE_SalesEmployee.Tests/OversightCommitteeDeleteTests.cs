using BE_SalesEmployee.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class OversightCommitteeDeleteTests
{
    private const string Director = "المدير";
    private const string Supervisor = "مسؤول المراقبين";

    private static OversightAccountService NewService() =>
        new(new InMemoryOversightAccountStore(), new ConfigurationBuilder().Build());

    [Fact]
    public async Task Empty_committee_can_be_deleted()
    {
        var svc = NewService();
        var (_, _, committee) = await svc.CreateCommitteeAsync(Director, "لجنة", ["najaf"], "x", default);

        var (ok, _) = await svc.DeleteCommitteeAsync(Supervisor, committee!.Id, default);

        Assert.True(ok);
        Assert.Empty(await svc.ListCommitteesAsync(default));
    }

    [Fact]
    public async Task Committee_with_accounts_is_kept()
    {
        var svc = NewService();
        var (_, _, committee) = await svc.CreateCommitteeAsync(Director, "لجنة", ["najaf"], "x", default);
        await svc.CreateAccountAsync(Director, "علي", "1", "مسؤول لجنة المراقبة", committee!.Id, "x", default);

        var (ok, error) = await svc.DeleteCommitteeAsync(Director, committee.Id, default);

        Assert.False(ok);
        Assert.Equal("احذف حسابات اللجنة أولاً", error);
        Assert.Single(await svc.ListCommitteesAsync(default));
    }

    [Fact]
    public async Task Committee_roles_cannot_delete_committees()
    {
        var svc = NewService();
        var (_, _, committee) = await svc.CreateCommitteeAsync(Director, "لجنة", ["najaf"], "x", default);

        var (ok, _) = await svc.DeleteCommitteeAsync("مسؤول لجنة المراقبة", committee!.Id, default);

        Assert.False(ok);
    }
}
