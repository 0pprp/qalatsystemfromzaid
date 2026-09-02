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
            var total = sale.FinalSalePrice.ToString("0");
            var totalWords = IraqiDinarWords.ToArabic(sale.FinalSalePrice);
            var installment = sale.DailyInstallment.ToString("0");
            var installmentWords = IraqiDinarWords.ToArabic(sale.DailyInstallment);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Content().Border(1.5f).Padding(14).Column(col =>
                    {
                        col.Item().AlignCenter().Text("عقد بيع").Bold().FontSize(15);
                        col.Item().PaddingTop(8).Text(text =>
                        {
                            text.Span("حرصًا من الطرفين على إتمام عملية البيع والشراء وفقًا للأحكام والشروط المتفق عليها، فقد تم تحرير هذا العقد بتاريخ ").FontSize(10.5f);
                            text.Span(date).Bold().FontSize(10.5f);
                        });
                        col.Item().PaddingTop(10).Text("الطرف الأول: شركة قلعة الضمان للتجارة العامة محدودة المسؤولية.").Bold().FontSize(10.5f);
                        LabelRow(col, "الطرف الثاني:", sale.FullName);
                        LabelRow(col, "البطاقة الوطنية:", sale.NationalCardNumber);
                        LabelRow(col, "محافظة:", sale.Province);
                        LabelRow(col, "أقرب نقطة دالة:", sale.NearestLandmark);
                        LabelRow(col, "رقم الهاتف:", sale.Phone);
                        LabelRow(col, "الانتساب:", "");
                        LabelRow(col, "اسم المختار:", sale.MukhtarName);
                        LabelRow(col, "رقم مركز التموين:", sale.RationCenterNumber);
                        col.Item().PaddingTop(8).Text($"1. باع الطرف الأول إلى الطرف الثاني بضاعة نوع {goods}").FontSize(10.5f);
                        col.Item().Text($"   بسعر كلي والبالغ قدره رقمًا {total}   كتابة {totalWords}").FontSize(10.5f);
                        col.Item().PaddingTop(5).Text($"2. يلتزم الطرف الثاني بتسديد المبلغ الكلي على شكل دفعات بقسط يومي وقدره رقمًا {installment}   كتابة {installmentWords}").FontSize(10.5f);
                        col.Item().Text("   ويبدأ القسط الأول من تاريخ توقيع هذا العقد ويستمر دون توقف إلى نهاية تسديد كامل المبلغ.").FontSize(10.5f);
                        col.Item().PaddingTop(5).Text("3. على الطرف الثاني إشعار الطرف الأول عند انتقال محل عمله أو سكنه ويبلغ الطرف الأول بعنوان محل عمله أو سكنه الجديد.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text($"4. يسلم الطرف الثاني إلى الطرف الأول أمانة مثبتة مقدارها {total} وذلك بحوالة أو وصل أمانة منظمة قانونيًا.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("5. الطرف الثاني مسؤول عن فحص البضاعة عند استلامها قبل توقيع العقد للتأكد من خلوها من أي عيب أو تلف.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("6. يتحمل الطرف المخالف عن الالتزام بهذا العقد كافة تكاليف الدعوى بما فيها الرسوم وأتعاب المحاماة.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("7. في حال إخلال الطرف الثاني بأي شرط من شروط هذا العقد، يلتزم بدفع تعويض للطرف الآخر مبلغ قدره نفس قيمة العقد.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("8. في حال تخلف الطرف الثاني عن التسديد 7 مرات متوالية أو متقطعة يترتب عليه استحقاق كامل المبلغ المتبقي فورًا.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("9. يتحمل محكمة النجف الأشرف في أي نزاع مدني أو جزائي بخصوص الدعاوى الناشئة عن هذا العقد.").FontSize(10.5f);
                        col.Item().PaddingTop(4).Text("10. يكون هذا العقد ملزم للطرفين وورثتهم.").FontSize(10.5f);
                        col.Item().PaddingTop(10).Text($"أني الموقع أدناه {sale.UserName} أتعهد بأني اشتريت البضاعة إلى الطرف الثاني وقد تم استلامها من قبله وفقًا لتعليمات الشركة، وقد تأكدت من جميع معلومات الزبون المذكورة في هذا العقد أعلاه ومنها المستمسكات الأصلية وعنوانه المثبت أعلاه ورقم موبايله، وعليه وقعت.").FontSize(10.5f);
                        col.Item().PaddingTop(8).Text("أني  أمين صندوق الفرع أشهد بأن مندوب المبيعات قد وقع أمامي.").FontSize(10.5f);
                        col.Item().PaddingTop(30).Row(row =>
                        {
                            Signature(row, "الطرف الأول");
                            Signature(row, "أمين الصندوق");
                            Signature(row, "الطرف الثاني");
                            Signature(row, "مندوب المبيعات");
                        });
                    });
                });
            }).GeneratePdf();
        }

        private byte[] BuildPromissoryNote(SalesDraftDTO sale)
        {
            var date = (sale.CompletedAt ?? DateTime.Now).ToString("yyyy/MM/dd");
            var amount = sale.FinalSalePrice.ToString("0");
            var words = IraqiDinarWords.ToArabic(sale.FinalSalePrice);
            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    ConfigurePage(page);
                    page.Content().Border(1.5f).Padding(14).Column(col =>
                    {
                        col.Item().AlignCenter().Text("وصل أمانة").Bold().FontSize(18);
                        col.Item().PaddingTop(20).Text($"المبلغ قيمة: {amount}").Bold().FontSize(12);
                        col.Item().PaddingTop(6).Text($"كتابة: {words}").Bold().FontSize(12);
                        col.Item().PaddingTop(8).Text($"التاريخ: {date}").FontSize(12);
                        col.Item().PaddingTop(22).Text($"إني الموقع أدناه وبحضوري الشركة قلعة الضمان للتجارة العامة محدودة المسؤولية بمبلغ {amount} إعلاه أتعهد بأن أعيده متى ما طلبت الشركة مني ولا يحق لي التأخير.").FontSize(12);
                        col.Item().PaddingTop(20);
                        LabelRow(col, "اسم المسلم:", sale.FullName);
                        LabelRow(col, "رقم البطاقة أو الجنسية:", sale.NationalCardNumber);
                        LabelRow(col, "اسم المختار:", sale.MukhtarName);
                        LabelRow(col, "العنوان:", sale.Address);
                        col.Item().PaddingTop(20).Text("بصمة المسلم:   ....................................................").FontSize(12);
                        col.Item().PaddingTop(10).Text("توقيع المسلم: ....................................................").FontSize(12);
                        col.Item().PaddingTop(35).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("الشاهد الأول").Bold().FontSize(12);
                                c.Item().PaddingTop(8).Text("الاسم: ").FontSize(12);
                                c.Item().PaddingTop(4).Text("التوقيع: ................................").FontSize(12);
                            });
                            row.ConstantItem(40);
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("الشاهد الثاني").Bold().FontSize(12);
                                c.Item().PaddingTop(8).Text("الاسم: ").FontSize(12);
                                c.Item().PaddingTop(4).Text("التوقيع: ................................").FontSize(12);
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

        private static void LabelRow(ColumnDescriptor col, string label, string? value)
        {
            col.Item().Row(row =>
            {
                row.AutoItem().Text(label).Bold().FontSize(10.5f);
                row.ConstantItem(6);
                row.RelativeItem().Text(value ?? string.Empty).FontSize(10.5f);
            });
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
