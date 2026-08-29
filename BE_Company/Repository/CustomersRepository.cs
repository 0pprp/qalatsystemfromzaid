using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using System;

namespace BE_Company.Repository
{
    public class CustomersRepository : ICustomersRepository
    {
        private readonly string _connectionString;

        public CustomersRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<CustomersGetDTO?> CustomersSalesCustomer_Create(CustomersSalesPostDTO customersSalesPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                foreach (var item in customersSalesPostDTO.Contents.Where(c => c.ItemID != null))
                {
                    await SelectItemSalesTemporaryPost_Create(new SelectItemSalesTemporaryPostDTO()
                    {
                        ItemID = item.ItemID,
                        Quantity = item.Quantity,
                        UserCreateID = customersSalesPostDTO.UserCreateID
                    });
                }
                return await connection.QueryFirstOrDefaultAsync<CustomersGetDTO>("CustomersSalesCustomer_Create",
                new
                {
                    DelegateID = customersSalesPostDTO.DelegateID,
                    StoreID = customersSalesPostDTO.StoreID,
                    UserID = customersSalesPostDTO.UserCreateID,
                    CustomerName = customersSalesPostDTO.CustomerName,
                    PhoneNumber = customersSalesPostDTO.PhoneNumber,
                    Address = customersSalesPostDTO.Address,
                    ShopName = customersSalesPostDTO.ShopName,
                    NearestFunctionPoint = customersSalesPostDTO.NearestFunctionPoint,
                    SaleName = customersSalesPostDTO.SaleName,
                    ReceiptName = customersSalesPostDTO.ReceiptName,
                    Notes = customersSalesPostDTO.Notes,
                    DateCreate = customersSalesPostDTO.DateCreate,
                    DiscountAmountTotal = customersSalesPostDTO.DiscountAmountTotal / 1448,
                    DiscountAmountTotalDay = customersSalesPostDTO.DiscountAmountDay / 1448
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<bool?> SelectItemSalesTemporaryPost_Create(SelectItemSalesTemporaryPostDTO selectItemSalesTemporaryPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<bool>("SelectItemSalesTemporaryPost_Create",
                new
                {
                    ItemID = selectItemSalesTemporaryPostDTO.ItemID,
                    Quantity = selectItemSalesTemporaryPostDTO.Quantity,
                    UserCreateID = selectItemSalesTemporaryPostDTO.UserCreateID,
                },
                commandType: CommandType.StoredProcedure);
            }
        }



        public async Task<IEnumerable<CustomersGetDTO>?> Customers_GetAll(int? delegateID, string? textSearch, string? showType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersGetDTO>("Customers_GetAll",
                new
                {
                    DelegateID = delegateID,
                    TextSearch = textSearch,
                    ShowType = showType
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersGetDTO>?> Customers_GetByLastPaymentDate(DateTime? fromDate, DateTime? toDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersGetDTO>("Customers_GetByLastPaymentDate",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersGetDTO>?> Customers_GetAllZero(DateTime? fromDate, DateTime? toDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersGetDTO>("Customers_GetAllZero",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<CustomerWeekPaymentsGetDTO>?> Customers_GetWeekReceipt(int? delegateID, string? showType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomerWeekPaymentsGetDTO>("Customers_GetWeekReceipt",
                new
                {
                    DelegateID = delegateID,
                    ShowType = showType
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<CustomerMonthPaymentsGetDTO>?> Customers_GetMonthReceipt(int? delegateID, string? showType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomerMonthPaymentsGetDTO>("Customers_GetMonthReceipt",
                new
                {
                    DelegateID = delegateID,
                    ShowType = showType
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersFollowGetDTO>?> Customers_Follow(int? delegateID, DateTime? paymentDate, string? showType)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersFollowGetDTO>("Customers_Follow",
                new
                {
                    DelegateID = delegateID,
                    PaymentDate = paymentDate,
                    ShowType = showType
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<CustomersInfoSimpleGetDTO?> Customers_InfoSimple(int? customerID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<CustomersInfoSimpleGetDTO>("Customers_InfoSimple",
                new
                {
                    CustomerID = customerID,
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<CustomersGetDTO?> Customers_Update(int? customerID,CustomersPutDTO customersPutDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<CustomersGetDTO>("Customers_Update",
                new
                {
                    CustomerID = customerID,
                    UserUpdateID = customersPutDTO.UserUpdateID,
                    CustomerName = customersPutDTO.CustomerName,
                    PhoneNumber = customersPutDTO.PhoneNumber,
                    Address = customersPutDTO.Address,
                    ShopName = customersPutDTO.ShopName,
                    SaleName = customersPutDTO.SaleName,
                    Notes = customersPutDTO.Notes,
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<CustomersGetDTO?> Customers_Move(int? customerID, int? delegateID, int? userUpdateID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<CustomersGetDTO>("Customers_Move",
                new
                {
                    CustomerID = customerID,
                    DelegateID = delegateID,
                    UserUpdateID = userUpdateID
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }



        public async Task<bool?> Customers_MoveLegal(int? customerID, int? userID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<CustomersGetDTO>("Customers_MoveLegal",
                new
                {
                    CustomerID = customerID,
                    UserID = userID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }
    }
}
