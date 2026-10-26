using System.Collections.Concurrent;
using BE_Company.Utilities;

namespace BE_Company.Services;

/// <summary>In-memory portal token revocation by jti until expiry.</summary>
public interface IPortalTokenRevocationStore
{
    void Revoke(string jti, DateTimeOffset expiresAtUtc);
    bool IsRevoked(string jti);
}

public sealed class MemoryPortalTokenRevocationStore : IPortalTokenRevocationStore
{
    private readonly ConcurrentDictionary<string, long> _revoked = new(StringComparer.Ordinal);

    public void Revoke(string jti, DateTimeOffset expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(jti)) return;
        _revoked[jti.Trim()] = expiresAtUtc.ToUnixTimeSeconds();
        Sweep();
    }

    public bool IsRevoked(string jti)
    {
        if (string.IsNullOrWhiteSpace(jti)) return true;
        Sweep();
        return _revoked.ContainsKey(jti.Trim());
    }

    private void Sweep()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var kv in _revoked)
        {
            if (kv.Value < now)
            {
                _revoked.TryRemove(kv.Key, out _);
            }
        }
    }
}

public sealed class PortalAccessService
{
    private readonly IConfiguration _configuration;
    private readonly IPortalTokenRevocationStore _revocation;

    public PortalAccessService(IConfiguration configuration, IPortalTokenRevocationStore revocation)
    {
        _configuration = configuration;
        _revocation = revocation;
    }

    public string CreateCustomerPortalToken(int customerId, int branchId, TimeSpan? lifetime = null)
    {
        var key = RequireEncryptionKey();
        var days = _configuration.GetValue("PortalSettings:TokenLifetimeDays", 30);
        var ttl = lifetime ?? TimeSpan.FromDays(Math.Clamp(days, 1, 365));
        return PortalTokenCrypto.Create(
            customerId,
            branchId,
            PortalTokenCrypto.PurposeCustomerPortal,
            ttl,
            key);
    }

    public bool TryAuthorize(
        string? token,
        out int customerId,
        out string? error,
        int? expectedBranchId = null)
    {
        customerId = 0;
        error = null;
        if (string.IsNullOrWhiteSpace(token))
        {
            error = "المعرف مفقود";
            return false;
        }

        string key;
        try
        {
            key = RequireEncryptionKey();
        }
        catch
        {
            error = "خطأ في إعدادات الخادم";
            return false;
        }

        var payload = PortalTokenCrypto.TryValidate(token, key);
        if (payload is null)
        {
            error = "المعرف غير صالح أو منتهي الصلاحية";
            return false;
        }

        if (_revocation.IsRevoked(payload.Jti))
        {
            error = "المعرف غير صالح أو منتهي الصلاحية";
            return false;
        }

        var configuredBranch = ResolveConfiguredBranchId();
        if (configuredBranch is int cfg && payload.BranchId != cfg)
        {
            error = "المعرف غير صالح لهذا الفرع";
            return false;
        }

        if (expectedBranchId is int expected && expected > 0 && payload.BranchId != expected)
        {
            error = "المعرف غير صالح لهذا الفرع";
            return false;
        }

        customerId = payload.CustomerId;
        return true;
    }

    public void RevokeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return;
        var key = RequireEncryptionKey();
        var payload = PortalTokenCrypto.TryValidate(token, key);
        if (payload is null) return;
        _revocation.Revoke(payload.Jti, DateTimeOffset.FromUnixTimeSeconds(payload.ExpUnix));
    }

    private string RequireEncryptionKey()
    {
        var key = _configuration["PortalSettings:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("PortalSettings:EncryptionKey is required.");
        }

        return key.Trim();
    }

    /// <summary>Configured portal/sales branch city id for this host, if set.</summary>
    public int? GetConfiguredBranchId() => ResolveConfiguredBranchId();

    private int? ResolveConfiguredBranchId()
    {
        if (int.TryParse(_configuration["PortalSettings:BranchId"], out var portalBranch) && portalBranch > 0)
        {
            return portalBranch;
        }

        // Optional numeric SalesManagement:BranchId when it is an integer city id.
        if (int.TryParse(_configuration["SalesManagement:BranchId"], out var salesBranch) && salesBranch > 0)
        {
            return salesBranch;
        }

        return null;
    }
}
