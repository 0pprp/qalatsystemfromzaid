using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    public sealed class SalesDocumentService : ISalesDocumentService
    {
        public const string Contract = "Contract";
        public const string PromissoryNote = "PromissoryNote";
        public const string SaleDocuments = "SaleDocuments";
        public const string PreviewContract = "PreviewContract";
        public const string PreviewPromissoryNote = "PreviewPromissoryNote";
        public const string PreviewSaleDocuments = "PreviewSaleDocuments";

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
            results.Add(await EnsureOneAsync(sale, SaleDocuments, existing, ct));
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
                await WritePreviewAsync(sale, PreviewSaleDocuments, $"Sale_{sale.SaleId}_Preview_SaleDocuments.pdf", folder, ct),
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
            await File.WriteAllBytesAsync(path, Render(sale, type), ct);
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
            var sale = await _complete.GetOwnedSaleAsync(saleId, employeeId, ct)
                       ?? throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك تنزيل مستندات عملية لا تخصك.");

            var bytes = Render(sale, record.DocumentType);
            var path = string.IsNullOrWhiteSpace(record.StoragePath)
                ? DefaultPath(sale.SaleId, record.DocumentType)
                : record.StoragePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, bytes, ct);
            if (!string.Equals(record.StoragePath, path, StringComparison.OrdinalIgnoreCase))
            {
                record.StoragePath = path;
            }

            return (record, bytes);
        }

        private async Task<SalesDocumentRecord> EnsureOneAsync(
            SalesDraftDTO sale,
            string type,
            List<SalesDocumentRecord> existing,
            CancellationToken ct)
        {
            var current = existing.FirstOrDefault(d => string.Equals(d.DocumentType, type, StringComparison.OrdinalIgnoreCase));
            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "sales", sale.SaleId.ToString());
            Directory.CreateDirectory(folder);
            var fileName = type == SaleDocuments
                ? $"Sale_{sale.SaleId}_SaleDocuments.pdf"
                : type == Contract
                ? $"Sale_{sale.SaleId}_Contract.pdf"
                : $"Sale_{sale.SaleId}_PromissoryNote.pdf";
            var path = current != null && !string.IsNullOrWhiteSpace(current.StoragePath)
                ? current.StoragePath
                : Path.Combine(folder, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, Render(sale, type), ct);

            if (current != null)
            {
                current.StoragePath = path;
                current.FileName = string.IsNullOrWhiteSpace(current.FileName) ? fileName : current.FileName;
                return current;
            }

            return await _complete.UpsertDocumentAsync(new SalesDocumentRecord
            {
                SaleId = sale.SaleId,
                DocumentType = type,
                FileName = fileName,
                StoragePath = path,
                CreatedAt = DateTime.Now
            }, ct);
        }

        public static byte[] Render(SalesDraftDTO sale, string documentType)
        {
            if (IsCombined(documentType))
            {
                return OfficialSalesPdfRenderer.BuildSaleDocuments(sale);
            }

            if (IsContract(documentType))
            {
                return OfficialSalesPdfRenderer.BuildContract(sale);
            }

            if (IsPromissory(documentType))
            {
                return OfficialSalesPdfRenderer.BuildPromissoryNote(sale);
            }

            throw new SalesCompleteException(StatusCodes.Status400BadRequest, "نوع المستند غير مدعوم.");
        }

        public static bool IsCombined(string? type) =>
            string.Equals(type, SaleDocuments, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, PreviewSaleDocuments, StringComparison.OrdinalIgnoreCase);

        public static IReadOnlyList<SalesDocumentDTO> PreferDisplayDocuments(IEnumerable<SalesDocumentDTO> docs)
        {
            var list = docs.ToList();
            var combined = list.Where(d => IsCombined(d.Type)).ToList();
            if (combined.Count > 0)
            {
                return combined;
            }

            var finals = list.Where(d =>
                string.Equals(d.Type, Contract, StringComparison.OrdinalIgnoreCase)
                || string.Equals(d.Type, PromissoryNote, StringComparison.OrdinalIgnoreCase)).ToList();
            if (finals.Count > 0)
            {
                return finals;
            }

            return list.Where(d =>
                string.Equals(d.Type, PreviewContract, StringComparison.OrdinalIgnoreCase)
                || string.Equals(d.Type, PreviewPromissoryNote, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        public static bool IsContract(string? type) =>
            string.Equals(type, Contract, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, PreviewContract, StringComparison.OrdinalIgnoreCase);

        public static bool IsPromissory(string? type) =>
            string.Equals(type, PromissoryNote, StringComparison.OrdinalIgnoreCase)
            || string.Equals(type, PreviewPromissoryNote, StringComparison.OrdinalIgnoreCase);

        private string DefaultPath(int saleId, string documentType)
        {
            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "sales", saleId.ToString());
            var fileName = IsCombined(documentType)
                ? $"Sale_{saleId}_SaleDocuments.pdf"
                : IsContract(documentType)
                ? $"Sale_{saleId}_Contract.pdf"
                : $"Sale_{saleId}_PromissoryNote.pdf";
            return Path.Combine(folder, fileName);
        }
    }
}
