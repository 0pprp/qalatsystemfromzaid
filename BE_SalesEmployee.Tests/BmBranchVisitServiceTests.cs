using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Services;
using BE_SalesEmployee.DelegatedManager.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_SalesEmployee.Tests;

public sealed class BmBranchVisitServiceTests : IDisposable
{
    private readonly FakeEnv _env = new();
    private readonly InMemoryBmBranchVisitStore _visits = new();
    private readonly InMemoryBranchManagerAppUserStore _users = new();
    private readonly InMemoryDirectorAssignedTaskStore _tasks = new();
    private readonly BmBranchVisitService _sut;
    private readonly Guid _actorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public BmBranchVisitServiceTests()
    {
        var files = new BmBranchVisitFileStorage(_env, NullLogger<BmBranchVisitFileStorage>.Instance);
        var taskFiles = new DirectorAssignedTaskFileStorage(
            _env, NullLogger<DirectorAssignedTaskFileStorage>.Instance);
        var taskSvc = new DirectorAssignedTaskService(_tasks, taskFiles);
        _sut = new BmBranchVisitService(_visits, _users, files, taskSvc);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_env.ContentRootPath))
                Directory.Delete(_env.ContentRootPath, recursive: true);
        }
        catch { /* ignore */ }
    }

    [Fact]
    public async Task Open_snapshots_active_members_for_city_and_sets_arrival()
    {
        await _users.UpsertAsync(new BranchManagerAppUser
        {
            Id = _actorId,
            UserName = "bm1",
            DisplayName = "أحمد",
            IsActive = true,
            PasswordHash = "x",
            Cities = [new BranchManagerAppUserCity { CityValue = "najaf", CityName = "النجف" }],
        });
        await _users.UpsertAsync(new BranchManagerAppUser
        {
            Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            UserName = "bm2",
            DisplayName = "سارة",
            IsActive = true,
            PasswordHash = "x",
            Cities = [new BranchManagerAppUserCity { CityValue = "najaf", CityName = "النجف" }],
        });

        var (ok, error, visit, _) = await _sut.OpenAsync(
            _actorId, "bm1", "أحمد", "najaf", "النجف", CancellationToken.None);

        Assert.True(ok, error);
        Assert.NotNull(visit);
        Assert.Equal(BmBranchVisitStatus.Open, visit!.Status);
        Assert.Equal(2, visit.Members.Count);
        Assert.True(visit.ArrivedAtUtc <= DateTime.UtcNow.AddSeconds(2));
    }

    [Fact]
    public async Task Open_second_city_while_open_fails()
    {
        await _users.UpsertAsync(new BranchManagerAppUser
        {
            Id = _actorId,
            UserName = "bm1",
            DisplayName = "أحمد",
            IsActive = true,
            PasswordHash = "x",
            Cities =
            [
                new BranchManagerAppUserCity { CityValue = "najaf" },
                new BranchManagerAppUserCity { CityValue = "basra" },
            ],
        });

        var first = await _sut.OpenAsync(_actorId, "bm1", "أحمد", "najaf", null, CancellationToken.None);
        Assert.True(first.Ok, first.Error);

        var second = await _sut.OpenAsync(_actorId, "bm1", "أحمد", "basra", null, CancellationToken.None);
        Assert.False(second.Ok);
        Assert.Contains("مفتوحة", second.Error ?? "");
    }

    [Fact]
    public async Task Depart_requires_long_note_then_closes()
    {
        await _users.UpsertAsync(new BranchManagerAppUser
        {
            Id = _actorId,
            UserName = "bm1",
            DisplayName = "أحمد",
            IsActive = true,
            PasswordHash = "x",
            Cities = [new BranchManagerAppUserCity { CityValue = "najaf" }],
        });
        var opened = await _sut.OpenAsync(_actorId, "bm1", "أحمد", "najaf", null, CancellationToken.None);
        Assert.True(opened.Ok, opened.Error);

        var shortNote = await _sut.DepartAsync(opened.Visit!.Id, _actorId, "قصير", null, CancellationToken.None);
        Assert.False(shortNote.Ok);

        var note = "قمنا بمراجعة القوائم وتقييم المندوبين ومتابعة المبيعات الجديدة في الفرع.";
        var noRatings = await _sut.DepartAsync(opened.Visit.Id, _actorId, note, null, CancellationToken.None);
        Assert.False(noRatings.Ok);
        Assert.Contains("تقييم", noRatings.Error ?? "");

        var rated = await _sut.UpsertDelegateRatingsAsync(
            opened.Visit.Id, _actorId, "أحمد",
            [(10, "مندوب أ", 7)], CancellationToken.None);
        Assert.True(rated.Ok, rated.Error);

        var closed = await _sut.DepartAsync(
            opened.Visit.Id, _actorId, note, null, CancellationToken.None, requiredDelegateIds: [10]);
        Assert.True(closed.Ok, closed.Error);
        Assert.Equal(BmBranchVisitStatus.Closed, closed.Visit!.Status);
        Assert.NotNull(closed.Visit.DepartedAtUtc);
        Assert.Equal(note, closed.Visit.Notes);
    }

    [Fact]
    public async Task Upsert_ratings_validates_0_to_10()
    {
        await _users.UpsertAsync(new BranchManagerAppUser
        {
            Id = _actorId,
            UserName = "bm1",
            DisplayName = "أحمد",
            IsActive = true,
            PasswordHash = "x",
            Cities = [new BranchManagerAppUserCity { CityValue = "najaf" }],
        });
        var opened = await _sut.OpenAsync(_actorId, "bm1", "أحمد", "najaf", null, CancellationToken.None);
        var bad = await _sut.UpsertDelegateRatingsAsync(
            opened.Visit!.Id, _actorId, "أحمد",
            [(1, "مندوب", 11)], CancellationToken.None);
        Assert.False(bad.Ok);

        var ok = await _sut.UpsertDelegateRatingsAsync(
            opened.Visit.Id, _actorId, "أحمد",
            [(1, "مندوب", 8), (2, "آخر", 0)], CancellationToken.None);
        Assert.True(ok.Ok, ok.Error);
        Assert.Equal(2, ok.Visit!.DelegateRatings.Count);
    }

    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } =
            Path.Combine(Path.GetTempPath(), "bm-visit-tests-" + Guid.NewGuid().ToString("N"));
        public string WebRootPath { get; set; } = "";
        public string ApplicationName { get; set; } = "tests";
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
