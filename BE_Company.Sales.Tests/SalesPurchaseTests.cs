using BE_Company.DTO;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Controllers;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class SalesPurchaseTests
    {
        [Fact]
        public void PurchaseEndpoints_AreSalesManagerOnly_AndEmployeeInventoryUnchanged()
        {
            Assert.Equal(
                SalesPolicies.SalesManager,
                typeof(SalesManagerController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);
            Assert.NotNull(typeof(SalesManagerController).GetMethod(nameof(SalesManagerController.CreatePurchase)));
            Assert.NotNull(typeof(SalesManagerController).GetMethod(nameof(SalesManagerController.Purchases)));

            var inventory = typeof(SalesController).GetMethod(nameof(SalesController.Inventory));
            Assert.Equal(
                SalesPolicies.SalesEmployee,
                inventory!.GetCustomAttributes<AuthorizeAttribute>().Single().Policy);
        }

        [Fact]
        public async Task Manager_CanCreatePurchase_AndStockIncreasesByQuantity()
        {
            var repo = Seed();
            var svc = new SalesPurchaseService(repo);

            var created = await svc.CreateAsync(Manager(), ValidRequest(), CancellationToken.None);

            Assert.True(created.BuyId > 0);
            Assert.Equal("INV-100", created.SupplierInvoiceNumber);
            Assert.Equal("مدير مبيعات", created.CreatedByUserType);
            Assert.Equal("مدير النجف", created.CreatedByDisplayName);
            Assert.Equal("najaf-demo", created.CreatedByBranchId);
            Assert.Equal(3, repo.ItemStock[5]);
            Assert.Equal(1, repo.OfficialBuyCalls);
        }

        [Fact]
        public async Task Employee_CannotCreatePurchase()
        {
            var svc = new SalesPurchaseService(Seed());
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.CreateAsync(Employee(), ValidRequest(), CancellationToken.None));
            Assert.Equal(403, ex.StatusCode);
        }

        [Fact]
        public async Task DuplicateSupplierInvoice_IsRejected()
        {
            var repo = Seed();
            var svc = new SalesPurchaseService(repo);
            await svc.CreateAsync(Manager(), ValidRequest("INV-100"), CancellationToken.None);

            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.CreateAsync(Manager(), ValidRequest("INV-100"), CancellationToken.None));

            Assert.Equal(409, ex.StatusCode);
            Assert.Contains("مسجّلة مسبقاً", ex.Message);
            Assert.Equal(3, repo.ItemStock[5]);
            Assert.Single(repo.Buys);
        }

        [Fact]
        public async Task SameItemDifferentInvoice_IsAllowed()
        {
            var repo = Seed();
            var svc = new SalesPurchaseService(repo);
            await svc.CreateAsync(Manager(), ValidRequest("INV-1"), CancellationToken.None);
            await svc.CreateAsync(Manager(), ValidRequest("INV-2"), CancellationToken.None);
            Assert.Equal(5, repo.ItemStock[5]);
            Assert.Equal(2, repo.Buys.Count);
        }

        [Fact]
        public async Task BranchPurchase_DoesNotChangeAnotherBranchStock()
        {
            var najaf = Seed("najaf-demo");
            var karbala = Seed("karbala-demo");
            karbala.ItemStock[5] = 8;
            var najafSvc = new SalesPurchaseService(najaf);
            var karbalaSvc = new SalesPurchaseService(karbala);

            await najafSvc.CreateAsync(Manager("najaf-demo", "النجف"), ValidRequest(), CancellationToken.None);

            Assert.Equal(3, najaf.ItemStock[5]);
            Assert.Equal(8, karbala.ItemStock[5]);
            Assert.Empty(karbala.Buys);
        }

        [Fact]
        public async Task FailedCreate_DoesNotLeaveInvoiceOrStockChange()
        {
            var repo = Seed();
            repo.FailAfterBuyHeader = true;
            var svc = new SalesPurchaseService(repo);

            await Assert.ThrowsAsync<SalesCompleteException>(() =>
                svc.CreateAsync(Manager(), ValidRequest(), CancellationToken.None));

            Assert.Empty(repo.Buys);
            Assert.Equal(1, repo.ItemStock[5]);
            Assert.Equal(0, repo.OfficialBuyCalls);
        }

        [Fact]
        public async Task NewQuantity_AppearsOnEmployeeInventorySource()
        {
            var repo = Seed();
            repo.ItemStock[5] = 0;
            var svc = new SalesPurchaseService(repo);

            Assert.Empty(repo.EmployeeInventory());
            await svc.CreateAsync(Manager(), ValidRequest(qty: 4), CancellationToken.None);

            var inventory = repo.EmployeeInventory();
            var item = Assert.Single(inventory);
            Assert.Equal(5, item.ProductId);
            Assert.Equal(4, item.AvailableQuantity);
        }

        [Fact]
        public void InvoiceNumber_IsNormalized_AndNotMatchedByItemName()
        {
            Assert.Equal("INV-9", SalesPurchaseRules.NormalizeInvoiceNumber("  INV-9  "));
            Assert.False(SalesPurchaseRules.HasInvoiceNumber("   "));
            Assert.True(SalesPurchaseRules.HasInvoiceNumber("A-1"));
        }

        private static SalesIdentity Manager(string branchId = "najaf-demo", string branchName = "النجف") => new()
        {
            EmployeeId = 9,
            EmployeeName = "مدير النجف",
            BranchId = branchId,
            BranchName = branchName,
            Role = SalesRoles.SalesManager,
            UserType = SalesRoles.UserTypeSalesManager
        };

        private static SalesIdentity Employee() => new()
        {
            EmployeeId = 1,
            EmployeeName = "موظف",
            BranchId = "najaf-demo",
            BranchName = "النجف",
            Role = SalesRoles.SalesEmployee,
            UserType = SalesRoles.UserTypeSalesEmployee
        };

        private static SalesPurchaseCreateDTO ValidRequest(string invoice = "INV-100", int qty = 2) => new()
        {
            SupplierId = 3,
            StoreId = 1,
            BoxId = 2,
            Date = new DateTime(2026, 9, 7),
            SupplierInvoiceNumber = invoice,
            TotalAmountSpent = 400000,
            Contents =
            [
                new SalesPurchaseLineDTO { ItemId = 5, Quantity = qty, ItemCostDenar = 200000 }
            ]
        };

        private static FakeSalesPurchaseRepository Seed(string branchId = "najaf-demo")
        {
            var repo = new FakeSalesPurchaseRepository { BranchId = branchId };
            repo.ItemStock[5] = 1;
            repo.ItemNames[5] = "ثلاجة سامسونج";
            repo.ItemCosts[5] = 200000;
            repo.Boxes[2] = 5_000_000;
            repo.SupplierAccounts[3] = 0;
            repo.ItemsInStore[1] = [5];
            return repo;
        }

        private sealed class FakeSalesPurchaseRepository : ISalesPurchaseRepository
    {
        public string BranchId { get; set; } = "najaf-demo";
        public readonly Dictionary<int, int> ItemStock = new();
        public readonly Dictionary<int, string> ItemNames = new();
        public readonly Dictionary<int, double> ItemCosts = new();
        public readonly Dictionary<int, double> Boxes = new();
        public readonly Dictionary<int, double> SupplierAccounts = new();
        public readonly Dictionary<int, List<int>> ItemsInStore = new();
        public readonly List<SalesPurchaseListItemDTO> Buys = [];
        public int OfficialBuyCalls { get; private set; }
        public bool FailAfterBuyHeader { get; set; }

        public Task EnsureSchemaAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<bool> InvoiceExistsAsync(int supplierId, string invoiceNumber, CancellationToken ct)
        {
            var normalized = SalesPurchaseRules.NormalizeInvoiceNumber(invoiceNumber);
            return Task.FromResult(Buys.Any(b =>
                b.SupplierId == supplierId
                && string.Equals(b.SupplierInvoiceNumber, normalized, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<SalesPurchaseLookupsDTO> GetLookupsAsync(CancellationToken ct) =>
            Task.FromResult(new SalesPurchaseLookupsDTO
            {
                Suppliers = [new SuppliersGetDTO { SupplierID = 3, SupplierName = "مورد", AmountAccount = 0 }],
                Stores = [new StoresDataGetDTO { StoreID = 1, StoreName = "المخزن الرئيسي" }],
                Boxes = [new BoxsGetDTO { BoxID = 2, BoxName = "الصندوق", AmountDenar = Boxes.GetValueOrDefault(2) }]
            });

        public Task<IReadOnlyList<SalesPurchaseItemOptionDTO>> GetItemsAsync(int storeId, string? search, CancellationToken ct)
        {
            var ids = ItemsInStore.GetValueOrDefault(storeId) ?? [];
            IReadOnlyList<SalesPurchaseItemOptionDTO> rows = ids.Select(id => new SalesPurchaseItemOptionDTO
            {
                ItemId = id,
                ItemName = ItemNames.GetValueOrDefault(id) ?? "",
                ItemCostDenar = ItemCosts.GetValueOrDefault(id),
                DisplayName = ItemNames.GetValueOrDefault(id) ?? ""
            }).ToList();
            return Task.FromResult(rows);
        }

        public Task<IReadOnlyList<SalesPurchaseListItemDTO>> ListAsync(
            DateTime? fromDate,
            DateTime? toDate,
            string? textSearch,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesPurchaseListItemDTO>>(Buys.ToList());

        public Task<double> GetBoxAmountDenarAsync(int boxId, CancellationToken ct) =>
            Task.FromResult(Boxes.GetValueOrDefault(boxId));

        public Task<double> GetSupplierAccountAmountAsync(int supplierId, CancellationToken ct) =>
            Task.FromResult(SupplierAccounts.GetValueOrDefault(supplierId));

        public Task<SalesPurchaseListItemDTO> CreateOfficialBuyAsync(SalesPurchaseCreateCommand command, CancellationToken ct)
        {
            var invoice = command.SupplierInvoiceNumber;
            if (Buys.Any(b =>
                    b.SupplierId == command.Buy.SupplierID
                    && string.Equals(b.SupplierInvoiceNumber, invoice, StringComparison.OrdinalIgnoreCase)))
            {
                throw new SalesCompleteException(409, "فاتورة الشراء هذه مسجّلة مسبقاً لنفس المورد.");
            }

            var stockSnapshot = ItemStock.ToDictionary(kv => kv.Key, kv => kv.Value);
            var buysSnapshot = Buys.Count;
            try
            {
                var row = new SalesPurchaseListItemDTO
                {
                    BuyId = Buys.Count + 1,
                    SupplierId = command.Buy.SupplierID,
                    SupplierInvoiceNumber = invoice,
                    CreatedByUserName = command.CreatedByUserName,
                    CreatedByDisplayName = command.CreatedByDisplayName,
                    CreatedByUserType = command.CreatedByUserType,
                    CreatedByBranchId = command.BranchId,
                    CreatedByBranchName = command.BranchName,
                    CreatedAtUtc = DateTime.UtcNow,
                    DateCreate = command.Buy.Date,
                    Notes = command.Notes
                };
                Buys.Add(row);
                if (FailAfterBuyHeader)
                {
                    throw new SalesCompleteException(500, "فشل تحديث المخزون.");
                }

                foreach (var line in command.Buy.Contents.Where(c => c.ItemID is > 0))
                {
                    ItemStock[line.ItemID!.Value] = ItemStock.GetValueOrDefault(line.ItemID.Value) + (line.Quantity ?? 0);
                }

                OfficialBuyCalls++;
                return Task.FromResult(row);
            }
            catch
            {
                ItemStock.Clear();
                foreach (var kv in stockSnapshot)
                {
                    ItemStock[kv.Key] = kv.Value;
                }

                if (Buys.Count > buysSnapshot)
                {
                    Buys.RemoveRange(buysSnapshot, Buys.Count - buysSnapshot);
                }

                throw;
            }
        }

        public IReadOnlyList<SalesInventoryItemDTO> EmployeeInventory() =>
            ItemStock
                .Where(kv => kv.Value > 0)
                .Select(kv => new SalesInventoryItemDTO
                {
                    ProductId = kv.Key,
                    ProductName = ItemNames.GetValueOrDefault(kv.Key) ?? "",
                    AvailableQuantity = kv.Value
                })
                .ToList();
    }
    }
}
