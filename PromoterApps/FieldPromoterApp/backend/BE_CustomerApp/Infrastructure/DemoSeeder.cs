using Dapper;

namespace BE_CustomerApp.Infrastructure;

/// <summary>Development only (Demo:Seed=true): a demo field agent so the app can sign in locally (agent1 / 123456).</summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(Db db, ILogger logger)
    {
        await using var c = await db.OpenAsync();
        if (await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM agents.Users WHERE Username IS NOT NULL") > 0) return;
        await c.ExecuteAsync("""
            IF NOT EXISTS (SELECT 1 FROM agents.Users WHERE Phone = '07700000001')
            INSERT agents.Users(Phone, FirstName, FatherName, GrandfatherName, ReferralCode, Username, PasswordHash, PasswordChangedAt, CreatedByAdmin, TermsAcceptedAt)
            VALUES ('07700000001', N'علي', N'حسين', N'محمد', 'DEMO01', 'agent1', @hash, SYSUTCDATETIME(), N'Demo seed', SYSUTCDATETIME())
            """, new { hash = Auth.PasswordHasher.Hash("123456") });
        logger.LogInformation("Demo agent seeded: agent1 / 123456");
    }
}
