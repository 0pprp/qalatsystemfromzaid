using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services
{
    public interface ISalesCustomerDocumentService
    {
        Task EnsureSchemaAsync(CancellationToken ct);
        Task<List<SalesCustomerDocumentDTO>> ListForSaleAsync(int saleId, bool managerUrls, CancellationToken ct);
        Task<List<SalesCustomerDocumentDTO>> ListForCustomerAsync(int? customerId, string? customerName, string? phone, bool managerUrls, CancellationToken ct);
        Task<SalesCustomerDocumentDTO> SaveAsync(
            string documentType,
            IFormFile file,
            int? saleId,
            int? employeeId,
            int? customerId,
            string? customerName,
            string? customerPhone,
            bool replaceSameType,
            bool managerUrls,
            CancellationToken ct);
        Task<(string FileName, byte[] Bytes, string ContentType)?> ReadFileAsync(int id, CancellationToken ct);
        Task DeleteAsync(int id, int? employeeId, CancellationToken ct);
    }

    public sealed class SalesCustomerDocumentService : ISalesCustomerDocumentService
    {
        private readonly SalesDevelopmentGuard _guard;
        private readonly IWebHostEnvironment _env;
        private readonly ISalesCompleteRepository _complete;

        public SalesCustomerDocumentService(
            SalesDevelopmentGuard guard,
            IWebHostEnvironment env,
            ISalesCompleteRepository complete)
        {
            _guard = guard;
            _env = env;
            _complete = complete;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct)
        {
            var cs = _guard.GetSalesConnectionString()
                     ?? throw new InvalidOperationException("Sales module has no usable branch connection.");
            await using var connection = new SqlConnection(cs);
            await connection.ExecuteAsync(new CommandDefinition(SchemaSql, cancellationToken: ct));
        }

        public async Task<List<SalesCustomerDocumentDTO>> ListForSaleAsync(int saleId, bool managerUrls, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var rows = (await connection.QueryAsync<SalesCustomerDocumentDTO>(new CommandDefinition(
                SelectSql + " WHERE SaleId = @SaleId ORDER BY CreatedAtUtc, Id",
                new { SaleId = saleId }, cancellationToken: ct))).ToList();
            return rows.Select(r => Shape(r, managerUrls)).ToList();
        }

        public async Task<List<SalesCustomerDocumentDTO>> ListForCustomerAsync(
            int? customerId, string? customerName, string? phone, bool managerUrls, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var drafts = await connection.QueryAsync<(int SaleId, int? CustomerId, string? FullName, string? Phone)>(
                new CommandDefinition(
                    "SELECT SaleId, CustomerId, FullName, Phone FROM dbo.SalesDrafts",
                    cancellationToken: ct));
            var saleIds = drafts
                .Where(d =>
                    (customerId is > 0 && d.CustomerId == customerId)
                    || SalesCustomerDocumentMatcher.NamesMatch(d.FullName, customerName)
                    || SalesCustomerDocumentMatcher.PhonesMatch(d.Phone, phone))
                .Select(d => d.SaleId)
                .ToHashSet();

            var rows = await connection.QueryAsync<SalesCustomerDocumentDTO>(new CommandDefinition(
                SelectSql + " ORDER BY CreatedAtUtc, Id", cancellationToken: ct));
            return rows
                .Where(r => SalesCustomerDocumentMatcher.Matches(r, customerId, customerName, phone, saleIds))
                .Select(r => Shape(r, managerUrls))
                .ToList();
        }

        public async Task<SalesCustomerDocumentDTO> SaveAsync(
            string documentType,
            IFormFile file,
            int? saleId,
            int? employeeId,
            int? customerId,
            string? customerName,
            string? customerPhone,
            bool replaceSameType,
            bool managerUrls,
            CancellationToken ct)
        {
            if (file == null || file.Length <= 0)
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "الصورة مطلوبة.");
            }

            if (!SalesCustomerDocumentTypes.IsKnown(documentType))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "نوع المستند غير صالح.");
            }

            var type = SalesCustomerDocumentTypes.Normalize(documentType);
            SalesDraftDTO? sale = null;
            if (saleId is > 0 && employeeId is > 0)
            {
                sale = await _complete.GetOwnedSaleAsync(saleId.Value, employeeId.Value, ct)
                       ?? throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك رفع مستند لعملية لا تخصك.");
                if (SalesCompleteRules.AlreadyCompleted(sale.Status))
                {
                    throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تعديل مستندات زبون لعملية مكتملة.");
                }
            }

            if (sale != null)
            {
                customerId ??= sale.CustomerId;
                if (string.IsNullOrWhiteSpace(customerName))
                {
                    customerName = sale.FullName;
                }

                if (string.IsNullOrWhiteSpace(customerPhone))
                {
                    customerPhone = sale.Phone;
                }
            }

            if (saleId is null or <= 0 && customerId is null or <= 0
                && string.IsNullOrWhiteSpace(customerName) && string.IsNullOrWhiteSpace(customerPhone))
            {
                throw new SalesCompleteException(StatusCodes.Status400BadRequest, "حدد الزبون.");
            }

            await EnsureSchemaAsync(ct);
            var ext = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(ext) || ext.Length > 8)
            {
                ext = ".jpg";
            }

            ext = ext.ToLowerInvariant();
            if (ext is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
            {
                ext = ".jpg";
            }

            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            int? existingId = null;
            if (replaceSameType && saleId is > 0)
            {
                existingId = await connection.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
                    "SELECT TOP 1 Id FROM dbo.SalesCustomerDocuments WHERE SaleId = @SaleId AND DocumentType = @DocumentType ORDER BY Id DESC",
                    new { SaleId = saleId, DocumentType = type }, cancellationToken: ct));
            }

            var folder = Path.Combine(_env.ContentRootPath, "App_Data", "sales", "customer-docs");
            Directory.CreateDirectory(folder);
            int id;
            if (existingId is > 0)
            {
                id = existingId.Value;
                var previous = await connection.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
                    "SELECT FileKey FROM dbo.SalesCustomerDocuments WHERE Id = @Id",
                    new { Id = id }, cancellationToken: ct));
                TryDeleteFile(previous);
            }
            else
            {
                id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.SalesCustomerDocuments
(SaleId, CustomerId, CustomerName, CustomerPhone, DocumentType, FileKey, FileName, ContentType, CreatedAtUtc)
OUTPUT INSERTED.Id
VALUES
(@SaleId, @CustomerId, @CustomerName, @CustomerPhone, @DocumentType, N'', @FileName, @ContentType, SYSUTCDATETIME());",
                    new
                    {
                        SaleId = saleId,
                        CustomerId = customerId,
                        CustomerName = customerName,
                        CustomerPhone = customerPhone,
                        DocumentType = type,
                        FileName = string.IsNullOrWhiteSpace(file.FileName) ? "doc" + ext : Path.GetFileName(file.FileName),
                        ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? GuessContentType(ext) : file.ContentType
                    }, cancellationToken: ct));
            }

            var fileName = id + ext;
            var path = Path.Combine(folder, fileName);
            await using (var stream = File.Create(path))
            {
                await file.CopyToAsync(stream, ct);
            }

            var key = $"sales/customer-docs/{fileName}";
            await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.SalesCustomerDocuments SET
 FileKey = @FileKey,
 FileName = @FileName,
 ContentType = @ContentType,
 CustomerId = COALESCE(@CustomerId, CustomerId),
 CustomerName = COALESCE(@CustomerName, CustomerName),
 CustomerPhone = COALESCE(@CustomerPhone, CustomerPhone),
 UpdatedAtUtc = SYSUTCDATETIME()
