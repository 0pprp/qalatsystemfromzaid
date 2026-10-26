using Dapper;
using BE_DelegateWebApplication.Utilities;
using Microsoft.Data.SqlClient;

namespace BE_DelegateWebApplication.Services.FollowerIdentity
{
    /// <summary>
    /// Follower identity = company <c>Users</c> row with <c>UserType = متابع</c>.
    /// Never Delegates. Never FollowerProfiles gate.
    /// </summary>
    public sealed class FollowerUserIdentity
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = "";
        public string? AsyncId { get; set; }
        public string? UserType { get; set; }
        public int? CityId { get; set; }
        public string? CityName { get; set; }
        /// <summary>True when UserState is active (bit 1 / null treated as active).</summary>
        public bool IsActive { get; set; }
        /// <summary>Loaded only for credential verify; never returned to clients.</summary>
        public string? Password { get; set; }
    }

    public static class FollowerUserType
    {
        public const string Arabic = "متابع";

        public static bool IsFollowerType(string? userType) =>
            !string.IsNullOrWhiteSpace(userType)
            && (string.Equals(userType, Arabic, StringComparison.Ordinal)
                || userType.StartsWith(Arabic, StringComparison.Ordinal));
    }

    public enum FollowerAuthFailure
    {
        None = 0,
        InvalidCredentials = 1,
        NotFollower = 2,
        Inactive = 3,
    }

    public sealed class FollowerAuthOutcome
    {
        public FollowerUserIdentity? Identity { get; init; }
        public FollowerAuthFailure Failure { get; init; }

        public static FollowerAuthOutcome Ok(FollowerUserIdentity identity) =>
            new() { Identity = identity, Failure = FollowerAuthFailure.None };

        public static FollowerAuthOutcome Fail(FollowerAuthFailure failure) =>
            new() { Failure = failure };
    }

    public static class FollowerAuthMessages
    {
        public const string InvalidCredentials = "اسم المستخدم أو كلمة المرور غير صحيحة";
        public const string NotFollower = "هذا الحساب غير مخول لتطبيق المتابع";
        public const string Inactive = "هذا الحساب غير فعال";
        public const string InvalidSession = "الجلسة غير صالحة";
        /// <summary>Deploy verification marker — keep referenced.</summary>
        public const string DeployMarker = "FOLLOWER_LOGIN_PBKDF2_V1";
    }

    public interface IFollowerIdentityService
    {
        Task<FollowerUserIdentity?> ResolveByAsyncIdAsync(string? asyncId, CancellationToken ct = default);
        Task<FollowerAuthOutcome> ResolveSessionByAsyncIdAsync(string? asyncId, CancellationToken ct = default);
        Task<FollowerAuthOutcome> AuthenticateByCredentialsAsync(string? userName, string? password, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerUserIdentity>> ListActiveAsync(CancellationToken ct = default);
    }

    public sealed class FollowerIdentityService : IFollowerIdentityService
    {
        private const string UserSelectSql = @"
SELECT TOP 1
    u.UserID AS UserId,
    u.UserName,
    u.AsyncID AS AsyncId,
    u.UserType,
    city.CityId,
    city.CityName,
    CAST(CASE WHEN ISNULL(u.UserState, 1) = 1 THEN 1 ELSE 0 END AS BIT) AS IsActive,
    u.Password
FROM dbo.Users u
OUTER APPLY (
    SELECT TOP 1 usc.CityID AS CityId, ci.CityName
    FROM dbo.UsersSelectedCities usc
    LEFT JOIN dbo.Cities ci ON ci.CityID = usc.CityID
    WHERE usc.UserID = u.UserID
) city";

        private readonly string _cs;

        public FollowerIdentityService(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DataBaseConnection")
                  ?? throw new InvalidOperationException("DataBaseConnection missing.");
        }

        public async Task<FollowerUserIdentity?> ResolveByAsyncIdAsync(string? asyncId, CancellationToken ct = default)
        {
            var outcome = await ResolveSessionByAsyncIdAsync(asyncId, ct);
            return outcome.Failure == FollowerAuthFailure.None ? outcome.Identity : null;
        }

        public async Task<FollowerAuthOutcome> ResolveSessionByAsyncIdAsync(string? asyncId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(asyncId))
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.InvalidCredentials);
            }

            await using var c = new SqlConnection(_cs);
            var row = await c.QueryFirstOrDefaultAsync<FollowerUserIdentity>(new CommandDefinition(
                UserSelectSql + @"
WHERE LTRIM(RTRIM(u.AsyncID)) = LTRIM(RTRIM(@AsyncId))
  AND (
        u.UserType = N'متابع'
        OR u.UserType LIKE N'متابع%'
      )
ORDER BY u.UserID;",
                new { AsyncId = asyncId.Trim().TrimEnd('/') },
                cancellationToken: ct));

            return EvaluateRow(row);
        }

        public async Task<FollowerAuthOutcome> AuthenticateByCredentialsAsync(
            string? userName,
            string? password,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrEmpty(password))
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.InvalidCredentials);
            }

            await using var c = new SqlConnection(_cs);
            // FOLLOWER_LOGIN_PBKDF2_V1 — verify via PasswordHasher (hashed or legacy plaintext).
            var row = await c.QueryFirstOrDefaultAsync<FollowerUserIdentity>(new CommandDefinition(
                UserSelectSql + @"
WHERE LOWER(LTRIM(RTRIM(u.UserName))) = LOWER(LTRIM(RTRIM(@UserName)))
ORDER BY u.UserID;",
                new { UserName = userName.Trim() },
                cancellationToken: ct));

            if (row is null || row.UserId <= 0 || string.IsNullOrEmpty(row.Password))
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.InvalidCredentials);
            }

            if (!PasswordHasher.Verify(password, row.Password, out var needsUpgrade))
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.InvalidCredentials);
            }

            if (needsUpgrade)
            {
                var upgraded = PasswordHasher.Hash(password);
                await c.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.Users SET Password = @Password WHERE UserID = @UserID;",
                    new { Password = upgraded, UserID = row.UserId },
                    cancellationToken: ct));
            }

            // Ensure AsyncID exists — Flutter requires it for session.
            if (string.IsNullOrWhiteSpace(row.AsyncId))
            {
                var newAsync = Guid.NewGuid().ToString("N");
                await c.ExecuteAsync(new CommandDefinition(
                    "UPDATE dbo.Users SET AsyncID = @AsyncId WHERE UserID = @UserID AND (AsyncID IS NULL OR LTRIM(RTRIM(AsyncID)) = N'');",
                    new { AsyncId = newAsync, UserID = row.UserId },
                    cancellationToken: ct));
                row.AsyncId = newAsync;
            }

            row.Password = null;
            _ = FollowerAuthMessages.DeployMarker;
            return EvaluateRow(row);
        }

        public async Task<IReadOnlyList<FollowerUserIdentity>> ListActiveAsync(CancellationToken ct = default)
        {
            await using var c = new SqlConnection(_cs);
            var rows = await c.QueryAsync<FollowerUserIdentity>(new CommandDefinition(@"
SELECT
    u.UserID AS UserId,
    u.UserName,
    u.AsyncID AS AsyncId,
    u.UserType,
    city.CityId,
    city.CityName,
    CAST(1 AS BIT) AS IsActive
FROM dbo.Users u
OUTER APPLY (
    SELECT TOP 1 usc.CityID AS CityId, ci.CityName
    FROM dbo.UsersSelectedCities usc
    LEFT JOIN dbo.Cities ci ON ci.CityID = usc.CityID
    WHERE usc.UserID = u.UserID
) city
WHERE ISNULL(u.UserState, 1) = 1
  AND (
        u.UserType = N'متابع'
        OR u.UserType LIKE N'متابع%'
      )
ORDER BY u.UserName;", cancellationToken: ct));
            return rows.ToList();
        }

        private static FollowerAuthOutcome EvaluateRow(FollowerUserIdentity? row)
        {
            if (row is null || row.UserId <= 0)
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.InvalidCredentials);
            }

            if (!FollowerUserType.IsFollowerType(row.UserType))
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.NotFollower);
            }

            if (!row.IsActive)
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.Inactive);
            }

            row.Password = null;
            return FollowerAuthOutcome.Ok(row);
        }
    }
}
