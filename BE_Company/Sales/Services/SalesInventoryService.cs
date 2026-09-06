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
        Task<IReadOnlyList<SalesCustomerListDTO>> GetActiveCustomerListsAsync(CancellationToken ct);
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
                     ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
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

            return rows.Where(i => i.ItemID.HasValue && !IsHiddenFromSalesStaff(i.ItemName)).Select(Map).ToList();
        }

        public async Task<SalesInventoryItemDTO?> GetProductAsync(int productId, CancellationToken ct)
        {
            var items = await GetBranchInventoryAsync(ct);
            return items.FirstOrDefault(i => i.ProductId == productId);
        }

        public async Task<IReadOnlyList<SalesCustomerListDTO>> GetActiveCustomerListsAsync(CancellationToken ct)
        {
            var cs = _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
            await using var connection = new SqlConnection(cs);
            try
            {
                var rows = await connection.QueryAsync<SalesCustomerListDTO>(new CommandDefinition(
                    SalesActiveCustomerListsQuery.Sql,
                    cancellationToken: ct));
                return rows.Where(r => r.ListId > 0 && !string.IsNullOrWhiteSpace(r.ListName)).ToList();
            }
            catch
            {
                return [];
            }
        }

        public static async Task AttachListNamesAsync(
            SqlConnection connection,
            IEnumerable<SalesDraftDTO> drafts,
            CancellationToken ct)
        {
            var list = drafts as IList<SalesDraftDTO> ?? drafts.ToList();
            var ids = list
                .Where(d => d.CustomerListId is > 0)
                .Select(d => d.CustomerListId!.Value)
                .Distinct()
                .ToArray();
            if (ids.Length == 0)
            {
                return;
            }

            try
            {
                var rows = await connection.QueryAsync<(int Id, string? Name)>(new CommandDefinition(
                    "SELECT DelegateID AS Id, DelegateName AS Name FROM dbo.Delegates WHERE DelegateID IN @Ids",
                    new { Ids = ids },
                    cancellationToken: ct));
                var map = rows
                    .GroupBy(r => r.Id)
                    .ToDictionary(g => g.Key, g => g.First().Name ?? string.Empty);
                foreach (var draft in list)
                {
                    if (draft.CustomerListId is int id
                        && map.TryGetValue(id, out var name)
                        && !string.IsNullOrWhiteSpace(name))
                    {
                        draft.CustomerListName = name;
                    }
                }
            }
            catch
            {
                // Isolated tests may not have dbo.Delegates.
            }
        }

        public static async Task<string?> NameAsync(SqlConnection connection, int? listId, CancellationToken ct)
        {
            if (listId is not > 0)
            {
                return null;
            }

            try
            {
                return await connection.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
                    "SELECT TOP 1 DelegateName FROM dbo.Delegates WHERE DelegateID = @Id",
                    new { Id = listId },
                    cancellationToken: ct));
            }
            catch
            {
                return null;
            }
        }

        public static bool IsHiddenFromSalesStaff(string? productName)
        {
            var n = NormalizeArabic(productName);
            if (string.IsNullOrWhiteSpace(n))
            {
                return false;
            }

            if (n.Contains("تجهيز", StringComparison.Ordinal))
            {
                return true;
            }

            var mobile = n.Contains("موبايل", StringComparison.Ordinal) || n.Contains("موبايلات", StringComparison.Ordinal);
            return mobile && n.Contains("خارج", StringComparison.Ordinal);
        }

        internal static string NormalizeArabic(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value
                .Replace("أ", "ا", StringComparison.Ordinal)
                .Replace("إ", "ا", StringComparison.Ordinal)
                .Replace("آ", "ا", StringComparison.Ordinal)
                .Replace("ة", "ه", StringComparison.Ordinal)
                .Replace("ى", "ي", StringComparison.Ordinal)
                .Replace("ـ", "", StringComparison.Ordinal)
                .Trim();
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

    /// <summary>
    /// قوائم الزبون = كل Delegates في قاعدة الفرع الحالي، بدون فلترة نشاط أو تسديد.
    /// </summary>
    public static class SalesActiveCustomerListsQuery
    {
        public const string Sql = """
SELECT d.DelegateID AS ListId, d.DelegateName AS ListName
FROM dbo.Delegates d
ORDER BY d.DelegateName
""";
    }
}
