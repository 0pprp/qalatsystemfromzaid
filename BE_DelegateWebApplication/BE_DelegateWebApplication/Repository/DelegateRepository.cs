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
        private static int _followerCollectionColumnReady;
        private static readonly SemaphoreSlim FollowerCollectionColumnGate = new(1, 1);

        public DelegateRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<DelegateGetDTO?> GetDelegateLogin(string? asyncID)
        {
            if (string.IsNullOrWhiteSpace(asyncID)) return null;
            using (var connection = new SqlConnection(_connectionString))
            {
                // Fast auth path — View_Delegates aggregates all customers (~12s on Basra).
                await EnsureFollowerCollectionColumnAsync(connection);
                var result = await connection.QuerySingleOrDefaultAsync<DelegateGetDTO>(new CommandDefinition(@"
-- login-via-Delegates-not-View_Delegates
SELECT TOP (1)
    D.DelegateID AS DelegateId,
    D.UserID AS UserId,
    D.CityID AS CityId,
    D.DelegateName,
    D.Address,
    D.PhoneNumber,
    D.Notes,
    D.DeviceSaleState,
    D.AsyncID AS AsyncId,
    D.DevicePaymentState,
    D.ReceiptName,
    D.UpdateReceipt,
    D.DeleteReceipt,
    CAST(ISNULL(D.FollowerCollectionEnabled, 0) AS BIT) AS FollowerCollectionEnabled
FROM dbo.Delegates D
WHERE D.AsyncID = @AsyncID
ORDER BY D.DelegateID;",
                    new { AsyncID = asyncID.Trim().TrimEnd('/') },
                    commandTimeout: 5));
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

        /// <summary>
        /// Returns only the logged-in delegate's own list (يوزر المندوب).
        /// Sibling/SelectDelegate children (e.g. تجهيز محل) are no longer exposed to the app.
        /// </summary>
        public async Task<IEnumerable<SelectDelegateGetDTO>?> GetDelegateSelect(int? delegateId)
        {
            if (delegateId is null or <= 0)
            {
                return Array.Empty<SelectDelegateGetDTO>();
            }

            using var connection = new SqlConnection(_connectionString);
            var result = await connection.QueryAsync<SelectDelegateGetDTO>(@"
SELECT
    d.DelegateID AS DelegateId,
    d.DelegateID AS DelegateChildId,
    d.DelegateID AS DelegateFatherId,
    d.UserID AS UserId,
    d.DelegateName,
    d.ReceiptName,
    CAST(ISNULL(d.UpdateReceipt, 0) AS BIT) AS UpdateReceipt,
    CAST(ISNULL(d.DeleteReceipt, 0) AS BIT) AS DeleteReceipt,
    CAST(ISNULL(d.DevicePaymentState, 0) AS BIT) AS DevicePaymentState,
    CAST(ISNULL(d.FollowerCollectionEnabled, 0) AS BIT) AS FollowerCollectionEnabled
FROM dbo.Delegates d
WHERE d.DelegateID = @DelegateID;",
                new { DelegateID = delegateId.Value },
                commandTimeout: 60);
            return DelegateListDedupe.DistinctByDelegateId(result).ToList();
        }

        /// <summary>
        /// Lists assigned to a User-based follower via dbo.FollowerUserLists (ListId = Delegates.DelegateID).
        /// Empty assignment → empty list (login still allowed).
        /// </summary>
        public async Task<IEnumerable<SelectDelegateGetDTO>?> GetFollowerCityLists(int followerUserId)
        {
            using var connection = new SqlConnection(_connectionString);
            await EnsureFollowerUserListsSchemaAsync(connection);
            await EnsureFollowerCollectionColumnAsync(connection);
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
    CAST(ISNULL(d.DevicePaymentState, 0) AS BIT) AS DevicePaymentState,
    CAST(ISNULL(d.FollowerCollectionEnabled, 0) AS BIT) AS FollowerCollectionEnabled
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
                try
                {
                    var result = await connection.QuerySingleOrDefaultAsync<LinkedFlagDTO>(
                        "Followers_IsLinked",
                        new { FatherID = fatherId, ChildID = childId },
                        commandType: CommandType.StoredProcedure);
                    return result != null && result.IsLinked == 1;
                }
                catch (SqlException)
                {
                    // SP missing / broken on some city DBs — callers fall back to GetDelegateSelect.
                    return false;
                }
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

        private static async Task EnsureFollowerCollectionColumnAsync(SqlConnection connection)
        {
            if (Volatile.Read(ref _followerCollectionColumnReady) == 1) return;
            await FollowerCollectionColumnGate.WaitAsync();
            try
            {
                if (Volatile.Read(ref _followerCollectionColumnReady) == 1) return;
                // Dynamic SQL: static ALTER on hot dbo.Delegates compiles even when IF is false
                // and contending SCH-M locks make every GetDelegateLogin exceed the APK 8s budget.
                await connection.ExecuteAsync(new CommandDefinition(@"
IF COL_LENGTH(N'dbo.Delegates', N'FollowerCollectionEnabled') IS NULL
    EXEC(N'ALTER TABLE dbo.Delegates ADD FollowerCollectionEnabled BIT NOT NULL CONSTRAINT DF_Delegates_FollowerCollectionEnabled DEFAULT (0)');
", commandTimeout: 5));
                Volatile.Write(ref _followerCollectionColumnReady, 1);
            }
            catch (SqlException)
            {
                // Leave ready=0 so a later request can retry; callers must not hang login.
            }
            finally
            {
                FollowerCollectionColumnGate.Release();
            }
        }
    }
}
