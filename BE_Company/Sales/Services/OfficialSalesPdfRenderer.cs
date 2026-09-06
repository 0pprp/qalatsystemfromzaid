using BE_Company.Sales.DTO;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// مصدر PDF الوحيد لعقد البيع ووصل الأمانة: A4 عمودي، RTL، فقرة واحدة لكل بلوك.
    /// لا يُقسَّم النص العربي إلى Span/Run متعددة حتى لا تختفي المسافات بين الكلمات.
    /// </summary>
    public static class OfficialSalesPdfRenderer
    {
        public const float Margin = 48f;
        public const float BodyFontSize = 11.5f;
        public const float TitleFontSize = 20f;
        public const float ReceiptTitleFontSize = 22f;
        public const float LineHeight = 1.22f;
        public const float ParagraphSpacing = 5.5f;

        private static bool _licenseSet;
        private static bool _fontRegistered;
        private static bool _boldRegistered;
        private static readonly object FontLock = new();

        public static byte[] BuildContract(SalesDraftDTO sale)
        {
            var values = OfficialSalesDocumentText.FromSale(sale);
            var paragraphs = OfficialSalesDocumentText.BuildContractParagraphs(values);
            EnsureLicense();
            var bold = BoldFamily();
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Content().StopPaging().ScaleToFit().Column(col =>
                    {
                        col.Item().AlignCenter().BorderBottom(1.2f).PaddingBottom(4)
                            .Text("عقد بيع").Bold().FontFamily(bold).FontSize(TitleFontSize);
                        col.Item().PaddingTop(10).Column(body =>
                        {
                            body.Spacing(ParagraphSpacing);
                            foreach (var paragraph in paragraphs)
                            {
                                body.Item().Element(c => BodyParagraph(c, paragraph.PlainText));
                            }
                        });
                        col.Item().PaddingTop(12).Row(row =>
                        {
                            Signature(row, "الطرف الأول");
                            Signature(row, "أمين الصندوق");
                        });
                        col.Item().PaddingTop(28).Row(row =>
                        {
                            Signature(row, "الطرف الثاني");
                            Signature(row, "مندوب المبيعات");
                        });
                    });
                });
            }).GeneratePdf();
        }

        public static byte[] BuildPromissoryNote(SalesDraftDTO sale)
        {
            var values = OfficialSalesDocumentText.FromSale(sale);
            var paragraphs = OfficialSalesDocumentText.BuildReceiptParagraphs(values);
            EnsureLicense();
            var bold = BoldFamily();
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Content().StopPaging().ScaleToFit().Column(col =>
                    {
                        col.Item().AlignCenter()
                            .Text("وصل أمانة").Bold().FontFamily(bold).FontSize(ReceiptTitleFontSize);
                        col.Item().PaddingTop(14).Column(body =>
                        {
                            body.Spacing(ParagraphSpacing);
                            foreach (var paragraph in paragraphs)
                            {
                                body.Item().Element(c => BodyParagraph(c, paragraph.PlainText));
                            }
                        });
                        col.Item().PaddingTop(12).Row(row =>
                        {
                            row.RelativeItem(2).AlignRight().Text("بصمة المدين:");
                            row.RelativeItem(3).AlignCenter().Text("توقيع المدين:");
                        });
                        col.Item().PaddingTop(18).Element(c => WitnessBlock(c, "الشاهد الأول", bold));
                        col.Item().PaddingTop(14).Element(c => WitnessBlock(c, "الشاهد الثاني", bold));
                    });
                });
            }).GeneratePdf();
        }

        private static void ConfigurePage(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(Margin);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(t => t
                .FontFamily(ResolveFontFamily())
                .FontSize(BodyFontSize)
                .LineHeight(LineHeight));
        }

        private static void BodyParagraph(IContainer container, string text)
        {
            container.AlignRight().Text(PdfSafe(text));
        }

        private static void Signature(RowDescriptor row, string label)
        {
            row.RelativeItem().AlignRight().Text(label).Bold().FontSize(BodyFontSize);
        }

        private static void WitnessBlock(IContainer container, string title, string boldFamily)
        {
            container.Column(block =>
            {
                block.Item().Width(120).BorderBottom(0.8f).PaddingBottom(3).AlignCenter()
                    .Text(title).Bold().FontFamily(boldFamily).FontSize(14);
                block.Item().Height(8);
                block.Item().Text("الأسم:");
                block.Item().Height(28);
                block.Item().Text("التوقيع:");
            });
        }

        public static string PdfSafe(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var buffer = new System.Text.StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (ch is '\u200E' or '\u200F' or '\uFEFF' or '\uFFFD'
                    or >= '\u202A' and <= '\u202E'
                    or >= '\u2066' and <= '\u2069')
                {
                    continue;
                }

                buffer.Append(ch);
            }

            return buffer.ToString();
        }

        private static string BoldFamily()
        {
            RegisterFontsOnce();
            return _boldRegistered ? "Cairo-Bold" : (_fontRegistered ? "Cairo" : "Arial");
        }

        private static string ResolveFontFamily()
        {
            RegisterFontsOnce();
            return _fontRegistered ? "Cairo" : "Arial";
        }

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
