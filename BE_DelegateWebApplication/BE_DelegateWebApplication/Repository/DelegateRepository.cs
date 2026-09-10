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
        /// Lists for a User-based follower (UserType=متابع): payment delegates in UsersSelectedCities,
        /// or all payment delegates when no city is assigned.
        /// </summary>
        public async Task<IEnumerable<SelectDelegateGetDTO>?> GetFollowerCityLists(int followerUserId)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<SelectDelegateGetDTO>(@"
DECLARE @IsFollower BIT = (
    SELECT TOP 1 CASE
        WHEN ISNULL(u.UserState, 1) = 1 AND (u.UserType = N'متابع' OR u.UserType LIKE N'متابع%')
        THEN 1 ELSE 0 END
    FROM dbo.Users u WHERE u.UserID = @UserId
);
IF ISNULL(@IsFollower, 0) = 0
BEGIN
    SELECT CAST(NULL AS INT) AS DelegateId WHERE 1 = 0;
    RETURN;
END

DECLARE @CityId INT = (
    SELECT TOP 1 usc.CityID FROM dbo.UsersSelectedCities usc WHERE usc.UserID = @UserId
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
                // User-based: fatherId is Users.UserID with UserType = متابع.
                var follower = await connection.QueryFirstOrDefaultAsync<dynamic>(@"
SELECT TOP 1
    CAST(CASE WHEN ISNULL(u.UserState, 1) = 1 AND (u.UserType = N'متابع' OR u.UserType LIKE N'متابع%') THEN 1 ELSE 0 END AS BIT) AS Ok,
    (SELECT TOP 1 usc.CityID FROM dbo.UsersSelectedCities usc WHERE usc.UserID = u.UserID) AS CityId
FROM dbo.Users u
WHERE u.UserID = @UserId;",
                    new { UserId = fatherId });

                if (follower != null && Convert.ToBoolean(follower.Ok))
                {
                    int? cityId = follower.CityId as int?;
                    if (cityId is null && follower.CityId != null)
                    {
                        cityId = Convert.ToInt32(follower.CityId);
                    }
                    if (cityId is null)
                    {
                        return true;
                    }

                    var sameCity = await connection.ExecuteScalarAsync<int?>(@"
SELECT TOP 1 1
FROM dbo.Delegates d
WHERE d.DelegateID = @ChildId AND d.CityID = @CityId;",
                        new { ChildId = childId, CityId = cityId });
                    return sameCity == 1;
                }

                // Legacy Delegate-linked followers — keep for old data only.
                var result = await connection.QuerySingleOrDefaultAsync<LinkedFlagDTO>(
                    "Followers_IsLinked",
                    new { FatherID = fatherId, ChildID = childId },
                    commandType: CommandType.StoredProcedure);
                return result != null && result.IsLinked == 1;
            }
        }
    }
}
