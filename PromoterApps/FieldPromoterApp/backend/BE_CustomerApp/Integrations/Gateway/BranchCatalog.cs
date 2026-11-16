using Microsoft.Extensions.Caching.Memory;

namespace BE_CustomerApp.Integrations.Gateway;

/// <summary>
/// Qalaa sales branches (from the gateway, cached 10 minutes). If the gateway is unreachable the
/// last known list, or the static Branches:Fallback config list, is used so the catalog keeps working.
/// </summary>
public sealed class BranchCatalog
{
    private const string CacheKey = "branches:v1";
    private readonly IGatewayClient _gateway;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<BranchCatalog> _logger;
    private IReadOnlyList<GatewayBranch>? _lastKnown;

    public BranchCatalog(IGatewayClient gateway, IMemoryCache cache, IConfiguration config, ILogger<BranchCatalog> logger)
    {
        _gateway = gateway;
        _cache = cache;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GatewayBranch>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<GatewayBranch>? cached) && cached is not null) return cached;
        try
        {
            var branches = (await _gateway.GetBranchesAsync(ct)).OrderBy(b => b.CityValue).ToList();
            if (branches.Count > 0)
            {
                _lastKnown = branches;
                _cache.Set(CacheKey, (IReadOnlyList<GatewayBranch>)branches, TimeSpan.FromMinutes(10));
                return branches;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Branch list unavailable from gateway; using fallback");
        }

        var fallback = _lastKnown ?? _config.GetSection("Branches:Fallback").Get<List<GatewayBranch>>() ?? [];
        _cache.Set(CacheKey, fallback, TimeSpan.FromMinutes(1));
        return fallback;
    }

    public async Task<bool> ExistsAsync(int cityValue, CancellationToken ct = default) =>
        (await GetAllAsync(ct)).Any(b => b.CityValue == cityValue);

    public async Task<string?> NameAsync(int cityValue, CancellationToken ct = default) =>
        (await GetAllAsync(ct)).FirstOrDefault(b => b.CityValue == cityValue)?.Name;
}
