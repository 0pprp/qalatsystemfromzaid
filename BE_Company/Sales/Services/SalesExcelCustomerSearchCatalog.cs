using BE_Company.DTO;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Sales.Services
{
    public interface ISalesExcelCustomerSearchCatalog
    {
        Task<IReadOnlyList<SalesExcelSearchCustomerRow>> LoadCustomersAsync(CancellationToken ct);
        Task<IReadOnlyList<SalesExcelSearchSaleRow>> LoadSalesAsync(IReadOnlyCollection<int> customerIds, CancellationToken ct);
        string CityValue { get; }
        string CityName { get; }
        int WriteCount { get; }
    }

    public sealed class SalesExcelCustomerSearchCatalog : ISalesExcelCustomerSearchCatalog
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly IConfiguration _configuration;

        public SalesExcelCustomerSearchCatalog(SalesDevelopmentGuard guard, IConfiguration configuration)
        {
            _guard = guard;
            _configuration = configuration;
            var cityValue = _configuration["SalesManagement:BranchId"];
            var cityName = _configuration["SalesManagement:BranchName"];
            if (string.IsNullOrWhiteSpace(cityValue))
            {
                cityValue = _guard.CurrentCatalog ?? "branch";
            }

            CityValue = cityValue;
            CityName = SalesCityDisplay.HumanName(cityName, cityValue, cityValue);
        }

        public string CityValue { get; }
        public string CityName { get; }
        public int WriteCount => 0;

        public async Task<IReadOnlyList<SalesExcelSearchCustomerRow>> LoadCustomersAsync(CancellationToken ct)
        {
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            try
            {
                var rows = await connection.QueryAsync<SalesExcelSearchCustomerRow>(new CommandDefinition(
                    @"SELECT CustomerID AS CustomerId,
                             CustomerName AS FullName,
                             PhoneNumber AS Phone,
                             CityName AS Province,
                             Address,
                             DelegateName,
                             DelegateID AS DelegateId,
                             AmountTotalSales,
                             ReceiptsTotal,
                             AmountRemaining
                      FROM View_CustomersDelegate
                      WHERE CustomerState = 1",
                    cancellationToken: ct));
                return rows.ToList();
            }
            catch
            {
                await using var fallbackConnection = new SqlConnection(cs);
                var fallback = await fallbackConnection.QueryAsync<CustomersGetDTO>(new CommandDefinition(
                    "Customers_GetAll",
                    new
                    {
                        DelegateID = (int?)null,
                        TextSearch = (string?)null,
                        ShowType = "الجميع"
                    },
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: ct));
                return fallback
                    .Where(c => c.CustomerID is > 0)
                    .Select(c => new SalesExcelSearchCustomerRow
                    {
                        CustomerId = c.CustomerID!.Value,
                        FullName = c.CustomerName ?? string.Empty,
                        Phone = c.PhoneNumber,
                        Province = c.CityName,
                        Address = c.Address,
                        DelegateName = c.DelegateName,
                        DelegateId = c.DelegateID,
                        AmountTotalSales = c.AmountTotalSales,
                        ReceiptsTotal = c.ReceiptsTotal,
                        AmountRemaining = c.AmountRemaining
                    })
                    .ToList();
            }
        }

        public async Task<IReadOnlyList<SalesExcelSearchSaleRow>> LoadSalesAsync(
            IReadOnlyCollection<int> customerIds,
            CancellationToken ct)
        {
            if (customerIds.Count == 0)
            {
                return [];
            }

            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var result = new List<SalesExcelSearchSaleRow>();
            foreach (var chunk in customerIds.Distinct().Chunk(400))
            {
                var rows = await connection.QueryAsync<SalesExcelSearchSaleRow>(new CommandDefinition(
                    @"SELECT CustomerID AS CustomerId,
                             CustomerSaleID AS SaleId,
                             DateCreate AS SaleDate,
                             COALESCE(AmountTotalSalesDenar, AmountTotalDenar, 0) AS SaleAmount,
                             AccountZero
                      FROM View_CustomersSales
                      WHERE CustomerID IN @Ids
                      ORDER BY DateCreate DESC, CustomerSaleID DESC",
                    new { Ids = chunk.ToArray() },
                    cancellationToken: ct));
                result.AddRange(rows);
            }

            return result;
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
    }
}
