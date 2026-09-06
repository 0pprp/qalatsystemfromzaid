using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public sealed class SalesDocumentService : ISalesDocumentService
    {
        public const string Contract = "Contract";
        public const string PromissoryNote = "PromissoryNote";
        public const string PreviewContract = "PreviewContract";
        public const string PreviewPromissoryNote = "PreviewPromissoryNote";

        private readonly IWebHostEnvironment _env;
        private readonly ISalesCompleteRepository _complete;

        public SalesDocumentService(IWebHostEnvironment env, ISalesCompleteRepository complete)
        {
            _env = env;
            _complete = complete;
        }

        public async Task<IReadOnlyList<SalesDocumentDTO>> EnsureGeneratedAsync(SalesDraftDTO sale, CancellationToken ct)
        {
            var existing = (await _complete.GetDocumentsAsync(sale.SaleId, sale.EmployeeId, ct)).ToList();
            var results = new List<SalesDocumentRecord>();
            results.Add(await EnsureOneAsync(sale, Contract, existing, ct));
            results.Add(await EnsureOneAsync(sale, PromissoryNote, existing, ct));
            return results.Select(SalesDocumentMapper.ToDto).ToList();
        }

        public async Task<IReadOnlyList<SalesDocumentDTO>> EnsurePreviewGeneratedAsync(SalesDraftDTO sale, CancellationToken ct)
        {
            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "sales", sale.SaleId.ToString(), "preview");
            Directory.CreateDirectory(folder);
            var results = new List<SalesDocumentRecord>
            {
                await WritePreviewAsync(sale, PreviewContract, $"Sale_{sale.SaleId}_Preview_Contract.pdf", folder, ct),
                await WritePreviewAsync(sale, PreviewPromissoryNote, $"Sale_{sale.SaleId}_Preview_PromissoryNote.pdf", folder, ct)
            };
            return results.Select(SalesDocumentMapper.ToDto).ToList();
        }

        private async Task<SalesDocumentRecord> WritePreviewAsync(
            SalesDraftDTO sale,
            string type,
            string fileName,
            string folder,
            CancellationToken ct)
        {
            var path = Path.Combine(folder, fileName);
            var bytes = type == PreviewContract
                ? OfficialSalesPdfRenderer.BuildContract(sale)
                : OfficialSalesPdfRenderer.BuildPromissoryNote(sale);
            await File.WriteAllBytesAsync(path, bytes, ct);
            return await _complete.UpsertDocumentAsync(new SalesDocumentRecord
            {
                SaleId = sale.SaleId,
                DocumentType = type,
                FileName = fileName,
                StoragePath = path,
                CreatedAt = DateTime.Now
            }, ct);
        }

        public async Task<(SalesDocumentRecord Record, byte[] Bytes)> ReadOwnedFileAsync(
            int saleId,
            int documentId,
            int employeeId,
            CancellationToken ct)
        {
            var record = await _complete.GetDocumentAsync(saleId, documentId, employeeId, ct)
                         ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "المستند غير موجود.");
            if (!File.Exists(record.StoragePath))
            {
                var sale = await _complete.GetOwnedSaleAsync(saleId, employeeId, ct)
                           ?? throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك تنزيل مستندات عملية لا تخصك.");
                await EnsureGeneratedAsync(sale, ct);
                record = await _complete.GetDocumentAsync(saleId, documentId, employeeId, ct)
                         ?? throw new SalesCompleteException(StatusCodes.Status404NotFound, "المستند غير موجود.");
            }

            var bytes = await File.ReadAllBytesAsync(record.StoragePath, ct);
            return (record, bytes);
        }

        private async Task<SalesDocumentRecord> EnsureOneAsync(
            SalesDraftDTO sale,
            string type,
            List<SalesDocumentRecord> existing,
            CancellationToken ct)
        {
            var current = existing.FirstOrDefault(d => string.Equals(d.DocumentType, type, StringComparison.OrdinalIgnoreCase));
            if (current != null && File.Exists(current.StoragePath))
            {
                return current;
            }

            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "sales", sale.SaleId.ToString());
            Directory.CreateDirectory(folder);
            var fileName = type == Contract
                ? $"Sale_{sale.SaleId}_Contract.pdf"
                : $"Sale_{sale.SaleId}_PromissoryNote.pdf";
            var path = Path.Combine(folder, fileName);
            var bytes = type == Contract
                ? OfficialSalesPdfRenderer.BuildContract(sale)
                : OfficialSalesPdfRenderer.BuildPromissoryNote(sale);
            await File.WriteAllBytesAsync(path, bytes, ct);

            var saved = await _complete.UpsertDocumentAsync(new SalesDocumentRecord
            {
                SaleId = sale.SaleId,
                DocumentType = type,
                FileName = fileName,
                StoragePath = path,
                CreatedAt = DateTime.Now
            }, ct);
            return saved;
        }
    }
}
