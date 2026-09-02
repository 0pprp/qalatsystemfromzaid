using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BE_Company.Sales.Tests
{
    public sealed class FakeDraftRepository : ISalesDraftRepository
    {
        public Task EnsureSchemaAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<SalesDraftDTO> CreateAsync(SalesDraftDTO draft, CancellationToken ct) => Task.FromResult(draft);
        public Task<IReadOnlyList<SalesDraftDTO>> GetByEmployeeAsync(int employeeId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesDraftDTO>>([]);
        public Task<SalesDraftDTO?> GetByIdAsync(int saleId, int employeeId, CancellationToken ct) =>
            Task.FromResult<SalesDraftDTO?>(null);
    }

    public sealed class FakeCompleteRepository : ISalesCompleteRepository
    {
        public readonly Dictionary<int, SalesDraftDTO> Sales = new();
        public readonly Dictionary<int, int> Stock = new();
        public readonly List<SalesDocumentRecord> Documents = [];
        public int DeductionCount { get; private set; }
        public int CompleteCalls { get; private set; }

        public Task<SalesCompleteTxResult> CompleteInTransactionAsync(int saleId, int employeeId, string cityValue, CancellationToken ct)
        {
            CompleteCalls++;
            if (!Sales.TryGetValue(saleId, out var sale))
            {
                throw new SalesCompleteException(404, "العملية غير موجودة.");
            }

            if (sale.EmployeeId != employeeId || !string.Equals(sale.CityValue, cityValue, StringComparison.OrdinalIgnoreCase))
            {
                throw new SalesCompleteException(403, "لا يمكنك إتمام عملية تخص موظفاً أو فرعاً آخر.");
            }

            if (sale.Status == SalesStatuses.Rejected || sale.EvaluationLevel == SalesEvaluationLevels.Rejected)
            {
                throw new SalesCompleteException(409, "لا يمكن إتمام عملية بيع مرفوضة.");
            }

            if (SalesCompleteRules.AlreadyCompleted(sale.Status))
            {
                return Task.FromResult(new SalesCompleteTxResult
                {
                    Sale = sale,
                    AlreadyCompleted = true,
                    InventoryDeducted = false,
                    DeductionCount = 0
                });
            }

            var validation = SalesCompleteRules.ValidateForComplete(sale);
            if (validation != null)
            {
                throw new SalesCompleteException(400, validation);
            }

            foreach (var item in sale.Items)
            {
                if (!Stock.ContainsKey(item.ProductId))
                {
                    throw new SalesCompleteException(409, "أحد المنتجات لم يعد موجوداً في المخزن.");
                }

                if (Stock[item.ProductId] < item.Quantity)
                {
                    throw new SalesCompleteException(409, "الكمية المطلوبة غير متوفرة حالياً.");
                }
            }

            foreach (var item in sale.Items)
            {
                Stock[item.ProductId] -= item.Quantity;
            }

            DeductionCount++;
            sale.Status = SalesStatuses.Completed;
            sale.CompletedAt = DateTime.Now;
            sale.CompletedBy = employeeId;
            sale.DocumentsStatus = SalesStatuses.DocumentsPending;
            return Task.FromResult(new SalesCompleteTxResult
            {
                Sale = sale,
                AlreadyCompleted = false,
                InventoryDeducted = true,
                DeductionCount = 1
            });
        }

        public Task<IReadOnlyList<SalesDocumentRecord>> GetDocumentsAsync(int saleId, int employeeId, CancellationToken ct)
        {
            if (!Sales.TryGetValue(saleId, out var sale) || sale.EmployeeId != employeeId)
            {
                throw new SalesCompleteException(403, "لا يمكنك الوصول إلى مستندات عملية لا تخصك.");
            }

            return Task.FromResult<IReadOnlyList<SalesDocumentRecord>>(Documents.Where(d => d.SaleId == saleId).ToList());
        }

        public Task<SalesDocumentRecord?> GetDocumentAsync(int saleId, int documentId, int employeeId, CancellationToken ct)
        {
            if (Sales.TryGetValue(saleId, out var sale) && sale.EmployeeId != employeeId)
            {
                throw new SalesCompleteException(403, "لا يمكنك تنزيل مستندات عملية لا تخصك.");
            }

            return Task.FromResult(Documents.FirstOrDefault(d => d.SaleId == saleId && d.DocumentId == documentId));
        }

        public Task<SalesDocumentRecord> UpsertDocumentAsync(SalesDocumentRecord record, CancellationToken ct)
        {
            var existing = Documents.FirstOrDefault(d => d.SaleId == record.SaleId && d.DocumentType == record.DocumentType);
            if (existing != null)
            {
                existing.FileName = record.FileName;
                existing.StoragePath = record.StoragePath;
                existing.CreatedAt = record.CreatedAt;
                return Task.FromResult(existing);
            }

            record.DocumentId = Documents.Count + 1;
            Documents.Add(record);
            return Task.FromResult(record);
        }

        public Task SetDocumentsStatusAsync(int saleId, string documentsStatus, CancellationToken ct)
        {
            if (Sales.TryGetValue(saleId, out var sale))
            {
                sale.DocumentsStatus = documentsStatus;
            }

            return Task.CompletedTask;
        }

        public Task<SalesDraftDTO?> GetOwnedSaleAsync(int saleId, int employeeId, CancellationToken ct)
        {
            Sales.TryGetValue(saleId, out var sale);
            return Task.FromResult(sale != null && sale.EmployeeId == employeeId ? sale : null);
        }

        public Task<SalesDraftDTO?> GetSaleHeaderAsync(int saleId, CancellationToken ct)
        {
            Sales.TryGetValue(saleId, out var sale);
            return Task.FromResult(sale);
        }
    }

    public sealed class FakeDocumentService : ISalesDocumentService
    {
        public int GenerateCalls { get; private set; }

        public Task<IReadOnlyList<SalesDocumentDTO>> EnsureGeneratedAsync(SalesDraftDTO sale, CancellationToken ct)
        {
            GenerateCalls++;
            IReadOnlyList<SalesDocumentDTO> docs =
            [
                new() { DocumentId = 1, Type = "Contract", FileName = $"Sale_{sale.SaleId}_Contract.pdf", DownloadUrl = $"/api/sales/{sale.SaleId}/documents/1/download" },
                new() { DocumentId = 2, Type = "PromissoryNote", FileName = $"Sale_{sale.SaleId}_PromissoryNote.pdf", DownloadUrl = $"/api/sales/{sale.SaleId}/documents/2/download" }
            ];
            return Task.FromResult(docs);
        }

        public Task<(SalesDocumentRecord Record, byte[] Bytes)> ReadOwnedFileAsync(int saleId, int documentId, int employeeId, CancellationToken ct)
        {
            if (employeeId != 1)
            {
                throw new SalesCompleteException(403, "لا يمكنك تنزيل مستندات عملية لا تخصك.");
            }

            return Task.FromResult((new SalesDocumentRecord
            {
                DocumentId = documentId,
                SaleId = saleId,
                FileName = "Sale_1_Contract.pdf",
                DocumentType = "Contract"
            }, new byte[] { 1, 2, 3 }));
        }
    }

    public sealed class TempWebHostEnvironment : IWebHostEnvironment
    {
        public TempWebHostEnvironment(string root)
        {
            ContentRootPath = root;
            WebRootPath = root;
            EnvironmentName = "Development";
            ApplicationName = "tests";
            ContentRootFileProvider = new NullFileProvider();
            WebRootFileProvider = new NullFileProvider();
        }

        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; }
    }
}
