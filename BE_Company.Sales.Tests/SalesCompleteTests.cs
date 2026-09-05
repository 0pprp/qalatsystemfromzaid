using System.Reflection;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Controllers;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class SalesCompleteTests
    {
        private static SalesIdentity Identity(int employeeId = 1) => new()
        {
            EmployeeId = employeeId,
            EmployeeName = "موظف",
            BranchId = "najaf-demo",
            BranchName = "النجف",
            Role = SalesRoles.SalesEmployee,
            UserType = SalesRoles.UserTypeSalesEmployee
        };

        private static SalesDraftDTO Draft(int eval, string status = "Pending", int employeeId = 1, int qty = 1) => new()
        {
            SaleId = 10,
            EmployeeId = employeeId,
            CityValue = "najaf-demo",
            Status = status,
            FullName = "أحمد علي",
            Phone = "0770",
            Province = "النجف",
            NationalCardNumber = "N1",
            Address = "حي الأنصار",
            NearestLandmark = "جامع",
            MukhtarName = "حسن",
            RationCenterNumber = "12",
            EvaluationLevel = eval,
            EvaluationNote = "ملاحظة",
            BaseSalePrice = 2000000,
            FinalSalePrice = SalesEvaluationLevels.BlocksSale(eval) ? 0 : 2000000,
            DailyInstallment = 25000,
            DownPayment = 100000,
            Items =
            [
                new SalesDraftItemDTO { ProductId = 5, ProductName = "ثلاجة سامسونج", Quantity = qty, UnitSalePrice = 2000000, LineSalePrice = 2000000 }
            ]
        };

        [Fact]
        public async Task Complete_Accepted_CannotComplete()
        {
            var repo = Seed(SalesEvaluationLevels.Accepted, SalesStatuses.Rejected);
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService());
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.CompleteAsync(10, Identity(), CancellationToken.None));
            Assert.Equal(409, ex.StatusCode);
            Assert.Equal(0, repo.DeductionCount);
        }

        [Fact]
        public void Pricing_IgnoresEvaluation_AndComputesCheckoutDefaults()
        {
            var pricing = new SalesPricingService();
            Assert.Equal(2000000, pricing.ComputeFinalSalePrice(2000000, SalesEvaluationLevels.Accepted));
            Assert.Equal(SalesStatuses.Pending, pricing.ResolveStatus(SalesEvaluationLevels.Accepted));
            Assert.Equal(2000000, pricing.ComputeFinalSalePrice(2000000, SalesEvaluationLevels.Good));
            var snapshot = pricing.ComputeCheckout(1000000, 25000, null, null, null);
            Assert.Equal(1000000, snapshot.DefaultTotalSalePrice);
            Assert.Equal(25000, snapshot.DefaultDailyInstallment);
            Assert.Equal(50000, snapshot.DefaultDownPayment);
            Assert.Equal(1000000, snapshot.FinalTotalSalePrice);
            var overridden = pricing.ComputeCheckout(1000000, 25000, 900000, 20000, 40000);
            Assert.Equal(900000, overridden.FinalTotalSalePrice);
            Assert.Equal(20000, overridden.FinalDailyInstallment);
            Assert.Equal(40000, overridden.FinalDownPayment);
        }

        [Fact]
        public async Task Evaluation_Accepted_DoesNotBlockPendingSale()
        {
            var repo = Seed(SalesEvaluationLevels.Accepted);
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService());
            var result = await svc.CompleteAsync(10, Identity(), CancellationToken.None);
            Assert.Equal(SalesStatuses.Completed, result.Status);
            Assert.Equal(1, repo.DeductionCount);
        }

        [Fact]
        public void Inventory_HidesFitoutAndExternalMobiles()
        {
            Assert.True(SalesInventoryService.IsHiddenFromSalesStaff("تجهيز محل"));
            Assert.True(SalesInventoryService.IsHiddenFromSalesStaff("موبايلات خارجية"));
            Assert.False(SalesInventoryService.IsHiddenFromSalesStaff("ثلاجة سامسونج"));
        }

        [Fact]
        public async Task Complete_PendingGood_Succeeds()
        {
            var repo = Seed(SalesEvaluationLevels.Good);
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService());
            var result = await svc.CompleteAsync(10, Identity(), CancellationToken.None);
            Assert.Equal(SalesStatuses.Completed, result.Status);
            Assert.Equal(2000000, result.FinalSalePrice);
            Assert.Equal(1, repo.DeductionCount);
        }

        [Fact]
        public async Task Complete_MissingShop_FailsWhenShopServicePresent()
        {
            var repo = Seed(SalesEvaluationLevels.Good);
            var shops = new FakeShopService();
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService(), shops: shops);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.CompleteAsync(10, Identity(), null, CancellationToken.None));
            Assert.Equal(400, ex.StatusCode);
            Assert.Equal(0, repo.DeductionCount);
            Assert.Equal(0, shops.UpsertCount);
        }

        [Fact]
        public async Task Complete_Rejected_DoesNotUpsertShop()
        {
            var repo = Seed(SalesEvaluationLevels.Accepted, SalesStatuses.Rejected);
            var shops = new FakeShopService();
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService(), shops: shops);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.CompleteAsync(10, Identity(), ValidShop(), CancellationToken.None));
            Assert.Equal(409, ex.StatusCode);
            Assert.Equal(0, shops.UpsertCount);
            Assert.Equal(0, repo.DeductionCount);
        }

        [Fact]
        public async Task Complete_PendingGood_WithShop_Succeeds()
        {
            var repo = Seed(SalesEvaluationLevels.Good);
            var shops = new FakeShopService();
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService(), shops: shops);
            var result = await svc.CompleteAsync(10, Identity(), ValidShop(), CancellationToken.None);
            Assert.Equal(SalesStatuses.Completed, result.Status);
            Assert.Equal(1, shops.UpsertCount);
            Assert.Equal(1, repo.DeductionCount);
        }

        [Fact]
        public async Task Rejected_CannotComplete()
        {
            var repo = Seed(SalesEvaluationLevels.Rejected, SalesStatuses.Rejected);
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService());
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.CompleteAsync(10, Identity(), CancellationToken.None));
            Assert.Equal(409, ex.StatusCode);
            Assert.Equal("لا يمكن إتمام عملية بيع مرفوضة.", ex.Message);
            Assert.Equal(0, repo.DeductionCount);
        }

        [Fact]
        public async Task PreviewDocuments_DoesNotCompleteOrDeduct()
        {
            var repo = Seed(SalesEvaluationLevels.Good);
            var docs = new FakeDocumentService();
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), docs);
            var preview = await svc.PreviewDocumentsAsync(10, Identity(), null, CancellationToken.None);
            Assert.Equal(2, preview.Documents.Count);
            Assert.Equal(1, docs.PreviewCalls);
            Assert.Equal(0, docs.GenerateCalls);
            Assert.Equal(0, repo.DeductionCount);
            Assert.Equal(SalesStatuses.Pending, repo.Sales[10].Status);
            Assert.Equal(100000, preview.DefaultDownPayment);
            Assert.Equal(100000, preview.DownPayment);
        }

        [Fact]
        public void CompleteRules_AllowMissingRationCenter()
        {
            var sale = Draft(SalesEvaluationLevels.Good);
            sale.RationCenterNumber = null;
            Assert.Null(SalesCompleteRules.ValidateForComplete(sale));
        }

        [Fact]
        public async Task QuantityUnavailable_FailsWithoutDeduction()
        {
            var repo = Seed(SalesEvaluationLevels.Good, qty: 5);
            repo.Stock[5] = 1;
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService());
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.CompleteAsync(10, Identity(), CancellationToken.None));
            Assert.Equal(409, ex.StatusCode);
            Assert.Equal(1, repo.Stock[5]);
            Assert.Equal(0, repo.DeductionCount);
        }

        [Fact]
        public async Task InventoryDeductedExactlyOnce()
        {
            var repo = Seed(SalesEvaluationLevels.Good);
            repo.Stock[5] = 3;
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService());
            await svc.CompleteAsync(10, Identity(), CancellationToken.None);
            Assert.Equal(2, repo.Stock[5]);
            Assert.Equal(1, repo.DeductionCount);
        }

        [Fact]
        public async Task SameCompleteTwice_NoSecondDeduction()
        {
            var repo = Seed(SalesEvaluationLevels.Good);
            repo.Stock[5] = 2;
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService());
            await svc.CompleteAsync(10, Identity(), CancellationToken.None);
            var second = await svc.CompleteAsync(10, Identity(), CancellationToken.None);
            Assert.Equal(SalesStatuses.Completed, second.Status);
            Assert.Equal(1, repo.Stock[5]);
            Assert.Equal(1, repo.DeductionCount);
            Assert.Equal(2, repo.CompleteCalls);
        }

        [Fact]
        public async Task PdfGeneratedOnce_AndRegenSafe()
        {
            var repo = Seed(SalesEvaluationLevels.Good);
            var root = Path.Combine(Path.GetTempPath(), "sales-pdf-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var docs = new SalesDocumentService(new TempWebHostEnvironment(root), repo);
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), docs);
            await svc.CompleteAsync(10, Identity(), CancellationToken.None);
            Assert.Equal(2, repo.Documents.Count);
            var firstTimes = repo.Documents.Select(d => d.CreatedAt).ToList();
            var again = await docs.EnsureGeneratedAsync(repo.Sales[10], CancellationToken.None);
            Assert.Equal(2, again.Count);
            Assert.Equal(firstTimes, repo.Documents.Select(d => d.CreatedAt).ToList());
            File.Delete(repo.Documents[0].StoragePath);
            var regenerated = await docs.EnsureGeneratedAsync(repo.Sales[10], CancellationToken.None);
            Assert.Equal(2, regenerated.Count);
            Assert.True(File.Exists(repo.Documents[0].StoragePath));
        }

        [Fact]
        public async Task EmployeeCannotCompleteAnotherEmployeeSale()
        {
            var repo = Seed(SalesEvaluationLevels.Good, employeeId: 9);
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService());
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.CompleteAsync(10, Identity(1), CancellationToken.None));
            Assert.Equal(403, ex.StatusCode);
            Assert.Equal(0, repo.DeductionCount);
        }

        [Fact]
        public async Task EmployeeCannotDownloadAnotherEmployeeDocuments()
        {
            var repo = Seed(SalesEvaluationLevels.Good, employeeId: 9);
            var docs = new FakeDocumentService();
            var svc = new SalesCompleteService(repo, new FakeDraftRepository(), docs);
            var ex = await Assert.ThrowsAsync<SalesCompleteException>(() => svc.DownloadAsync(10, 1, 1, CancellationToken.None));
            Assert.Equal(403, ex.StatusCode);
        }

        [Fact]
        public void NoToken_RequiresAuthorize_401()
        {
            var type = typeof(SalesController);
            Assert.NotNull(type.GetCustomAttribute<AuthorizeAttribute>());
            var complete = type.GetMethod(nameof(SalesController.Complete));
            Assert.NotNull(complete);
            var policy = complete!.GetCustomAttributes<AuthorizeAttribute>().Single();
            Assert.Equal(SalesPolicies.SalesEmployee, policy.Policy);
            var download = type.GetMethod(nameof(SalesController.Download));
            Assert.Equal(SalesPolicies.SalesEmployee, download!.GetCustomAttributes<AuthorizeAttribute>().Single().Policy);
        }

        [Fact]
        public void IraqiDinarWords_Examples()
        {
            Assert.Equal("خمسة وعشرون ألف دينار عراقي", IraqiDinarWords.ToArabic(25000));
            Assert.Equal("أربعة ملايين دينار عراقي", IraqiDinarWords.ToArabic(4000000));
        }

        [Fact]
        public void GoodsDescription_MultipleItems()
        {
            var text = SalesCompleteRules.GoodsDescription(
            [
                new SalesDraftItemDTO { ProductName = "ثلاجة سامسونج", Quantity = 1 },
                new SalesDraftItemDTO { ProductName = "غسالة LG", Quantity = 1 }
            ]);
            Assert.Equal("ثلاجة سامسونج عدد 1، غسالة LG عدد 1", text);
        }

        private static FakeCompleteRepository Seed(int eval, string status = "Pending", int employeeId = 1, int qty = 1)
        {
            var repo = new FakeCompleteRepository();
            var draft = Draft(eval, status, employeeId, qty);
            repo.Sales[draft.SaleId] = draft;
            repo.Stock[5] = 3;
            return repo;
        }

        private static SalesShopCompleteDTO ValidShop() => new()
        {
            ShopName = "محل أحمد",
            ShopBusinessType = "مواد غذائية",
            ShopStockEstimatedValue = 1500000,
            EstimatedDailyRevenue = 80000,
            ShopLength = 8,
            ShopWidth = 5,
            ShopImageKey = "sales/10/shop.jpg"
        };
    }

    file sealed class FakeShopService : ISalesShopProfileService
    {
        public int UpsertCount { get; private set; }

        public Task EnsureSchemaAsync(CancellationToken ct) => Task.CompletedTask;

        public void RequireCompletePayload(SalesShopCompleteDTO? shop)
        {
            if (shop == null
                || string.IsNullOrWhiteSpace(shop.ShopName)
                || string.IsNullOrWhiteSpace(shop.ShopBusinessType)
                || string.IsNullOrWhiteSpace(shop.ShopImageKey))
            {
                throw new SalesCompleteException(400, "بيانات المحل مطلوبة قبل إتمام البيع.");
            }

            if (shop.ShopStockEstimatedValue <= 0 || shop.EstimatedDailyRevenue <= 0 || shop.ShopLength <= 0 || shop.ShopWidth <= 0)
            {
                throw new SalesCompleteException(400, "بيانات المحل مطلوبة قبل إتمام البيع.");
            }
        }

        public Task<SalesShopProfileDTO> SaveImageAsync(int saleId, int employeeId, IFormFile file, CancellationToken ct) =>
            Task.FromResult(new SalesShopProfileDTO { SaleId = saleId, ShopImageKey = $"sales/{saleId}/shop.jpg" });

        public Task UpsertFromCompleteAsync(SalesDraftDTO sale, SalesShopCompleteDTO shop, CancellationToken ct)
        {
            UpsertCount++;
            return Task.CompletedTask;
        }

        public Task<SalesShopProfileDTO?> GetBySaleIdAsync(int saleId, CancellationToken ct) =>
            Task.FromResult<SalesShopProfileDTO?>(null);

        public Task<(string FileName, byte[] Bytes)?> ReadImageAsync(int saleId, CancellationToken ct) =>
            Task.FromResult<(string FileName, byte[] Bytes)?>(null);

        public Task<SalesCustomerProfileDTO> GetCustomerProfileAsync(int? customerId, string? customerName, string? phone, CancellationToken ct) =>
            Task.FromResult(new SalesCustomerProfileDTO());

        public Task<SalesCustomerNoteDTO> AddNoteAsync(SalesCustomerNoteCreateDTO note, string authorRole, string? authorName, CancellationToken ct) =>
            Task.FromResult(new SalesCustomerNoteDTO { Note = note.Note ?? "", AuthorRole = authorRole });
    }
}
