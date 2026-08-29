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
    }
}
