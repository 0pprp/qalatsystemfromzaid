using BE_SalesEmployee.DelegatedManager.Models;
using BE_SalesEmployee.DelegatedManager.Stores;
using BE_SalesEmployee.Services;
using BE_SalesEmployee.Utilities;

namespace BE_SalesEmployee.DelegatedManager.Services;

public sealed class BranchManagerAppUserService
{
    private readonly IBranchManagerAppUserStore _store;
    private readonly AdminCitiesService _cities;
    private readonly BranchProxyService _proxy;
    private readonly TokenService _tokens;
    private readonly ILogger<BranchManagerAppUserService> _logger;

    public BranchManagerAppUserService(
        IBranchManagerAppUserStore store,
        AdminCitiesService cities,
        BranchProxyService proxy,
        TokenService tokens,
        ILogger<BranchManagerAppUserService> logger)
    {
        _store = store;
        _cities = cities;
        _proxy = proxy;
        _tokens = tokens;
        _logger = logger;
    }

    public async Task<IReadOnlyList<object>> ListAsync(CancellationToken ct = default)
    {
        var catalog = await _cities.GetCitiesAsync(ct);
        var users = await _store.ListAsync(ct);
        return users.Select(u => ToDto(u, catalog)).ToList();
    }

    public async Task<object?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _store.GetAsync(id, ct);
        if (user is null) return null;
        return ToDto(user, await _cities.GetCitiesAsync(ct));
    }

    public async Task<(bool Ok, string? Error, object? User)> CreateAsync(
        string? userName,
        string? password,
        string? displayName,
        IEnumerable<string>? cityValues,
        string? createdBy,
        CancellationToken ct = default)
    {
        var name = (userName ?? "").Trim();
        if (name.Length < 3) return (false, "اسم المستخدم 3 أحرف على الأقل", null);
        if (string.IsNullOrEmpty(password) || password.Length < 4)
            return (false, "كلمة المرور 4 أحرف على الأقل", null);

        var existing = await _store.FindByUserNameAsync(name, ct);
        if (existing is not null) return (false, "اسم المستخدم مستخدم مسبقاً", null);

        var cities = await ResolveCitiesAsync(cityValues, ct);
        if (cities.Count == 0) return (false, "اختر محافظة واحدةً على الأقل", null);

        var user = await _store.UpsertAsync(new BranchManagerAppUser
        {
            UserName = name,
            PasswordHash = PasswordHasher.Hash(password),
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim(),
            IsActive = true,
            CreatedByUserName = createdBy,
            Cities = cities
        }, ct);

        await ProvisionBranchesAsync(user, password, ct);
        return (true, null, ToDto(user, await _cities.GetCitiesAsync(ct)));
    }

    public async Task<(bool Ok, string? Error, object? User)> UpdateAsync(
        Guid id,
        string? userName,
        string? password,
        string? displayName,
        bool? isActive,
        IEnumerable<string>? cityValues,
        CancellationToken ct = default)
    {
        var existing = await _store.GetAsync(id, ct);
        if (existing is null) return (false, "المستخدم غير موجود", null);

        var name = string.IsNullOrWhiteSpace(userName) ? existing.UserName : userName.Trim();
        if (name.Length < 3) return (false, "اسم المستخدم 3 أحرف على الأقل", null);

        var clash = await _store.FindByUserNameAsync(name, ct);
        if (clash is not null && clash.Id != id)
            return (false, "اسم المستخدم مستخدم مسبقاً", null);

        var cities = cityValues is null
            ? existing.Cities
            : await ResolveCitiesAsync(cityValues, ct);
        if (cities.Count == 0) return (false, "اختر محافظة واحدةً على الأقل", null);

        var passwordToStore = existing.PasswordHash;
        string? plainPassword = null;
        if (!string.IsNullOrEmpty(password))
        {
            if (password.Length < 4) return (false, "كلمة المرور 4 أحرف على الأقل", null);
            passwordToStore = PasswordHasher.Hash(password);
            plainPassword = password;
        }

        var updated = await _store.UpsertAsync(new BranchManagerAppUser
        {
            Id = existing.Id,
            UserName = name,
            PasswordHash = passwordToStore,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? existing.DisplayName : displayName.Trim(),
            IsActive = isActive ?? existing.IsActive,
            CreatedAtUtc = existing.CreatedAtUtc,
            CreatedByUserName = existing.CreatedByUserName,
            Cities = cities
        }, ct);

        if (plainPassword is not null)
        {
            await ProvisionBranchesAsync(updated, plainPassword, ct);
        }
        else
        {
            // Re-sync city membership without password change is best-effort only when password known — skip.
            _logger.LogInformation("BM app user {User} cities updated; branch password unchanged.", updated.UserName);
        }

        return (true, null, ToDto(updated, await _cities.GetCitiesAsync(ct)));
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
        _store.DeleteAsync(id, ct);

    public async Task<(bool Ok, string? Error, object? Payload)> AuthenticateAsync(
        string? userName,
        string? password,
        CancellationToken ct = default)
    {
        var user = await _store.FindByUserNameAsync(userName ?? "", ct);
        if (user is null || !user.IsActive)
            return (false, "بيانات الدخول غير صحيحة أو الحساب غير مفعّل من المدير المفوض", null);

        if (!PasswordHasher.Verify(password ?? "", user.PasswordHash, out var needsUpgrade))
            return (false, "بيانات الدخول غير صحيحة أو الحساب غير مفعّل من المدير المفوض", null);

        if (needsUpgrade)
        {
            user.PasswordHash = PasswordHasher.Hash(password!);
            await _store.UpsertAsync(user, ct);
        }

        var catalog = await _cities.GetCitiesAsync(ct);
        var cities = EnrichCities(user.Cities, catalog);
        if (cities.Count == 0)
            return (false, "لا توجد محافظات معيّنة لهذا الحساب", null);

        // Re-sync مدير فرع account onto each assigned branch so BM app can open cities.
        await ProvisionBranchesAsync(user, password!, ct);

        var gatewayToken = _tokens.CreateBranchManagerAppToken(
            user.Id.ToString("D"),
            user.DisplayName,
            out var expiration);

        return (true, null, new
        {
            userName = user.UserName,
            displayName = user.DisplayName,
            userType = "مدير فرع",
            userId = user.Id,
            token = gatewayToken,
            expiration,
            cities
        });
    }

    private async Task<List<BranchManagerAppUserCity>> ResolveCitiesAsync(
        IEnumerable<string>? cityValues,
        CancellationToken ct)
    {
        var catalog = await _cities.GetCitiesAsync(ct);
        var wanted = (cityValues ?? [])
            .Select(v => (v ?? "").Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return catalog
            .Where(c => wanted.Contains(c.Value) || wanted.Contains(c.Database) || wanted.Contains(c.Name))
            .Select(c => new BranchManagerAppUserCity
            {
                CityValue = string.IsNullOrWhiteSpace(c.Value) ? c.Database : c.Value,
                CityName = c.Name,
                CityLink = c.Link,
                Database = c.Database
            })
            .GroupBy(c => c.CityValue, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private async Task ProvisionBranchesAsync(BranchManagerAppUser user, string password, CancellationToken ct)
    {
        var catalog = await _cities.GetCitiesAsync(ct);
        foreach (var city in EnrichCities(user.Cities, catalog))
        {
            var link = city.CityLink;
            if (string.IsNullOrWhiteSpace(link)) continue;
            try
            {
                var body = System.Text.Json.JsonSerializer.Serialize(new
                {
                    userName = user.UserName,
                    password,
                    displayName = user.DisplayName
                });
                using var response = await _proxy.SendManagerAsync(
                    link,
                    "internal/branch-manager-users/upsert",
                    HttpMethod.Post,
                    body,
                    user.DisplayName,
                    ct);
                if (!response.IsSuccessStatusCode)
                {
                    var text = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning(
                        "BM user provision failed for {City} ({Status}): {Body}",
                        city.CityValue,
                        (int)response.StatusCode,
                        text.Length > 200 ? text[..200] : text);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BM user provision threw for {City}", city.CityValue);
            }
        }
    }

    private static List<BranchManagerAppUserCity> EnrichCities(
        IEnumerable<BranchManagerAppUserCity> assigned,
        IReadOnlyList<AdminCity> catalog)
    {
        var result = new List<BranchManagerAppUserCity>();
        foreach (var a in assigned)
        {
            var match = catalog.FirstOrDefault(c =>
                string.Equals(c.Value, a.CityValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Database, a.CityValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(c.Name, a.CityName, StringComparison.OrdinalIgnoreCase));
            result.Add(new BranchManagerAppUserCity
            {
                CityValue = match?.Value ?? a.CityValue,
                CityName = match?.Name ?? a.CityName,
                CityLink = match?.Link ?? a.CityLink,
                Database = match?.Database ?? a.Database
            });
        }
        return result;
    }

    private static object ToDto(BranchManagerAppUser user, IReadOnlyList<AdminCity> catalog)
    {
        var cities = EnrichCities(user.Cities, catalog);
        return new
        {
            user.Id,
            user.UserName,
            user.DisplayName,
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc,
            user.CreatedByUserName,
            cities = cities.Select(c => new
            {
                cityValue = c.CityValue,
                cityName = c.CityName,
                link = c.CityLink,
                database = c.Database
            })
        };
    }
}