WHERE Id = @Id",
                new
                {
                    Id = id,
                    FileKey = key,
                    FileName = fileName,
                    ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? GuessContentType(ext) : file.ContentType,
                    CustomerId = customerId,
                    CustomerName = customerName,
                    CustomerPhone = customerPhone
                }, cancellationToken: ct));

            var row = await connection.QueryFirstAsync<SalesCustomerDocumentDTO>(new CommandDefinition(
                SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: ct));
            return Shape(row, managerUrls);
        }

        public async Task<(string FileName, byte[] Bytes, string ContentType)?> ReadFileAsync(int id, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var row = await connection.QueryFirstOrDefaultAsync<SalesCustomerDocumentDTO>(new CommandDefinition(
                SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: ct));
            if (row == null || string.IsNullOrWhiteSpace(row.FileKey))
            {
                return null;
            }

            var path = ResolvePath(row.FileKey);
            if (!File.Exists(path))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(path, ct);
            var contentType = string.IsNullOrWhiteSpace(row.ContentType)
                ? GuessContentType(Path.GetExtension(path))
                : row.ContentType!;
            return (row.FileName, bytes, contentType);
        }

        public async Task DeleteAsync(int id, int? employeeId, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            var cs = RequireConnection();
            await using var connection = new SqlConnection(cs);
            var row = await connection.QueryFirstOrDefaultAsync<SalesCustomerDocumentDTO>(new CommandDefinition(
                SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: ct));
            if (row == null)
            {
                throw new SalesCompleteException(StatusCodes.Status404NotFound, "المستند غير موجود.");
            }

            if (employeeId is > 0)
            {
                if (row.SaleId is null or <= 0)
                {
                    throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكن حذف هذا المستند.");
                }

                var sale = await _complete.GetOwnedSaleAsync(row.SaleId.Value, employeeId.Value, ct)
                           ?? throw new SalesCompleteException(StatusCodes.Status403Forbidden, "لا يمكنك حذف مستند لعملية لا تخصك.");
                if (SalesCompleteRules.AlreadyCompleted(sale.Status))
                {
                    throw new SalesCompleteException(StatusCodes.Status409Conflict, "لا يمكن تعديل مستندات زبون لعملية مكتملة.");
                }
            }

            TryDeleteFile(row.FileKey);
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM dbo.SalesCustomerDocuments WHERE Id = @Id",
                new { Id = id }, cancellationToken: ct));
        }

        private static SalesCustomerDocumentDTO Shape(SalesCustomerDocumentDTO row, bool managerUrls)
        {
            row.TypeLabel = SalesCustomerDocumentTypes.Label(row.DocumentType);
            row.FileUrl = managerUrls
                ? $"/api/sales-manager/customer-documents/{row.Id}/file"
                : $"/api/sales/customer-documents/{row.Id}/file";
            return row;
        }

        private void TryDeleteFile(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            try
            {
                var path = ResolvePath(key);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private string ResolvePath(string key)
        {
            if (Path.IsPathRooted(key) || key.Contains(":\\", StringComparison.Ordinal))
            {
                return key;
            }

            var relative = key.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            if (relative.StartsWith("App_Data", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(_env.ContentRootPath, relative);
            }

            return Path.Combine(_env.ContentRootPath, "App_Data", relative);
        }

        private string RequireConnection() =>
            _guard.GetSalesConnectionString()
            ?? throw new InvalidOperationException("Sales module has no usable branch connection.");

        private static string GuessContentType(string ext) => ext.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        private const string SelectSql = @"
SELECT Id, SaleId, CustomerId, CustomerName, CustomerPhone, DocumentType, FileKey, FileName, ContentType, CreatedAtUtc, UpdatedAtUtc
FROM dbo.SalesCustomerDocuments";

        private const string SchemaSql = @"
IF OBJECT_ID(N'dbo.SalesCustomerDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesCustomerDocuments (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        SaleId INT NULL,
        CustomerId INT NULL,
        CustomerName NVARCHAR(255) NULL,
        CustomerPhone NVARCHAR(50) NULL,
        DocumentType NVARCHAR(50) NOT NULL,
        FileKey NVARCHAR(500) NOT NULL,
        FileName NVARCHAR(255) NOT NULL,
        ContentType NVARCHAR(100) NULL,
        CreatedAtUtc DATETIME NOT NULL CONSTRAINT DF_SalesCustomerDocuments_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME NULL
    );
    CREATE INDEX IX_SalesCustomerDocuments_SaleId ON dbo.SalesCustomerDocuments (SaleId);
    CREATE INDEX IX_SalesCustomerDocuments_CustomerId ON dbo.SalesCustomerDocuments (CustomerId);
END;";
    }

    public static class SalesCustomerDocumentMatcher
    {
        public static IFormFile? ResolveUpload(IFormFile? file, HttpRequest request)
        {
            if (file is { Length: > 0 })
            {
                return file;
            }

            try
            {
                return request.Form.Files.FirstOrDefault(f => f.Length > 0);
            }
            catch
            {
                return null;
            }
        }

        public static bool Matches(
            SalesCustomerDocumentDTO row,
            int? customerId,
            string? customerName,
            string? phone,
            IReadOnlyCollection<int>? saleIds)
        {
            if (customerId is > 0 && row.CustomerId == customerId)
            {
                return true;
            }

            if (saleIds != null && row.SaleId is int saleId && saleIds.Contains(saleId))
            {
                return true;
            }

            return NamesMatch(row.CustomerName, customerName) || PhonesMatch(row.CustomerPhone, phone);
        }

        public static bool NamesMatch(string? left, string? right)
        {
            var a = NormalizeName(left);
            var b = NormalizeName(right);
            return a.Length > 0 && a == b;
        }

        public static bool PhonesMatch(string? left, string? right)
        {
            var a = PhoneDigits(left);
            var b = PhoneDigits(right);
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            {
                return false;
            }

            if (a == b)
            {
                return true;
            }

            var tailA = a.Length > 10 ? a[^10..] : a;
            var tailB = b.Length > 10 ? b[^10..] : b;
            return tailA.Length >= 7 && tailA == tailB;
        }

        public static string? PhoneDigits(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var digits = new string(value.Where(char.IsDigit).ToArray());
            return digits.Length == 0 ? null : digits;
        }

        private static string NormalizeName(string? value) =>
            string.Join(" ", (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToLowerInvariant();
    }
}
