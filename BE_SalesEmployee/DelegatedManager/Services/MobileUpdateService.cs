using BE_SalesEmployee.DelegatedManager.Domain;
using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class MobileUpdateCheck
{
    public required MobileAppRelease Release { get; init; }
    public required MobileUpdateKind Kind { get; init; }

    public bool UpdateAvailable => Kind != MobileUpdateKind.None;
    public bool Mandatory => Kind == MobileUpdateKind.Mandatory;
}

public sealed class MobileUpdateService
{
    private readonly IMobileUpdateStore _store;

    public MobileUpdateService(IMobileUpdateStore store)
    {
        _store = store;
    }

    public Task<MobileAppRelease?> GetAsync(string appKey, CancellationToken ct = default) =>
        _store.GetAsync(appKey, ct);

    /// <summary>Null when the app key is unknown; otherwise the release plus the decision for the installed build.</summary>
    public async Task<MobileUpdateCheck?> EvaluateAsync(
        string appKey,
        int currentVersionCode,
        CancellationToken ct = default)
    {
        var release = await _store.GetAsync(appKey, ct);
        if (release is null)
        {
            return null;
        }

        return new MobileUpdateCheck
        {
            Release = release,
            Kind = MobileUpdateRules.Evaluate(
                currentVersionCode,
                release.LatestVersionCode,
                release.MinimumSupportedVersionCode,
                release.ForceUpdate)
        };
    }
}
