namespace BE_SalesEmployee.Services;

/// <summary>
/// Creates the central logins listed under <c>OversightSeedAccounts</c> in configuration
/// (UserName, Password, Role) when they do not exist yet. Existing accounts are never changed.
/// The list lives in the server's own configuration, not in the repository.
/// </summary>
public static class OversightAccountSeeder
{
    private sealed class SeedAccount
    {
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string? Role { get; set; }
    }

    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
    {
        var accounts = configuration.GetSection("OversightSeedAccounts").Get<List<SeedAccount>>();
        if (accounts is null || accounts.Count == 0)
            return;
        try
        {
            using var scope = services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<OversightAccountService>();
            var store = scope.ServiceProvider.GetRequiredService<IOversightAccountStore>();
            foreach (var account in accounts)
            {
                var name = account.UserName?.Trim();
                if (string.IsNullOrEmpty(name) || await store.UserNameExistsAsync(name, CancellationToken.None))
                    continue;
                var (ok, error, _) = await service.CreateAccountAsync(
                    OversightAccessRules.RoleDirector, name, account.Password, account.Role, null, "seed", CancellationToken.None);
                if (ok)
                    logger.LogInformation("Seeded oversight account {UserName} ({Role})", name, account.Role);
                else
                    logger.LogWarning("Oversight account {UserName} was not seeded: {Error}", name, error);
            }
        }
        catch (Exception ex)
        {
            // Never keep the gateway from starting because of a seed.
            logger.LogError(ex, "Seeding oversight accounts failed");
        }
    }
}
