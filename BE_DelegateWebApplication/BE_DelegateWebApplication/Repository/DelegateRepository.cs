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
        /// Lists assigned to a User-based follower via dbo.FollowerUserLists (ListId = Delegates.DelegateID).
        /// Empty assignment → empty list (login still allowed).
        /// </summary>
        public async Task<IEnumerable<SelectDelegateGetDTO>?> GetFollowerCityLists(int followerUserId)
        {
            using var connection = new SqlConnection(_connectionString);
            await EnsureFollowerUserListsSchemaAsync(connection);
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

SELECT
    d.DelegateID AS DelegateId,
    d.DelegateID AS DelegateChildId,
    d.DelegateName,
    d.ReceiptName,
    CAST(ISNULL(d.UpdateReceipt, 0) AS BIT) AS UpdateReceipt,
    CAST(ISNULL(d.DeleteReceipt, 0) AS BIT) AS DeleteReceipt,
    CAST(ISNULL(d.DevicePaymentState, 0) AS BIT) AS DevicePaymentState
FROM dbo.FollowerUserLists ful
INNER JOIN dbo.Delegates d ON d.DelegateID = ful.ListId
WHERE ful.UserId = @UserId
ORDER BY d.DelegateName;",
                new { UserId = followerUserId });
        }

        public async Task<bool> IsFollowerListLinked(int fatherId, int childId)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await EnsureFollowerUserListsSchemaAsync(connection);

                // User-based ACL: fatherId = Users.UserID, childId = Delegates.DelegateID (ListId).
                var allowed = await connection.ExecuteScalarAsync<int?>(@"
SELECT TOP 1 1
FROM dbo.Users u
INNER JOIN dbo.FollowerUserLists ful ON ful.UserId = u.UserID AND ful.ListId = @ListId
WHERE u.UserID = @UserId
  AND ISNULL(u.UserState, 1) = 1
  AND (u.UserType = N'متابع' OR u.UserType LIKE N'متابع%');",
                    new { UserId = fatherId, ListId = childId });

                if (allowed == 1)
                {
                    return true;
                }

                // If the subject is an active follower user, deny when not in FollowerUserLists
                // (do not fall through to legacy SelectDelegate — that would bypass ACL).
                var isFollowerUser = await connection.ExecuteScalarAsync<int?>(@"
SELECT TOP 1 1
FROM dbo.Users u
WHERE u.UserID = @UserId
  AND ISNULL(u.UserState, 1) = 1
  AND (u.UserType = N'متابع' OR u.UserType LIKE N'متابع%');",
                    new { UserId = fatherId });
                if (isFollowerUser == 1)
                {
                    return false;
                }

                // Legacy Delegate-linked followers only (pre User migration).
                var result = await connection.QuerySingleOrDefaultAsync<LinkedFlagDTO>(
                    "Followers_IsLinked",
                    new { FatherID = fatherId, ChildID = childId },
                    commandType: CommandType.StoredProcedure);
                return result != null && result.IsLinked == 1;
            }
        }

        private static async Task EnsureFollowerUserListsSchemaAsync(SqlConnection connection)
        {
            await connection.ExecuteAsync(@"
IF OBJECT_ID(N'dbo.FollowerUserLists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerUserLists (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FollowerUserLists PRIMARY KEY,
        UserId INT NOT NULL,
        ListId INT NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerUserLists_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_FollowerUserLists_User_List UNIQUE (UserId, ListId)
    );
END");
        }
    }
}
