using System.Reflection;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Controllers;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UglyToad.PdfPig;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class OfficialSalesPdfDownloadTests
    {
        [Fact]
        public void EmployeeAndManagerRoutes_ShareCompleteDownload()
        {
            var employee = typeof(SalesController).GetMethod(nameof(SalesController.Download));
            var manager = typeof(SalesManagerController).GetMethod(nameof(SalesManagerController.SaleDocumentDownload));
            Assert.NotNull(employee);
            Assert.NotNull(manager);
            Assert.Equal(
                SalesPolicies.SalesEmployee,
                employee!.GetCustomAttributes<AuthorizeAttribute>().Single().Policy);
            Assert.Equal(
                SalesPolicies.SalesManager,
                typeof(SalesManagerController).GetCustomAttribute<AuthorizeAttribute>()!.Policy);

            var employeeRoute = typeof(SalesController).GetCustomAttribute<RouteAttribute>()!.Template;
            var managerRoute = typeof(SalesManagerController).GetCustomAttribute<RouteAttribute>()!.Template;
            Assert.Equal("api/sales", employeeRoute);
            Assert.Equal("api/sales-manager", managerRoute);
        }

        [Fact]
        public async Task EmployeeDownload_ContractAndReceipt_AreSingleA4Page()
        {
            await using var ctx = await PdfDownloadContext.CreateAsync();
            AssertSingleA4(await ctx.EmployeeDownloadAsync(SalesDocumentService.Contract));
            AssertSingleA4(await ctx.EmployeeDownloadAsync(SalesDocumentService.PromissoryNote));
        }

        [Fact]
        public async Task ManagerDownload_ContractAndReceipt_AreSingleA4Page()
        {
            await using var ctx = await PdfDownloadContext.CreateAsync();
            AssertSingleA4(await ctx.ManagerDownloadAsync(SalesDocumentService.Contract));
            AssertSingleA4(await ctx.ManagerDownloadAsync(SalesDocumentService.PromissoryNote));
        }

        [Fact]
        public async Task EmployeeAndManagerDownload_ReturnSameRendererBytes()
        {
            await using var ctx = await PdfDownloadContext.CreateAsync();
            var employeeContract = await ctx.EmployeeDownloadAsync(SalesDocumentService.Contract);
            var managerContract = await ctx.ManagerDownloadAsync(SalesDocumentService.Contract);
            var employeeReceipt = await ctx.EmployeeDownloadAsync(SalesDocumentService.PromissoryNote);
            var managerReceipt = await ctx.ManagerDownloadAsync(SalesDocumentService.PromissoryNote);
            Assert.Equal(employeeContract, managerContract);
            Assert.Equal(employeeReceipt, managerReceipt);
        }

        [Fact]
        public async Task Download_RegeneratesStaleMultiPageFiles_ForBothPaths()
        {
            await using var ctx = await PdfDownloadContext.CreateAsync();
            ctx.OverwriteWithStalePages(SalesDocumentService.Contract, 2);
            ctx.OverwriteWithStalePages(SalesDocumentService.PromissoryNote, 4);
            Assert.Equal(2, PageCount(File.ReadAllBytes(ctx.PathOf(SalesDocumentService.Contract))));
            Assert.Equal(4, PageCount(File.ReadAllBytes(ctx.PathOf(SalesDocumentService.PromissoryNote))));

            AssertSingleA4(await ctx.EmployeeDownloadAsync(SalesDocumentService.Contract));
            AssertSingleA4(await ctx.ManagerDownloadAsync(SalesDocumentService.PromissoryNote));
            Assert.Equal(1, PageCount(File.ReadAllBytes(ctx.PathOf(SalesDocumentService.Contract))));
            Assert.Equal(1, PageCount(File.ReadAllBytes(ctx.PathOf(SalesDocumentService.PromissoryNote))));
        }

        [Fact]
        public async Task PreviewDownload_ContractAndReceipt_AreSingleA4Page()
        {
            await using var ctx = await PdfDownloadContext.CreateAsync();
            var preview = await ctx.Documents.EnsurePreviewGeneratedAsync(ctx.Sale, CancellationToken.None);
            Assert.Equal(3, preview.Count);
            Assert.Single(SalesDocumentService.PreferDisplayDocuments(preview));
            Assert.Equal(SalesDocumentService.PreviewSaleDocuments, SalesDocumentService.PreferDisplayDocuments(preview)[0].Type);
            AssertSingleA4(await ctx.EmployeeDownloadAsync(SalesDocumentService.PreviewContract));
            AssertSingleA4(await ctx.ManagerDownloadAsync(SalesDocumentService.PreviewPromissoryNote));
        }

        [Fact]
        public async Task CombinedSaleDocuments_Download_IsTwoA4Pages()
        {
            await using var ctx = await PdfDownloadContext.CreateAsync();
            using var stream = new MemoryStream(await ctx.EmployeeDownloadAsync(SalesDocumentService.SaleDocuments));
            using var document = PdfDocument.Open(stream);
            Assert.Equal(2, document.NumberOfPages);
            Assert.InRange(document.GetPage(1).Width, 590, 600);
            Assert.InRange(document.GetPage(1).Height, 835, 850);
            Assert.InRange(document.GetPage(2).Width, 590, 600);
            Assert.InRange(document.GetPage(2).Height, 835, 850);
            Assert.Equal(
                await ctx.EmployeeDownloadAsync(SalesDocumentService.SaleDocuments),
                await ctx.ManagerDownloadAsync(SalesDocumentService.SaleDocuments));
        }

        private static void AssertSingleA4(byte[] pdf)
        {
            using var stream = new MemoryStream(pdf);
            using var document = PdfDocument.Open(stream);
            Assert.Equal(1, document.NumberOfPages);
            var page = document.GetPage(1);
            Assert.InRange(page.Width, 590, 600);
            Assert.InRange(page.Height, 835, 850);
        }

        private static int PageCount(byte[] pdf)
        {
            using var stream = new MemoryStream(pdf);
            using var document = PdfDocument.Open(stream);
            return document.NumberOfPages;
        }

        private sealed class PdfDownloadContext : IAsyncDisposable
        {
            private PdfDownloadContext(
                string root,
                FakeCompleteRepository repo,
                SalesDocumentService documents,
                SalesCompleteService complete)
            {
                Root = root;
                Repo = repo;
                Documents = documents;
                Complete = complete;
            }

            public string Root { get; }
            public FakeCompleteRepository Repo { get; }
            public SalesDocumentService Documents { get; }
            public SalesCompleteService Complete { get; }
            public SalesDraftDTO Sale => Repo.Sales[10];

            public static async Task<PdfDownloadContext> CreateAsync()
            {
                var root = Path.Combine(Path.GetTempPath(), "sales-pdf-dl-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                var repo = new FakeCompleteRepository();
                repo.Sales[10] = new SalesDraftDTO
                {
                    SaleId = 10,
                    EmployeeId = 1,
                    UserName = "موظف تجريبي",
                    CityName = "النجف",
                    CityValue = "najaf-demo",
                    Status = SalesStatuses.Completed,
                    FullName = "أحمد علي محمد",
                    Phone = "07701234567",
                    Province = "النجف",
                    NationalCardNumber = "N1234567",
                    Address = "حي الأنصار",
                    NearestLandmark = "قرب جامع الأنصار",
                    MukhtarName = "حسن كاظم",
                    RationCenterNumber = "4412",
                    FinalSalePrice = 1500000,
                    DailyInstallment = 25000,
                    DownPayment = 75000,
                    CompletedAt = new DateTime(2026, 9, 5),
                    Items =
                    [
                        new SalesDraftItemDTO { ProductId = 5, ProductName = "ثلاجة سامسونج 18 قدم", Quantity = 1 }
                    ]
                };
                var documents = new SalesDocumentService(new TempWebHostEnvironment(root), repo);
                var complete = new SalesCompleteService(repo, new FakeDraftRepository(), documents);
                await documents.EnsureGeneratedAsync(repo.Sales[10], CancellationToken.None);
                return new PdfDownloadContext(root, repo, documents, complete);
            }

            public Task<byte[]> EmployeeDownloadAsync(string type) =>
                DownloadAsync(type, employeeId: 1);

            public Task<byte[]> ManagerDownloadAsync(string type) =>
                DownloadAsync(type, Sale.EmployeeId);

            public string PathOf(string type) =>
                Repo.Documents.Single(d => string.Equals(d.DocumentType, type, StringComparison.OrdinalIgnoreCase)).StoragePath;

            public void OverwriteWithStalePages(string type, int pages)
            {
                QuestPDF.Settings.License = LicenseType.Community;
                var stale = Document.Create(container =>
                {
                    for (var i = 0; i < pages; i++)
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4);
                            page.Content().Text($"stale-{i + 1}");
                        });
                    }
                }).GeneratePdf();
                File.WriteAllBytes(PathOf(type), stale);
            }

            private async Task<byte[]> DownloadAsync(string type, int employeeId)
            {
                var record = Repo.Documents.Single(d =>
                    string.Equals(d.DocumentType, type, StringComparison.OrdinalIgnoreCase));
                var file = await Complete.DownloadAsync(10, record.DocumentId, employeeId, CancellationToken.None);
                return file.Bytes;
            }

            public ValueTask DisposeAsync()
            {
                try
                {
                    if (Directory.Exists(Root))
                    {
                        Directory.Delete(Root, true);
                    }
                }
                catch
                {
                    // temp cleanup is best-effort
                }

                return ValueTask.CompletedTask;
            }
        }
    }
}
