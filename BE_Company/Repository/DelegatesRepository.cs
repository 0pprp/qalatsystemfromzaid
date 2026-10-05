using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System;

namespace BE_Company.Repository
{
    public class DelegatesRepository : IDelegatesRepository
    {
        private readonly string _connectionString;

        public DelegatesRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<DelegatesGetDTO?> Delegates_Create(DelegatesPostDTO delegatesPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<DelegatesGetDTO>("Delegates_Create",
                new
                {
                    DelegateName = delegatesPostDTO.DelegateName,
                    UserCreateID = delegatesPostDTO.UserCreateID,
                    Address = delegatesPostDTO.Address,
                    PhoneNumber = delegatesPostDTO.PhoneNumber,
                    ReceiptName = delegatesPostDTO.ReceiptName,
                    AsyncID = delegatesPostDTO.AsyncID,
                    Notes = delegatesPostDTO.Notes
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<DelegatesGetDTO?> Delegates_Update(DelegatesPutDTO delegatesPutDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<DelegatesGetDTO>("Delegates_Update",
                new
                {
                    DelegateID = delegatesPutDTO.DelegateID,
                    DelegateName = delegatesPutDTO.DelegateName,
                    UserUpdateID = delegatesPutDTO.UserUpdateID,
                    Address = delegatesPutDTO.Address,
                    PhoneNumber = delegatesPutDTO.PhoneNumber,
                    ReceiptName = delegatesPutDTO.ReceiptName,
                    AsyncID = delegatesPutDTO.AsyncID,
                    Notes = delegatesPutDTO.Notes
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> Delegates_Delete(int? delegateID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Delegates_Delete",
                new { DelegateID = delegateID, UserDeleteID = userDeleteID },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<IEnumerable<DelegatesGetDTO>?> Delegates_GetAll(string? delegateName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<DelegatesGetDTO>("Delegates_GetAll",
                new { DelegateName = delegateName },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<DelegatesDataGetDTO>?> Delegates_GetDataAll()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<DelegatesDataGetDTO>("Delegates_GetDataAll",
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<StatisticsGetDTO>?> Delegates_Statistics(DateTime? fromDate, DateTime? toDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<StatisticsGetDTO>("Delegates_Statistics",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<DelegatesDashboardDataGetDTO>?> Delegates_GetDashboardData(DateTime? fromDate, DateTime? toDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<DelegatesDashboardDataGetDTO>("Delegates_GetDashboardData",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<NoStatisticsGetDTO>?> Delegates_NoStatistics(DateTime? fromDate, DateTime? toDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<NoStatisticsGetDTO>("Delegates_NoStatistics",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<SelectDelegateGetDTO>?> SelectDelegate_GetByDelegateID(int? delegateID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<SelectDelegateGetDTO>("SelectDelegate_GetByDelegateID",
                new
                {
                    DelegateID = delegateID,
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<SelectDelegateGetDTO?> SelectDelegate_Create(SelectDelegatePostDTO selectDelegatePostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<SelectDelegateGetDTO>("SelectDelegate_Create",
                new
                {
                    DelegateFatherID = selectDelegatePostDTO.DelegateFatherID,
                    DelegateChildID = selectDelegatePostDTO.DelegateChildID,
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> SelectDelegate_Delete(int? selectDelegateID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("SelectDelegate_Delete",
                new { SelectDelegateID = selectDelegateID },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task EnsureFollowerCollectionColumnAsync(CancellationToken ct = default)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(new CommandDefinition(@"
IF COL_LENGTH(N'dbo.Delegates', N'FollowerCollectionEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Delegates
        ADD FollowerCollectionEnabled BIT NOT NULL
            CONSTRAINT DF_Delegates_FollowerCollectionEnabled DEFAULT (0);
END", cancellationToken: ct));
        }

        public async Task<IReadOnlyList<FollowerCollectionListDto>> GetFollowerCollectionListsAsync(
            CancellationToken ct = default)
        {
            await EnsureFollowerCollectionColumnAsync(ct);
            await using var connection = new SqlConnection(_connectionString);
            var rows = await connection.QueryAsync<FollowerCollectionListDto>(new CommandDefinition(@"
SELECT
    d.DelegateID AS DelegateId,
    d.DelegateName,
    CAST(ISNULL(d.FollowerCollectionEnabled, 0) AS BIT) AS FollowerCollectionEnabled,
    ISNULL((
        SELECT COUNT(1) FROM dbo.Customers c
        WHERE c.DelegateID = d.DelegateID AND ISNULL(c.CustomerState, 1) = 1
    ), 0) AS NumberOfCustomer,
    ISNULL((
        SELECT COUNT(1)
        FROM dbo.View_CustomersDelegate vc
        WHERE vc.DelegateID = d.DelegateID AND ISNULL(vc.AmountRemaining, 0) > 0
    ), 0) AS NumberOfCustomerIsNotZero
FROM dbo.Delegates d
WHERE ISNULL(d.DelegateState, 0) = 1
ORDER BY d.DelegateName;", cancellationToken: ct));
            return rows.ToList();
        }

        public async Task<bool> SetFollowerCollectionEnabledAsync(
            int delegateId,
            bool enabled,
            int? userId,
            CancellationToken ct = default)
        {
            if (delegateId <= 0)
            {
                return false;
            }

            await EnsureFollowerCollectionColumnAsync(ct);
            await using var connection = new SqlConnection(_connectionString);

            // Prefer SP when present; otherwise inline update + activity.
            var hasSp = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT CASE WHEN OBJECT_ID(N'dbo.UpdateFollowerCollectionEnabled', N'P') IS NULL THEN 0 ELSE 1 END",
                cancellationToken: ct));

            if (hasSp == 1)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UpdateFollowerCollectionEnabled",
                    new { DelegateID = delegateId, State = enabled, UserID = userId },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
                return true;
            }

            await connection.ExecuteAsync(new CommandDefinition(@"
DECLARE @Name NVARCHAR(200) = (SELECT TOP 1 DelegateName FROM dbo.Delegates WHERE DelegateID = @DelegateID);
DECLARE @Old NVARCHAR(10) = (SELECT TOP 1 CONVERT(ISNULL(FollowerCollectionEnabled, 0) AS NVARCHAR(10))
                             FROM dbo.Delegates WHERE DelegateID = @DelegateID);
INSERT INTO dbo.Activities (UserID, ActivityDescription, ActivityDate, AsyncState, AsyncID)
VALUES (@UserID,
        N'تم تعديل جباية المتابعين للقائمة ' + ISNULL(@Name, N'') + N' من ' + ISNULL(@Old, N'0')
            + N' الى ' + CONVERT(NVARCHAR(10), @State),
        GETUTCDATE(), 'false', NEWID());
UPDATE dbo.Delegates SET FollowerCollectionEnabled = @State WHERE DelegateID = @DelegateID;",
                new { DelegateID = delegateId, State = enabled, UserID = userId },
                cancellationToken: ct));
            return true;
        }
    }
}
