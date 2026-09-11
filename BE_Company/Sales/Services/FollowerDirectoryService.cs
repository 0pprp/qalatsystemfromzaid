using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Accountant follower directory: dbo.Users with UserType = متابع only.
    /// Independent of FollowerProfiles and FollowerUserLists.
    /// </summary>
    public interface IFollowerDirectoryService
    {
        Task<IReadOnlyList<FollowerDirectoryItem>> ListActiveFollowersAsync(CancellationToken ct = default);
    }

    public sealed class FollowerDirectoryItem
    {
        public int FollowerId { get; set; }
        public int UserId { get; set; }
        public string FollowerName { get; set; } = "";
        public string? CityName { get; set; }
        public int? CityId { get; set; }
        public bool HasActiveShift { get; set; }
        public double? LastLatitude { get; set; }
        public double? LastLongitude { get; set; }
        public DateTime? LastUpdatedAtUtc { get; set; }
    }

    public sealed class FollowerDirectoryService : IFollowerDirectoryService
    {
        private readonly string _cs;

        public FollowerDirectoryService(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("SalesDemoConnection")
                  ?? configuration.GetConnectionString("DataBaseConnection")
                  ?? throw new InvalidOperationException("Database connection missing.");
        }

        public async Task<IReadOnlyList<FollowerDirectoryItem>> ListActiveFollowersAsync(
            CancellationToken ct = default)
        {
            await using var c = new SqlConnection(_cs);
            var rows = await c.QueryAsync<FollowerDirectoryItem>(new CommandDefinition(@"
SELECT
    u.UserID AS FollowerId,
    u.UserID AS UserId,
    u.UserName AS FollowerName,
    city.CityName,
    city.CityId,
    CAST(0 AS BIT) AS HasActiveShift
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
