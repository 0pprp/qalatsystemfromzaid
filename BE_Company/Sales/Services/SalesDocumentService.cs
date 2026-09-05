using BE_Company.Sales.DTO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BE_Company.Sales.Services
{
    public sealed class SalesDocumentService : ISalesDocumentService
    {
        public const string Contract = "Contract";
        public const string PromissoryNote = "PromissoryNote";

        private readonly IWebHostEnvironment _env;
        private readonly ISalesCompleteRepository _complete;
        private static bool _licenseSet;

        public SalesDocumentService(IWebHostEnvironment env, ISalesCompleteRepository complete)
        {
            _env = env;
            _complete = complete;
            EnsureLicense();
        }

        public async Task<IReadOnlyList<SalesDocumentDTO>> EnsureGeneratedAsync(SalesDraftDTO sale, CancellationToken ct)
        {
            var existing = (await _complete.GetDocumentsAsync(sale.SaleId, sale.EmployeeId, ct)).ToList();
            var results = new List<SalesDocumentRecord>();
            results.Add(await EnsureOneAsync(sale, Contract, existing, ct));
            results.Add(await EnsureOneAsync(sale, PromissoryNote, existing, ct));
            return results.Select(SalesDocumentMapper.ToDto).ToList();
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
            var bytes = type == Contract ? BuildContract(sale) : BuildPromissoryNote(sale);
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

        private byte[] BuildContract(SalesDraftDTO sale)
        {
            var values = OfficialSalesDocumentText.FromSale(sale);
            var paragraphs = OfficialSalesDocumentText.BuildContractParagraphs(values);
            var bold = BoldFamily();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Content().Column(col =>
                    {
                        col.Item().AlignCenter().Element(c => ContractTitle(c, bold));
                        col.Item().PaddingTop(6);
                        foreach (var paragraph in paragraphs)
                        {
                            col.Item().PaddingBottom(3).Element(c => RichParagraph(c, paragraph, bold, 12));
                        }

                        col.Item().PaddingTop(8).Row(row =>
                        {
                            SignatureSpace(row, "الطرف الأول", 200, 88);
                            SignatureSpace(row, "أمين الصندوق", 260, 88);
                        });
                        col.Item().PaddingTop(10).Row(row =>
                        {
                            SignatureSpace(row, "الطرف الثاني", 200, 60);
                            SignatureSpace(row, "مندوب المبيعات", 260, 60);
                        });
                    });
                });
            }).GeneratePdf();
        }

        private byte[] BuildPromissoryNote(SalesDraftDTO sale)
        {
            var values = OfficialSalesDocumentText.FromSale(sale);
            var paragraphs = OfficialSalesDocumentText.BuildReceiptParagraphs(values);
            var bold = BoldFamily();
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Content().Column(col =>
                    {
                        col.Item().AlignCenter().Text("وصل أمانة").Bold().FontFamily(bold).FontSize(24);
                        col.Item().PaddingTop(16);
                        foreach (var paragraph in paragraphs)
                        {
                            col.Item().PaddingBottom(10).Element(c => RichParagraph(c, paragraph, bold, 12));
                        }

                        col.Item().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem(2).AlignRight().Text("بصمة المدين:").FontSize(12);
                            row.RelativeItem(3).AlignCenter().Text("توقيع المدين:").FontSize(12);
                        });
                        col.Item().PaddingTop(36);
                        WitnessBlock(col, "الشاهد الأول", bold);
                        col.Item().PaddingTop(12);
                        WitnessBlock(col, "الشاهد الثاني", bold);
                    });
                });
            }).GeneratePdf();
        }

        private void ConfigurePage(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.MarginLeft(40);
            page.MarginRight(40);
            page.MarginTop(44);
            page.MarginBottom(44);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(t => t.FontFamily(ResolveFontFamily()).FontSize(12).LineHeight(1.1f));
        }

        private static void ContractTitle(IContainer container, string boldFamily)
        {
            container.AlignCenter().BorderBottom(1.5f).PaddingBottom(3).Row(row =>
            {
                row.AutoItem().Height(30).AlignMiddle().Text("عقد").Bold().FontFamily(boldFamily).FontSize(24);
                row.ConstantItem(6);
                row.AutoItem().Height(30).AlignMiddle().Text("بيع").Bold().FontFamily(boldFamily).FontSize(24);
            });
        }

        private static void RichParagraph(
            IContainer container,
            OfficialSalesDocumentText.Paragraph paragraph,
            string boldFamily,
            float size)
        {
            container.AlignRight().Text(text =>
            {
                foreach (var part in paragraph.Parts)
                {
                    var span = text.Span(part.Text).FontSize(size);
                    if (part.IsField)
                    {
                        span.Bold().FontFamily(boldFamily);
                    }
                }
            });
        }

        private static void SignatureSpace(RowDescriptor row, string label, float width, float height)
        {
            row.ConstantItem(width).Height(height).AlignTop().AlignRight().Text(label).Bold().FontSize(13);
        }

        private static void WitnessBlock(ColumnDescriptor col, string title, string boldFamily)
        {
            col.Item().Width(92).BorderBottom(0.8f).PaddingBottom(2).AlignCenter()
                .Text(title).Bold().FontFamily(boldFamily).FontSize(15);
            col.Item().PaddingTop(8).Text("الأسم:").FontSize(12);
            col.Item().PaddingTop(34).Text("التوقيع:").FontSize(12);
            col.Item().Height(36);
        }

        private static string BoldFamily()
        {
            RegisterFontsOnce();
            return _boldRegistered ? "Cairo-Bold" : (_fontRegistered ? "Cairo" : "Arial");
        }

        private string ResolveFontFamily()
        {
            RegisterFontsOnce();
            return _fontRegistered ? "Cairo" : "Arial";
        }

        private static bool _fontRegistered;
        private static bool _boldRegistered;
        private static readonly object FontLock = new();

        private static void RegisterFontsOnce()
        {
            if (_fontRegistered)
            {
                return;
            }

            lock (FontLock)
            {
                if (_fontRegistered)
                {
                    return;
                }

                var regular = Path.Combine(AppContext.BaseDirectory, "Sales", "Fonts", "Cairo-Regular.ttf");
                var bold = Path.Combine(AppContext.BaseDirectory, "Sales", "Fonts", "Cairo-Bold.ttf");
                if (File.Exists(regular))
                {
                    QuestPDF.Drawing.FontManager.RegisterFontWithCustomName("Cairo", new MemoryStream(File.ReadAllBytes(regular)));
                    _fontRegistered = true;
                }

                if (File.Exists(bold))
                {
                    QuestPDF.Drawing.FontManager.RegisterFontWithCustomName("Cairo-Bold", new MemoryStream(File.ReadAllBytes(bold)));
                    _boldRegistered = true;
                }
            }
        }

        private static void EnsureLicense()
        {
            if (_licenseSet)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            _licenseSet = true;
        }
    }
}
