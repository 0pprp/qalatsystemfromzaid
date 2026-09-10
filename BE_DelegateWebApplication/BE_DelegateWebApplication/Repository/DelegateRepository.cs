using Dapper;
using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_DelegateWebApplication.Repository
{
    public class DelegateRepository : IDelegateRepository
    {
        private readonly string _connectionString;

        public DelegateRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<DelegateGetDTO?> GetDelegateLogin(string? asyncID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<DelegateGetDTO>("GetDelegateLogin",
                new
                {
                    AsyncID = asyncID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<DelegateGetDTO?> GetDelegateCheckLogout(string? asyncID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<DelegateGetDTO>("GetDelegateCheckLogout",
                new
                {
                    AsyncID = asyncID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<DelegateInfoGetDTO?> GetDelegateTitle(int? delegateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<DelegateInfoGetDTO>("GetDelegateTitle",
                new
                {
                    DelegateID = delegateId
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<SelectDelegateGetDTO>?> GetDelegateSelect(int? delegateId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<SelectDelegateGetDTO>("GetDelegateSelect",
                    new
                    {
                        DelegateID = delegateId
                    },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 600);
                return result;
            }
        }

        /// <summary>
        /// Lists available to a User-based follower: collection-delegate lists in the follower's profile city
        /// (or all payment delegates when CityId is null — Demo).
        /// </summary>
        public async Task<IEnumerable<SelectDelegateGetDTO>?> GetFollowerCityLists(int followerUserId)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<SelectDelegateGetDTO>(@"
DECLARE @CityId INT = (
    SELECT TOP 1 fp.CityId FROM dbo.FollowerProfiles fp WHERE fp.UserId = @UserId AND fp.IsActive = 1
);
SELECT
    d.DelegateID AS DelegateId,
    d.DelegateID AS DelegateChildId,
    d.DelegateName,
    d.ReceiptName,
    CAST(ISNULL(d.UpdateReceipt, 0) AS BIT) AS UpdateReceipt,
    CAST(ISNULL(d.DeleteReceipt, 0) AS BIT) AS DeleteReceipt,
    CAST(ISNULL(d.DevicePaymentState, 0) AS BIT) AS DevicePaymentState
FROM dbo.Delegates d
WHERE ISNULL(d.DevicePaymentState, 0) = 1
  AND (@CityId IS NULL OR d.CityID = @CityId)
ORDER BY d.DelegateName;",
                new { UserId = followerUserId });
        }

        public async Task<bool> IsFollowerListLinked(int fatherId, int childId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                // User-based: fatherId is Users.UserID with active FollowerProfile.
                var profile = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT TOP 1 fp.CityId
FROM dbo.FollowerProfiles fp
INNER JOIN dbo.Users u ON u.UserID = fp.UserId
WHERE fp.UserId = @UserId AND fp.IsActive = 1 AND ISNULL(u.UserState, 1) = 1;",
                    new { UserId = fatherId });

                if (profile != null)
                {
                    int? cityId = profile.CityId;
                    if (cityId is null)
                    {
                        // Demo / open city: allow any list for active follower user.
                        return true;
                    }

                    var sameCity = await connection.ExecuteScalarAsync<int?>(@"
SELECT TOP 1 1
FROM dbo.Delegates d
WHERE d.DelegateID = @ChildId AND d.CityID = @CityId;",
                        new { ChildId = childId, CityId = cityId });
                    return sameCity == 1;
                }

                // Legacy Delegate-linked followers (pre User migration) — keep for old data only.
                var result = await connection.QuerySingleOrDefaultAsync<LinkedFlagDTO>(
                    "Followers_IsLinked",
                    new { FatherID = fatherId, ChildID = childId },
                    commandType: CommandType.StoredProcedure);
                return result != null && result.IsLinked == 1;
            }
        }
    }
}
