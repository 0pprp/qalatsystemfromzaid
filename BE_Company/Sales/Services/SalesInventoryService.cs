using BE_Company.DTO;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Sales.Services
{
    public interface ISalesInventoryService
    {
        Task<IReadOnlyList<SalesInventoryItemDTO>> GetBranchInventoryAsync(CancellationToken ct);
        Task<SalesInventoryItemDTO?> GetProductAsync(int productId, CancellationToken ct);
    }

    public sealed class SalesInventoryService : ISalesInventoryService
    {
        private readonly SalesDevelopmentGuard _guard;

        public SalesInventoryService(SalesDevelopmentGuard guard)
        {
            _guard = guard;
        }

        public async Task<IReadOnlyList<SalesInventoryItemDTO>> GetBranchInventoryAsync(CancellationToken ct)
        {
            var cs = _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("Sales module refused a non-demo connection.");
            await using var connection = new SqlConnection(cs);
            var rows = await connection.QueryAsync<ItemsGetDTO>(new CommandDefinition(
                "Items_GetAll",
                new
                {
                    StoreID = (int?)null,
                    ItemName = (string?)null,
                    ShowType = "المواد الحالية"
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: ct));

            return rows.Where(i => i.ItemID.HasValue).Select(Map).ToList();
        }

        public async Task<SalesInventoryItemDTO?> GetProductAsync(int productId, CancellationToken ct)
        {
            var items = await GetBranchInventoryAsync(ct);
            return items.FirstOrDefault(i => i.ProductId == productId);
        }

        private static SalesInventoryItemDTO Map(ItemsGetDTO item)
        {
            return new SalesInventoryItemDTO
            {
                ProductId = item.ItemID!.Value,
                ProductName = item.ItemName ?? string.Empty,
                AvailableQuantity = item.Quantity ?? 0,
                SalePrice = Math.Round((decimal)(item.ItemPriceDenar ?? 0), 0, MidpointRounding.AwayFromZero),
                DailyInstallment = item.AmountDayDenar is double amountDay
                    ? Math.Round((decimal)amountDay, 0, MidpointRounding.AwayFromZero)
                    : null,
                StoreId = item.StoreID,
                Notes = item.Notes
            };
        }
    }
}
