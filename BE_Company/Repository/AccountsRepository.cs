using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BE_Company.Repository
{
    public class AccountsRepository : IAccountsRepository
    {
        private readonly string _connectionString;

        public AccountsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<IEnumerable<BoxsGetDTO>?> Boxs_GetAll()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<BoxsGetDTO>("Boxs_GetAll", commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<BoxsGetDTO?> Boxs_GetByBoxID(int? boxID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<BoxsGetDTO>("Boxs_GetByBoxID",
                new
                {
                    BoxID = boxID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<BoxsDataGetDTO>?> Boxs_GetAllData()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<BoxsDataGetDTO>("Boxs_GetAllData", commandType: CommandType.StoredProcedure);
                return result;
            }
        }


        public async Task<IEnumerable<AddToBoxsGetDTO>?> AddToBoxs_GetByDateByBoxID(DateTime? fromDate, DateTime? toDate, int? boxID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<AddToBoxsGetDTO>("AddToBoxs_GetByDateByBoxID",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    BoxID = boxID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<WithdrawalFromBoxsGetDTO>?> WithdrawalFromBoxs_GetByDateByBoxID(DateTime? fromDate, DateTime? toDate, int? boxID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<WithdrawalFromBoxsGetDTO>("WithdrawalFromBoxs_GetByDateByBoxID",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    BoxID = boxID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<TransferBoxsGetDTO>?> TransferBoxs_GetByDate(DateTime? fromDate, DateTime? toDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<TransferBoxsGetDTO>("TransferBoxs_GetByDate",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<bool?> Boxs_Delete(int? boxID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Boxs_Delete",
                new
                {
                    BoxID = boxID,
                    UserDeleteID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<bool?> AddToBox_Delete(int? addToBoxID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("AddToBox_Delete",
                new
                {
                    AddToBoxID = addToBoxID,
                    UserID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<bool?> Boxs_Create(BoxsPostDTO boxsPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Boxs_Create",
                new
                {
                    BoxName = boxsPostDTO.BoxName,
                    CreateUserID = boxsPostDTO.CreateUserID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<bool?> Boxs_Update(int? boxID,BoxsPutDTO boxsPutDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Boxs_Update",
                new
                {
                    BoxID = boxID,
                    BoxName = boxsPutDTO.BoxName,
                    UpdateUserID = boxsPutDTO.UpdateUserID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<bool?> WithdrawalFromBox_Delete(int? withdrawalFromBoxID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("WithdrawalFromBox_Delete",
                new
                {
                    WithdrawalFromBoxID = withdrawalFromBoxID,
                    UserID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<bool?> TransferBoxs_Delete(int? transferBoxID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("TransferBoxs_Delete",
                new
                {
                    TransferBoxID = transferBoxID,
                    UserID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<bool?> AddToBox_Create(AccountsPostDTO accountsPostDTO)
        {
            if (accountsPostDTO.Amount > 0)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var result = await connection.ExecuteAsync("AddToBox_Create",
                    new
                    {
                        UserID = accountsPostDTO.UserCreateID,
                        BoxID = accountsPostDTO.BoxID,
                        Notes = accountsPostDTO.Notes,
                        Amount = accountsPostDTO.Amount / 1448,
                    },
                    commandType: CommandType.StoredProcedure);
                }
            }
            return true;
        }

        public async Task<bool?> WithdrawalFromBox_Create(AccountsPostDTO accountsPostDTO)
        {
            if (accountsPostDTO.Amount > 0 && (await Boxs_GetByBoxID(accountsPostDTO.BoxID))?.AmountDenar >= accountsPostDTO.Amount)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var result = await connection.ExecuteAsync("WithdrawalFromBox_Create",
                    new
                    {
                        UserID = accountsPostDTO.UserCreateID,
                        BoxID = accountsPostDTO.BoxID,
                        Notes = accountsPostDTO.Notes,
                        Amount = accountsPostDTO.Amount / 1448,
                    },
                    commandType: CommandType.StoredProcedure);
                }
            }
            return true;
        }

        public async Task<bool?> TransferBoxs_Create(AccountsPostDTO accountsPostDTO)
        {
            if (accountsPostDTO.Amount > 0 && (await Boxs_GetByBoxID(accountsPostDTO.BoxID))?.AmountDenar >= accountsPostDTO.Amount)
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    var result = await connection.ExecuteAsync("TransferBoxs_Create",
                    new
                    {
                        UserID = accountsPostDTO.UserCreateID,
                        FromBoxID = accountsPostDTO.BoxID,
                        Notes = accountsPostDTO.Notes,
                        ToBoxID = accountsPostDTO.DestinationBoxID,
                        Amount = accountsPostDTO.Amount / 1448,
                    },
                    commandType: CommandType.StoredProcedure);
                }
            }
            return true;
        }
    }
}
