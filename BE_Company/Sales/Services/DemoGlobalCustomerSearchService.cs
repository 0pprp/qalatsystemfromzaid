using BE_Company.DTO;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Sales.Services
{
    public interface IGlobalCustomerSearchService
    {
        Task<IReadOnlyList<SalesCustomerSearchDTO>> SearchAsync(string query, CancellationToken ct);
        Task<CustomersGetDTO?> GetCustomerAsync(int customerId, CancellationToken ct);
    }

    /// <summary>
    /// Phase 1: Najaf Demo only, reusing Customers_GetAll.
    /// Callers stay stable when a later implementation fans out across CityLinks.
    /// </summary>
    public sealed class DemoGlobalCustomerSearchService : IGlobalCustomerSearchService
    {
        public const int MinimumQueryLength = 2;
        public const int ResultLimit = 50;

        private readonly SalesDevelopmentGuard _guard;
        private readonly IConfiguration _configuration;

        public DemoGlobalCustomerSearchService(SalesDevelopmentGuard guard, IConfiguration configuration)
        {
            _guard = guard;
            _configuration = configuration;
        }

        public async Task<IReadOnlyList<SalesCustomerSearchDTO>> SearchAsync(string query, CancellationToken ct)
        {
            var rows = await QueryCustomersAsync(query, ct);
            var cityValue = _configuration["SalesManagement:BranchId"];
            var cityName = _configuration["SalesManagement:BranchName"];
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                cityValue = _guard.CurrentCatalog ?? "branch";
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
                .Take(ResultLimit)
                .ToList();
        }

        public async Task<CustomersGetDTO?> GetCustomerAsync(int customerId, CancellationToken ct)
        {
            var rows = await QueryCustomersAsync(null, ct);
            return rows.FirstOrDefault(c => c.CustomerID == customerId);
        }

        private async Task<IEnumerable<CustomersGetDTO>> QueryCustomersAsync(string? textSearch, CancellationToken ct)
        {
            var cs = _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
            await using var connection = new SqlConnection(cs);
            return await connection.QueryAsync<CustomersGetDTO>(new CommandDefinition(
                "Customers_GetAll",
                new
                {
                    DelegateID = (int?)null,
                    TextSearch = textSearch,
                    ShowType = "الجميع"
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));
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
