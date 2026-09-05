using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// نص العقد ووصل الأمانة كما في الأداة الرسمية:
    /// Sales contract tool / lib/pdf/contract_text.dart و deposit_receipt_text.dart
    /// </summary>
    public static class OfficialSalesDocumentText
    {
        public const char Ltr = '\u200E';

        public sealed record Part(string Text, bool IsField);

        public sealed record Paragraph(IReadOnlyList<Part> Parts)
        {
            public string PlainText => string.Concat(Parts.Select(p => p.Text));
        }

        public static string FormatDate(DateTime date) => date.ToString("yyyy/MM/dd");

        public static string FormatWithCommas(string input)
        {
            var digits = new string((input ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
            {
                return string.Empty;
            }

            var buffer = new System.Text.StringBuilder();
            for (var i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                {
                    buffer.Append(',');
                }

                buffer.Append(digits[i]);
            }

            return buffer.ToString();
        }

        public static string OfficialAddressField(string? address, string? landmark)
        {
            var addr = address?.Trim() ?? string.Empty;
            var point = landmark?.Trim() ?? string.Empty;
            if (!string.IsNullOrEmpty(addr) && !string.IsNullOrEmpty(point) && addr != point)
            {
                return $"{addr} {point}";
            }

            return !string.IsNullOrEmpty(point) ? point : addr;
        }

        public static OfficialContractValues FromSale(SalesDraftDTO sale)
        {
            var total = sale.FinalSalePrice;
            var installment = sale.DailyInstallment;
            var phone = sale.Phone ?? string.Empty;
            return new OfficialContractValues(
                ContractDate: sale.CompletedAt ?? DateTime.Now,
                CustomerName: sale.FullName ?? string.Empty,
                NationalId: sale.NationalCardNumber ?? string.Empty,
                Province: string.IsNullOrWhiteSpace(sale.Province) ? (sale.CityName ?? string.Empty) : sale.Province,
                Address: OfficialAddressField(sale.Address, sale.NearestLandmark),
                Phone: phone,
                Whatsapp: phone,
                GuarantorName: sale.MukhtarName ?? string.Empty,
                RationCenterNumber: sale.RationCenterNumber ?? string.Empty,
                GoodsType: SalesCompleteRules.GoodsDescription(sale.Items ?? []),
                TotalPriceNumeric: decimal.Truncate(total).ToString("0"),
                TotalPriceWords: IraqiDinarWords.ToArabic(total),
                DailyInstallmentNumeric: decimal.Truncate(installment).ToString("0"),
                DailyInstallmentWords: IraqiDinarWords.ToArabic(installment),
                SalesRepName: sale.UserName ?? string.Empty,
                CashierName: string.Empty
            );
        }

        public static IReadOnlyList<Paragraph> BuildContractParagraphs(OfficialContractValues contract)
        {
            var date = FormatDate(contract.ContractDate);
            var totalNum = FormatWithCommas(contract.TotalPriceNumeric);
            var dailyNum = FormatWithCommas(contract.DailyInstallmentNumeric);
            return
            [
                Intro(date),
                Static("الطرف الأول شركة قلعة الضمان للتجارة العامة محدودة المسؤولية."),
                PartyTwo(contract),
                Contact(contract),
                SaleClause(contract, totalNum),
                InstallmentClause(contract, dailyNum),
                Static("3. على الطرف الثاني اشعار الطرف الأول عند انتقال محل عمله او سكنه ويبلغ الطرف الأول بعنوان محل عمله او سكنه الجديد."),
                Static("4. يسلم الطرف الثاني الى الطرف الأول نسخة ملونة من مستمسكاته الاصلية ", "هويته وبطاقة سكنه وبطاقة تموينه"),
                Static("5. الطرف الثاني مسؤول عن فحص البضاعة عند استلامها قبل توقيع العقد للتأكد من خلوها من أي عيب أو تلف."),
                Static("6. يتحمل الطرف المتخلف عن الالتزام بهذا العقد كافة تكاليف الدعوى بما فيها الرسوم وأتعاب المحاماة."),
                Static("7. في حال إخلال الطرف الثاني بأحد شروط هذا العقد، يلزم دفع تعويض للطرف الآخر مبلغ قدره نفس قيمة العقد."),
                Static("8. في حال تخلف الطرف الثاني عن التسديد ل7 مرات متوالية او متقطعة يترتب عليه استحقاق كامل المبلغ المتبقي فورا."),
                Static("9. تختص محكمة النجف الأشرف بأي نزاع مدني أو جزائي بخصوص الدعاوى الناشئة عن هذا العقد."),
                Static("10. يكون هذا العقد ملزم للطرفين وورثتهم."),
                SalesRep(contract),
                Cashier(contract)
            ];
        }

        public static IReadOnlyList<Paragraph> BuildReceiptParagraphs(OfficialContractValues contract)
        {
            var totalNum = FormatWithCommas(contract.TotalPriceNumeric);
            var date = FormatDate(contract.ContractDate);
            return
            [
                Labeled("المبلغ رقما: ", totalNum),
                Labeled("كتابــــــــــة: ", contract.TotalPriceWords),
                Debt(date),
                Labeled("أسم المستلم: ", contract.CustomerName),
                Labeled("رقم البطاقة الوطنية: ", contract.NationalId),
                Labeled("أسم المختار: ", contract.GuarantorName),
                Labeled("العنوان: ", contract.Address)
            ];
        }

        private static Paragraph Intro(string date)
        {
            var b = new Builder();
            b.Text("حرصاً من الطرفين على إتمام عملية البيع والشراء وفقاً للأحكام والشروط المتفق عليها، فقد تم تحرير هذا العقد بتاريخ ");
            b.Field(date);
            b.Text(".");
            return b.Build();
        }

        private static Paragraph PartyTwo(OfficialContractValues contract)
        {
            var b = new Builder();
            b.Text("الطرف الثاني ");
            b.Field(contract.CustomerName);
            b.Text(" والذي يحمل البطاقة الوطنية المرقمة ");
            b.Field(contract.NationalId);
            b.Text(" والساكن في محافظة ");
            b.Field(contract.Province);
            return b.Build();
        }

        private static Paragraph Contact(OfficialContractValues contract)
        {
            var b = new Builder();
            b.Text("أقرب نقطة دالة ");
            b.Field(contract.Address);
            b.Text(" رقم الهاتف ");
            b.Field(contract.Phone);
            b.Text(" واتساب ");
            b.Field(contract.Whatsapp);
            b.Text(" اسم المختار ");
            b.Field(contract.GuarantorName);
            b.Text(" رقم مركز التموين ");
            b.Field(contract.RationCenterNumber);
            return b.Build();
        }

        private static Paragraph SaleClause(OfficialContractValues contract, string totalNum)
        {
            var b = new Builder();
            b.Text("1. باع الطرف الأول إلى الطرف الثاني بضاعة نوع ");
            b.Field(contract.GoodsType);
            b.Text(" بسعر كلي والبالغ قدره رقما ");
            b.Field(totalNum);
            b.Text(" كتابة ");
            b.Field(contract.TotalPriceWords);
            return b.Build();
        }

        private static Paragraph InstallmentClause(OfficialContractValues contract, string dailyNum)
        {
            var b = new Builder();
            b.Text("2. يلتزم الطرف الثاني بتسديد المبلغ الكلي على شكل دفعات بقسط يومي قدرة رقما ");
            b.Field(dailyNum);
            b.Text(" كتابة ");
            b.Field(contract.DailyInstallmentWords);
            b.Text(" يبدا القسط الأول من تاريخ توقيع هذا العقد ويستمر دون توقف الى نهاية تسديد كامل المبلغ.");
            return b.Build();
        }

        private static Paragraph SalesRep(OfficialContractValues contract)
        {
            var b = new Builder();
            b.Text("أني الموقع ادناه ");
            b.Field(contract.SalesRepName);
            b.Text(" أعمل مندوب مبيعات الشركة أتعهد بأني باشرت ببيع البضاعة إلى الطرف الثاني وقد تم استلامها من قبله وفقاً لتعليمات الشركة، وقد تأكدت من جميع معلومات الزبون المذكورة في هذا العقد أعلاه ومنها المستمسكات الأصلية وعنوانه المثبت أعلاه ورقم موبايله، وعليه وقعت.");
            return b.Build();
        }

        private static Paragraph Cashier(OfficialContractValues contract)
        {
            var b = new Builder();
            b.Text("أنى ");
            b.Field(contract.CashierName);
            b.Text(" أمين صندوق الفرع اشهد بان مندوب المبيعات قد وقع امامي.");
            return b.Build();
        }

        private static Paragraph Debt(string date)
        {
            var b = new Builder();
            b.Text("إني الموقع أدناه أقر واعترف باني مدين لشركة قلعة الضمان للتجارة العامة محدودة المسؤولية بالمبلغ أعلاه واتعهد بان أعيده متى ما طلبت الشركة مني ولأجله وقعت بتاريخ ");
            b.Field(date);
            return b.Build();
        }

        private static Paragraph Labeled(string label, string value)
        {
            var b = new Builder();
            b.Text(label);
            b.Field(value);
            return b.Build();
        }

        private static Paragraph Static(string text, string? parens = null)
        {
            if (parens == null)
            {
                return new Paragraph([new Part(text, false)]);
            }

            var b = new Builder();
            b.Text(text);
            b.StaticParens(parens);
            return b.Build();
        }

        private sealed class Builder
        {
            private readonly List<Part> _parts = [];

            public void Text(string value) => _parts.Add(new Part(value, false));

            public void StaticParens(string inner) =>
                _parts.Add(new Part($"{Ltr}({inner}){Ltr}", false));

            public void Field(string value)
            {
                var trimmed = (value ?? string.Empty).Trim();
                if (trimmed.Length == 0)
                {
                    _parts.Add(new Part($"{Ltr}( .............................. ){Ltr}", false));
                    return;
                }

                _parts.Add(new Part($"{Ltr}( {trimmed} ){Ltr}", true));
            }

            public Paragraph Build() => new(_parts);
        }
    }

    public readonly record struct OfficialContractValues(
        DateTime ContractDate,
        string CustomerName,
        string NationalId,
        string Province,
        string Address,
        string Phone,
        string Whatsapp,
        string GuarantorName,
        string RationCenterNumber,
        string GoodsType,
        string TotalPriceNumeric,
        string TotalPriceWords,
        string DailyInstallmentNumeric,
        string DailyInstallmentWords,
        string SalesRepName,
        string CashierName);
}
