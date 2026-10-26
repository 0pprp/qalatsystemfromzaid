using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_Company.Repository
{
    public class CustomersSalesRepository : ICustomersSalesRepository
    {
        private readonly string _connectionString;

        public CustomersSalesRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<CustomersSalesGetDTO?> CustomersSales_GetByID(int? customerSaleID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryFirstOrDefaultAsync<CustomersSalesGetDTO>("CustomersSales_GetByID",
                new
                {
                    CustomerSaleID = customerSaleID,
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<CustomersSalesGetDTO?> CustomersSales_Create(CustomersSalesPostDTO customersSalesPostDTO)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var tx = await connection.BeginTransactionAsync();
            try
            {
                // Temp lines + sale SP share one connection/transaction so concurrent users
                // cannot steal each other's SelectItemSalesTemporary rows mid-flight.
                foreach (var item in customersSalesPostDTO.Contents.Where(c => c.ItemID != null))
                {
                    await connection.ExecuteAsync(
                        "SelectItemSalesTemporaryPost_Create",
                        new
                        {
                            ItemID = item.ItemID,
                            Quantity = item.Quantity,
                            UserCreateID = customersSalesPostDTO.UserCreateID,
                        },
                        transaction: (SqlTransaction)tx,
                        commandType: CommandType.StoredProcedure);
                }

                var created = await connection.QueryFirstOrDefaultAsync<CustomersSalesGetDTO>(
                    "CustomersSales_Create",
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
                        // Historical IQD/box unit factor — do not change without balance reconciliation (F25).
                        DiscountAmountTotal = customersSalesPostDTO.DiscountAmountTotal / 1448,
                        DiscountAmountTotalDay = customersSalesPostDTO.DiscountAmountDay / 1448
                    },
                    transaction: (SqlTransaction)tx,
                    commandType: CommandType.StoredProcedure);

                await tx.CommitAsync();
                return created;
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<bool?> SelectItemSalesTemporaryPost_Create(SelectItemSalesTemporaryPostDTO selectItemSalesTemporaryPostDTO)
        {
            await using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<bool>("SelectItemSalesTemporaryPost_Create",
                new
                {
                    ItemID = selectItemSalesTemporaryPostDTO.ItemID,
                    Quantity = selectItemSalesTemporaryPostDTO.Quantity,
                    UserCreateID = selectItemSalesTemporaryPostDTO.UserCreateID,
                },
                commandType: CommandType.StoredProcedure);
        }


        public async Task<bool?> CustomersSales_Delete(int? customerSaleID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("CustomersSales_Delete",
                new
                {
                    CustomerSaleID = customerSaleID,
                    UserID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<bool?> CustomersSales_UpdateDiscount(int? customerSaleID, DiscountDTO discountDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("CustomersSales_UpdateDiscount",
                new
                {
                    CustomerSaleID = customerSaleID,
                    DiscountAmountTotal = discountDTO.DiscountAmountTotalDenar / 1448,
                    DiscountAmountTotalDay = discountDTO.DiscountAmountTotalDayDenar / 1448,
                    UserUpdateID = discountDTO.UserUpdateID,
                    DateCreate = discountDTO.DateCreate,
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<IEnumerable<CustomersSalesGetDTO>?> CustomersSales_GetAll(DateTime? fromDate, DateTime? toDate, int? delegateID, string? customerName, string? itemName, string? saleName)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryAsync<CustomersSalesGetDTO>("CustomersSales_GetAll",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                    DelegateID = delegateID,
                    CustomerName = customerName,
                    ItemName = itemName,
                    SaleName = saleName
                },
                commandType: CommandType.StoredProcedure);

                return result;
            }
        }

        public async Task<CustomersGetDTO?> Customers_GetByCustomerID(int? customerID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QuerySingleOrDefaultAsync<CustomersGetDTO>("Customers_GetByCustomerID",
                new
                {
                    CustomerID = customerID,
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
        }

        public async Task<IEnumerable<CustomersSalesGetDTO>?> CustomersSales_GetByCustomerIDNew(int? customerID, DateTime? fromDate, DateTime? toDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                // Use inline SQL to query the view directly — no SP deployment needed
                var sql = @"
                    SELECT * FROM View_CustomersSales
                    WHERE (@CustomerID IS NULL OR CustomerID = @CustomerID)
                      AND (@FromDate IS NULL OR CONVERT(DATE, DateCreate) >= CONVERT(DATE, @FromDate))
                      AND (@ToDate IS NULL OR CONVERT(DATE, DateCreate) <= CONVERT(DATE, @ToDate))
                    ORDER BY DateCreate DESC";

                var result = await connection.QueryAsync<CustomersSalesGetDTO>(sql,
                    new
                    {
                        CustomerID = customerID,
                        FromDate = fromDate,
                        ToDate = toDate
                    });
                return result;
            }
        }
    }
}
