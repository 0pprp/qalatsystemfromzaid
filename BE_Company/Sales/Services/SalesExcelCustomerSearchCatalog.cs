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
                IEnumerable<SalesExcelSearchSaleRow> rows;
                try
                {
                    rows = await connection.QueryAsync<SalesExcelSearchSaleRow>(new CommandDefinition(
                        @"SELECT s.CustomerID AS CustomerId,
                                 s.CustomerSaleID AS SaleId,
                                 s.DateCreate AS SaleDate,
                                 COALESCE(s.AmountTotalSalesDenar, s.AmountTotalDenar, 0) AS SaleAmount,
                                 CAST(s.ReceiptsTotal AS float) AS PaidAmount,
                                 CAST(s.AmountRemaining AS float) AS RemainingAmount,
                                 s.AccountZero,
                                 s.ItemsNames,
                                 CAST(NULL AS int) AS PaymentCount,
                                 CAST(NULL AS datetime2) AS LastPaymentDate
                          FROM View_CustomersSales s
                          WHERE s.CustomerID IN @Ids
                          ORDER BY s.DateCreate DESC, s.CustomerSaleID DESC",
                        new { Ids = chunk.ToArray() },
                        cancellationToken: ct));
                }
                catch
                {
                    rows = await connection.QueryAsync<SalesExcelSearchSaleRow>(new CommandDefinition(
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
                }

                var list = rows.ToList();
                await AttachPaymentStatsAsync(connection, list, ct);
                result.AddRange(list);
            }

            return result;
        }

        private static async Task AttachPaymentStatsAsync(
            SqlConnection connection,
            List<SalesExcelSearchSaleRow> sales,
            CancellationToken ct)
        {
            var saleIds = sales.Where(s => s.SaleId > 0).Select(s => s.SaleId).Distinct().ToArray();
            if (saleIds.Length == 0)
            {
                return;
            }

            try
            {
                var stats = (await connection.QueryAsync<(int SaleId, int PaymentCount, DateTime? LastPaymentDate, double? PaidAmount)>(
                    new CommandDefinition(
                        @"SELECT CustomerSaleID AS SaleId,
                                 COUNT(CustomerPaymentID) AS PaymentCount,
                                 MAX(PaymentDate) AS LastPaymentDate,
                                 CAST(SUM(AmountDenar) AS float) AS PaidAmount
                          FROM dbo.View_CustomersPaymentsDelegate
                          WHERE CustomerSaleID IN @Ids
                          GROUP BY CustomerSaleID",
                        new { Ids = saleIds },
                        cancellationToken: ct))).ToDictionary(x => x.SaleId);

                foreach (var sale in sales)
                {
                    if (!stats.TryGetValue(sale.SaleId, out var st))
                    {
                        continue;
                    }

                    sale.PaymentCount = st.PaymentCount;
                    sale.LastPaymentDate = st.LastPaymentDate;
                    if (sale.PaidAmount is null or 0)
                    {
                        sale.PaidAmount = st.PaidAmount;
                    }
                }
            }
            catch
            {
                // Payment view may lack CustomerSaleID — leave counts null → غير متوفر in UI.
            }
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
    }
}
