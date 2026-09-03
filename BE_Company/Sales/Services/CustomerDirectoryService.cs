using BE_Company.DTO;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Read-only lookup against this instance's DataBaseConnection.
    /// Used by Flutter global search. Auth is enforced by SalesController:
    /// a validated sales JWT (shared signing key) or DirectorySearchKey.
    /// The token is never used to resolve a local UserID or to write.
    /// </summary>
    public sealed class CustomerDirectoryService
    {
        private readonly IConfiguration _configuration;
        private readonly SalesDevelopmentGuard _guard;

        public CustomerDirectoryService(IConfiguration configuration, SalesDevelopmentGuard guard)
        {
            _configuration = configuration;
            _guard = guard;
        }

        public async Task<IReadOnlyList<SalesCustomerSearchDTO>> SearchAsync(string query, CancellationToken ct)
        {
            var cs = _configuration.GetConnectionString("DataBaseConnection")
                     ?? _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("No branch database connection.");
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<CustomersGetDTO>(new CommandDefinition(
                "Customers_GetAll",
                new
                {
                    DelegateID = (int?)null,
                    TextSearch = query,
                    ShowType = "الجميع"
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

            var catalog = new SqlConnectionStringBuilder(cs).InitialCatalog;
            var cityValue = _configuration["SalesManagement:BranchId"];
            var cityName = _configuration["SalesManagement:BranchName"];
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                cityValue = catalog;
            }
            if (string.IsNullOrWhiteSpace(cityName))
            {
                cityName = cityValue;
            }

            return rows
                .Where(c => c.CustomerID.HasValue)
                .Select(c => new SalesCustomerSearchDTO
                {
                    CustomerId = c.CustomerID!.Value,
                    FullName = c.CustomerName ?? string.Empty,
                    Phone = c.PhoneNumber,
                    Province = string.IsNullOrWhiteSpace(c.CityName) ? cityName : c.CityName,
                    SalePrice = ToMoney(c.AmountTotalSales),
                    SourceCityValue = cityValue,
                    SourceCityName = cityName
                })
                .Take(DemoGlobalCustomerSearchService.ResultLimit)
                .ToList();
        }

        private static decimal ToMoney(double? value)
        {
            if (!value.HasValue)
            {
                return 0;
            }
            return Math.Round((decimal)value.Value, 0, MidpointRounding.AwayFromZero);
        }
    }
}
