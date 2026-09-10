using Dapper;
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
        /// <summary>True when UserType is متابع and UserState is active.</summary>
        public bool IsActive { get; set; }
    }

    public static class FollowerUserType
    {
        public const string Arabic = "متابع";

        public static bool IsFollowerType(string? userType) =>
            !string.IsNullOrWhiteSpace(userType)
            && (string.Equals(userType, Arabic, StringComparison.Ordinal)
                || userType.StartsWith(Arabic, StringComparison.Ordinal));
    }

    public interface IFollowerIdentityService
    {
        Task<FollowerUserIdentity?> ResolveByAsyncIdAsync(string? asyncId, CancellationToken ct = default);
        Task<IReadOnlyList<FollowerUserIdentity>> ListActiveAsync(CancellationToken ct = default);
    }

    public sealed class FollowerIdentityService : IFollowerIdentityService
    {
        private readonly string _cs;

        public FollowerIdentityService(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DataBaseConnection")
                  ?? throw new InvalidOperationException("DataBaseConnection missing.");
        }

        public async Task<FollowerUserIdentity?> ResolveByAsyncIdAsync(string? asyncId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(asyncId)) return null;
            await using var c = new SqlConnection(_cs);
            var row = await c.QueryFirstOrDefaultAsync<FollowerUserIdentity>(new CommandDefinition(@"
SELECT TOP 1
    u.UserID AS UserId,
    u.UserName,
    u.AsyncID AS AsyncId,
    u.UserType,
    city.CityId,
    city.CityName,
    CAST(CASE WHEN ISNULL(u.UserState, 1) = 1 THEN 1 ELSE 0 END AS BIT) AS IsActive
FROM dbo.Users u
OUTER APPLY (
    SELECT TOP 1 usc.CityID AS CityId, ci.CityName
    FROM dbo.UsersSelectedCities usc
    LEFT JOIN dbo.Cities ci ON ci.CityID = usc.CityID
    WHERE usc.UserID = u.UserID
) city
WHERE LTRIM(RTRIM(u.AsyncID)) = LTRIM(RTRIM(@AsyncId))
  AND (
        u.UserType = N'متابع'
        OR u.UserType LIKE N'متابع%'
      );",
                new { AsyncId = asyncId.Trim().TrimEnd('/') }, cancellationToken: ct));

            if (row is null || row.UserId <= 0 || !row.IsActive) return null;
            if (!FollowerUserType.IsFollowerType(row.UserType)) return null;
            return row;
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
    }
}
