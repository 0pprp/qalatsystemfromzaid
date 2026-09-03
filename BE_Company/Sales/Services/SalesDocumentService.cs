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
            var goods = SalesCompleteRules.GoodsDescription(sale.Items);
            var date = (sale.CompletedAt ?? DateTime.Now).ToString("yyyy/MM/dd");
            var total = sale.FinalSalePrice.ToString("#,##0");
            var totalWords = IraqiDinarWords.ToArabic(sale.FinalSalePrice);
            var installment = sale.DailyInstallment.ToString("#,##0");
            var installmentWords = IraqiDinarWords.ToArabic(sale.DailyInstallment);
            var ration = sale.RationCenterNumber?.Trim();
            var hasRation = !string.IsNullOrWhiteSpace(ration);
            var employee = sale.UserName?.Trim() ?? string.Empty;
            var bold = BoldFamily();

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Content().Border(1.5f).Padding(14).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            Signature(row, "الطرف الأول");
                            Signature(row, "الطرف الثاني");
                            Signature(row, "مندوب المبيعات");
                            Signature(row, "أمين الصندوق");
                        });
                        col.Item().PaddingTop(8).AlignCenter().Text("عقد بيع").Bold().FontFamily(bold).FontSize(16);
                        col.Item().PaddingTop(10).Text(text =>
                        {
                            text.Span("حرصاً من الطرفين على إتمام عملية البيع والشراء وفقاً للأحكام والشروط المتفق عليها، فقد تم تحرير هذا العقد بتاريخ ").FontSize(10.5f);
                            text.Span(date).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span(".").FontSize(10.5f);
                        });
                        col.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("الطرف الأول: ").FontSize(10.5f);
                            text.Span("شركة قلعة الضمان للتجارة العامة محدودة المسؤولية").Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span(".").FontSize(10.5f);
                        });
                        Mix(col, "الطرف الثاني: ", sale.FullName, bold);
                        col.Item().Text(text =>
                        {
                            text.Span("والذي يحمل البطاقة الوطنية المرقمة ").FontSize(10.5f);
                            text.Span(sale.NationalCardNumber ?? string.Empty).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span(" والساكن في محافظة ").FontSize(10.5f);
                            text.Span(sale.Province ?? string.Empty).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span(".").FontSize(10.5f);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span("أقرب نقطة دالة ").FontSize(10.5f);
                            text.Span(sale.NearestLandmark ?? string.Empty).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span("  رقم الهاتف ").FontSize(10.5f);
                            text.Span(sale.Phone ?? string.Empty).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span("  واتساب ").FontSize(10.5f);
                            text.Span(sale.Phone ?? string.Empty).Bold().FontFamily(bold).FontSize(10.5f);
                        });
                        col.Item().Text(text =>
                        {
                            text.Span("اسم المختار ").FontSize(10.5f);
                            text.Span(sale.MukhtarName ?? string.Empty).Bold().FontFamily(bold).FontSize(10.5f);
                            if (hasRation)
                            {
                                text.Span("  رقم مركز التموين ").FontSize(10.5f);
                                text.Span(ration).Bold().FontFamily(bold).FontSize(10.5f);
                            }
                        });
                        col.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("1. باع الطرف الأول إلى الطرف الثاني بضاعة نوع ").FontSize(10.5f);
                            text.Span(goods).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span(" بسعر كلي والبالغ قدره رقماً ").FontSize(10.5f);
                            text.Span(total).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span(" كتابة ").FontSize(10.5f);
                            text.Span(totalWords).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span(".").FontSize(10.5f);
                        });
                        col.Item().PaddingTop(4).Text(text =>
                        {
                            text.Span("2. يلتزم الطرف الثاني بتسديد المبلغ الكلي على شكل دفعات بقسط يومي قدره رقماً ").FontSize(10.5f);
                            text.Span(installment).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span(" كتابة ").FontSize(10.5f);
                            text.Span(installmentWords).Bold().FontFamily(bold).FontSize(10.5f);
                            text.Span(".").FontSize(10.5f);
                        });
                        col.Item().Text("يبدأ القسط الأول من تاريخ توقيع هذا العقد ويستمر دون توقف إلى نهاية تسديد كامل المبلغ.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("3. على الطرف الثاني إشعار الطرف الأول عند انتقال محل عمله أو سكنه ويبلغ الطرف الأول بعنوان محل عمله أو سكنه الجديد.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text(hasRation
                            ? "4. يسلم الطرف الثاني إلى الطرف الأول نسخة ملونة من مستمسكاته الأصلية (هويته وبطاقة سكنه وبطاقة تموينه)."
                            : "4. يسلم الطرف الثاني إلى الطرف الأول نسخة ملونة من مستمسكاته الأصلية (هويته وبطاقة سكنه).").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("5. الطرف الثاني مسؤول عن فحص البضاعة عند استلامها قبل توقيع العقد للتأكد من خلوها من أي عيب أو تلف.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("6. يتحمل الطرف المتخلف عن الالتزام بهذا العقد كافة تكاليف الدعوى بما فيها الرسوم وأتعاب المحاماة.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("7. في حال إخلال الطرف الثاني بأحد شروط هذا العقد، يلزم دفع تعويض للطرف الآخر مبلغ قدره نفس قيمة العقد.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("8. في حال تخلف الطرف الثاني عن التسديد لـ 7 مرات متوالية أو متقطعة يترتب عليه استحقاق كامل المبلغ المتبقي فوراً.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("9. تختص محكمة النجف الأشرف بأي نزاع مدني أو جزائي بخصوص الدعاوى الناشئة عن هذا العقد.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("10. يكون هذا العقد ملزم للطرفين وورثتهم.").FontSize(10.5f);
                        col.Item().PaddingTop(10).Text(text =>
                        {
                            text.Span("أني الموقع أدناه أعمل مندوب مبيعات الشركة ").FontSize(10.5f);
                            if (!string.IsNullOrEmpty(employee))
                            {
                                text.Span(employee).Bold().FontFamily(bold).FontSize(10.5f);
                            }
                            text.Span(".").FontSize(10.5f);
                        });
                        col.Item().Text("أتعهد بأني باشرت ببيع البضاعة إلى الطرف الثاني وقد تم استلامها من قبله وفقاً لتعليمات الشركة، وقد تأكدت من جميع معلومات الزبون المذكورة في هذا العقد أعلاه ومنها المستمسكات الأصلية وعنوانه المثبت أعلاه ورقم موبايله، وعليه وقعت.").FontSize(10.5f);
                        col.Item().PaddingTop(8).Text("أني أمين صندوق الفرع أشهد بأن مندوب المبيعات قد وقع أمامي.").FontSize(10.5f);
                    });
                });
            }).GeneratePdf();
        }

        private byte[] BuildPromissoryNote(SalesDraftDTO sale)
        {
            var date = (sale.CompletedAt ?? DateTime.Now).ToString("yyyy/MM/dd");
            var amount = sale.FinalSalePrice.ToString("#,##0");
            var words = IraqiDinarWords.ToArabic(sale.FinalSalePrice);
            var bold = BoldFamily();
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Content().Border(1.5f).Padding(18).Column(col =>
                    {
                        col.Item().AlignCenter().Text("وصل أمانة").Bold().FontFamily(bold).FontSize(18);
                        col.Item().PaddingTop(22).Text(text =>
                        {
                            text.Span("المبلغ رقماً: ").FontSize(12);
                            text.Span(amount).Bold().FontFamily(bold).FontSize(12);
                        });
                        col.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("كتابة: ").FontSize(12);
                            text.Span(words).Bold().FontFamily(bold).FontSize(12);
                        });
                        col.Item().PaddingTop(16).Text(text =>
                        {
                            text.Span("إني الموقع أدناه أقر وأعترف بأني مدين لشركة قلعة الضمان للتجارة العامة محدودة المسؤولية بالمبلغ أعلاه وأتعهد بأن أعيده متى ما طلبت الشركة مني ولأجله وقعت بتاريخ ").FontSize(12);
                            text.Span(date).Bold().FontFamily(bold).FontSize(12);
                            text.Span(".").FontSize(12);
                        });
                        Mix(col, "اسم المستلم: ", sale.FullName, bold, 12);
                        Mix(col, "رقم البطاقة الوطنية: ", sale.NationalCardNumber, bold, 12);
                        Mix(col, "اسم المختار: ", sale.MukhtarName, bold, 12);
                        Mix(col, "العنوان: ", sale.Address, bold, 12);
                        col.Item().PaddingTop(28).Row(row =>
                        {
                            row.RelativeItem().Text("بصمة المدين: ....................").FontSize(12);
                            row.RelativeItem().AlignLeft().Text("توقيع المدين: ....................").FontSize(12);
                        });
                        col.Item().PaddingTop(36).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("الأول الشاهد").Bold().FontFamily(bold).FontSize(12);
                                c.Item().PaddingTop(10).Text("الاسم: ....................").FontSize(12);
                                c.Item().PaddingTop(8).Text("التوقيع: ....................").FontSize(12);
                            });
                            row.ConstantItem(28);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("الثاني الشاهد").Bold().FontFamily(bold).FontSize(12);
                                c.Item().PaddingTop(10).Text("الاسم: ....................").FontSize(12);
                                c.Item().PaddingTop(8).Text("التوقيع: ....................").FontSize(12);
                            });
                        });
                    });
                });
            }).GeneratePdf();
        }

        private void ConfigurePage(PageDescriptor page)
        {
            page.Size(PageSizes.A4);
            page.Margin(22);
            page.ContentFromRightToLeft();
            page.DefaultTextStyle(t => t.FontFamily(ResolveFontFamily()).FontSize(10.5f).LineHeight(1.65f));
        }

        private static void Mix(ColumnDescriptor col, string label, string? value, string boldFamily, float size = 10.5f)
        {
            col.Item().PaddingTop(8).Text(text =>
            {
                text.Span(label).FontSize(size);
                text.Span(value ?? string.Empty).Bold().FontFamily(boldFamily).FontSize(size);
            });
        }

        private static string BoldFamily()
        {
            RegisterFontsOnce();
            return _boldRegistered ? "Cairo-Bold" : (_fontRegistered ? "Cairo" : "Arial");
        }

        private static void Signature(RowDescriptor row, string label)
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().AlignCenter().Text(label).Bold().FontSize(9);
                c.Item().PaddingTop(25).AlignCenter().Text("..................").FontSize(8);
            });
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
