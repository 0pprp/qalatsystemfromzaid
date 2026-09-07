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
        public Task<SalesDraftDTO> ReplaceContentsAsync(SalesDraftDTO draft, CancellationToken ct) => Task.FromResult(draft);
        public Task<IReadOnlyList<SalesDraftDTO>> GetByEmployeeAsync(int employeeId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SalesDraftDTO>>([]);
        public Task<SalesDraftDTO?> GetByIdAsync(int saleId, int employeeId, CancellationToken ct) =>
            Task.FromResult<SalesDraftDTO?>(null);

        public Task UpdateCheckoutAsync(SalesDraftDTO draft, CancellationToken ct) => Task.CompletedTask;
    }

    public sealed class RecordedCustomerPayment
    {
        public int CustomerPaymentId { get; set; }
        public int SaleId { get; set; }
        public int? CustomerId { get; set; }
        public int? UserId { get; set; }
        public decimal AmountDenar { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    public sealed class FakeCompleteRepository : ISalesCompleteRepository
    {
        public readonly Dictionary<int, SalesDraftDTO> Sales = new();
        public readonly Dictionary<int, int> Stock = new();
        public readonly List<SalesDocumentRecord> Documents = [];
        public readonly List<RecordedCustomerPayment> Payments = [];
        public readonly Dictionary<int, string> OfficialCustomers = new();
        public readonly Dictionary<int, string> RequestNames = new();
        public readonly Dictionary<int, int> MainStock = new();
        public int DeductionCount { get; private set; }
        public int MainPostingCount { get; private set; }
        public int CompleteCalls { get; private set; }
        public int PostingCalls { get; private set; }
        public bool FailPayment { get; set; }
        public bool FailMainPosting { get; set; }
        public decimal ReceiptsTotal => Payments.Sum(p => p.AmountDenar);

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

            if (sale.Status == SalesStatuses.Rejected)
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

            var stockSnapshot = sale.Items
                .GroupBy(i => i.ProductId)
                .ToDictionary(g => g.Key, g => Stock[g.Key]);
            var previousDeduction = DeductionCount;
            var previousPayments = Payments.Count;
            var previousPaymentId = sale.DownPaymentCustomerPaymentId;
            var previousStatus = sale.Status;
            var previousCompletedAt = sale.CompletedAt;
            var previousCompletedBy = sale.CompletedBy;
            var previousDocStatus = sale.DocumentsStatus;
            var previousFullName = sale.FullName;
            var previousPhone = sale.Phone;
            var previousAddress = sale.Address;

            try
            {
                ApplyDraftIdentity(sale);

                foreach (var item in sale.Items)
                {
                    Stock[item.ProductId] -= item.Quantity;
                }

                DeductionCount++;
                sale.Status = SalesStatuses.Completed;
                sale.CompletedAt = DateTime.Now;
                sale.CompletedBy = employeeId;
                sale.DocumentsStatus = SalesStatuses.DocumentsPending;
                sale.PostingStatus = SalesPostingStatuses.Pending;
                sale.PostedAtUtc = null;
                sale.LastPostingError = null;
                return Task.FromResult(new SalesCompleteTxResult
                {
                    Sale = sale,
                    AlreadyCompleted = false,
                    InventoryDeducted = true,
                    DeductionCount = 1
                });
            }
            catch
            {
                foreach (var kv in stockSnapshot)
                {
                    Stock[kv.Key] = kv.Value;
                }

                DeductionCount = previousDeduction;
                if (Payments.Count > previousPayments)
                {
                    Payments.RemoveRange(previousPayments, Payments.Count - previousPayments);
                }

                sale.DownPaymentCustomerPaymentId = previousPaymentId;
                sale.Status = previousStatus;
                sale.CompletedAt = previousCompletedAt;
                sale.CompletedBy = previousCompletedBy;
                sale.DocumentsStatus = previousDocStatus;
                sale.FullName = previousFullName;
                sale.Phone = previousPhone;
                sale.Address = previousAddress;
                throw;
            }
        }

        public Task<IReadOnlyList<int>> ListUnpostedCompletedSaleIdsAsync(CancellationToken ct)
        {
            var ids = Sales.Values
                .Where(s => SalesCompleteRules.AlreadyCompleted(s.Status)
                            && SalesPostingStatuses.IsUnposted(s.PostingStatus))
                .OrderBy(s => s.CompletedAt)
                .Select(s => s.SaleId)
                .ToList();
            return Task.FromResult<IReadOnlyList<int>>(ids);
        }

        public Task PostToMainSystemAsync(int saleId, CancellationToken ct)
        {
            PostingCalls++;
            if (!Sales.TryGetValue(saleId, out var sale))
            {
                return Task.CompletedTask;
            }

            if (SalesPostingStatuses.IsPosted(sale.PostingStatus))
            {
                return Task.CompletedTask;
            }

            if (!SalesCompleteRules.AlreadyCompleted(sale.Status))
            {
                return Task.CompletedTask;
            }

            sale.PostingAttempts++;
            if (FailMainPosting || FailPayment)
            {
                sale.PostingStatus = SalesPostingStatuses.Failed;
                sale.LastPostingError = "فشل ترحيل البيع.";
                throw new SalesCompleteException(500, "فشل تسجيل دفعة المقدمة.");
            }

            foreach (var item in sale.Items)
            {
                if (!MainStock.ContainsKey(item.ProductId) || MainStock[item.ProductId] < item.Quantity)
                {
                    sale.PostingStatus = SalesPostingStatuses.Failed;
                    sale.LastPostingError = "الكمية المطلوبة غير متوفرة حالياً.";
                    throw new SalesCompleteException(409, "الكمية المطلوبة غير متوفرة حالياً.");
                }
            }

            foreach (var item in sale.Items)
            {
                MainStock[item.ProductId] -= item.Quantity;
            }

            MainPostingCount++;
            ApplyDraftIdentity(sale);
            if (sale.CustomerId is > 0 && OfficialCustomers.ContainsKey(sale.CustomerId.Value))
            {
                OfficialCustomers[sale.CustomerId.Value] = sale.FullName;
            }

            RecordDownPayment(sale, sale.EmployeeId);
            sale.PostingStatus = SalesPostingStatuses.Posted;
            sale.PostedAtUtc = DateTime.UtcNow;
            sale.LastPostingError = null;
            return Task.CompletedTask;
        }

        private void ApplyDraftIdentity(SalesDraftDTO sale)
        {
            string? requestName = sale.SalesRequestId is > 0
                ? RequestNames.GetValueOrDefault(sale.SalesRequestId.Value)
                : null;
            sale.FullName = SalesCustomerIdentity.PreferName(sale.FullName, requestName);
        }

        private void RecordDownPayment(SalesDraftDTO sale, int employeeId)
        {
            if (!SalesDownPaymentReceipt.ShouldRecord(sale.DownPayment, sale.DownPaymentCustomerPaymentId))
            {
                return;
            }

            if (FailPayment)
            {
                throw new SalesCompleteException(500, "فشل تسجيل دفعة المقدمة.");
            }

            var payment = new RecordedCustomerPayment
            {
                CustomerPaymentId = Payments.Count + 1,
                SaleId = sale.SaleId,
                CustomerId = sale.CustomerId,
                UserId = employeeId,
                AmountDenar = sale.DownPayment,
                PaymentDate = DateTime.Now
            };
            Payments.Add(payment);
            sale.DownPaymentCustomerPaymentId = payment.CustomerPaymentId;
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
        public int PreviewCalls { get; private set; }

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

        public Task<IReadOnlyList<SalesDocumentDTO>> EnsurePreviewGeneratedAsync(SalesDraftDTO sale, CancellationToken ct)
        {
            PreviewCalls++;
            IReadOnlyList<SalesDocumentDTO> docs =
            [
                new() { DocumentId = 101, Type = "PreviewContract", FileName = $"Sale_{sale.SaleId}_Preview_Contract.pdf", DownloadUrl = $"/api/sales/{sale.SaleId}/documents/101/download" },
                new() { DocumentId = 102, Type = "PreviewPromissoryNote", FileName = $"Sale_{sale.SaleId}_Preview_PromissoryNote.pdf", DownloadUrl = $"/api/sales/{sale.SaleId}/documents/102/download" }
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
