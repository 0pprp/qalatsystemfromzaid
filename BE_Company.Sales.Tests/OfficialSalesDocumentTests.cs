using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class OfficialSalesDocumentTests
    {
        private static SalesDraftDTO Draft() => new()
        {
            SaleId = 10,
            UserName = "موظف تجريبي",
            CityName = "النجف",
            FullName = "أحمد علي محمد",
            Phone = "07701234567",
            Province = "النجف",
            NationalCardNumber = "N1234567",
            Address = "حي الأنصار",
            NearestLandmark = "قرب جامع الأنصار",
            MukhtarName = "حسن كاظم",
            RationCenterNumber = "4412",
            FinalSalePrice = 1500000,
            DailyInstallment = 25000,
            CompletedAt = new DateTime(2026, 9, 5),
            Items =
            [
                new SalesDraftItemDTO { ProductId = 5, ProductName = "ثلاجة سامسونج 18 قدم", Quantity = 1 }
            ]
        };

        [Fact]
        public void Contract_UsesOfficialLegalTextAndSaleValues()
        {
            var values = OfficialSalesDocumentText.FromSale(Draft());
            var text = string.Join("\n", OfficialSalesDocumentText.BuildContractParagraphs(values).Select(p => p.PlainText));
            Assert.Contains("الطرف الأول شركة قلعة الضمان للتجارة العامة محدودة المسؤولية.", text);
            Assert.Contains("أحمد علي محمد", text);
            Assert.Contains("N1234567", text);
            Assert.Contains("النجف", text);
            Assert.Contains("حي الأنصار قرب جامع الأنصار", text);
            Assert.Contains("07701234567", text);
            Assert.Contains("حسن كاظم", text);
            Assert.Contains("4412", text);
            Assert.Contains("ثلاجة سامسونج 18 قدم عدد 1", text);
            Assert.Contains("1,500,000", text);
            Assert.Contains("25,000", text);
            Assert.Contains("هويته وبطاقة سكنه وبطاقة تموينه", text);
            Assert.Contains("أني الموقع ادناه", text);
            Assert.Contains("موظف تجريبي", text);
            Assert.Contains("أمين صندوق الفرع اشهد بان مندوب المبيعات قد وقع امامي.", text);
            Assert.DoesNotContain("الطرف الأول:", text);
        }

        [Fact]
        public void Receipt_UsesOfficialLabelsAndSaleValues()
        {
            var values = OfficialSalesDocumentText.FromSale(Draft());
            var text = string.Join("\n", OfficialSalesDocumentText.BuildReceiptParagraphs(values).Select(p => p.PlainText));
            Assert.Contains("المبلغ رقما: ", text);
            Assert.Contains("كتابــــــــــة: ", text);
            Assert.Contains("أسم المستلم: ", text);
            Assert.Contains("أحمد علي محمد", text);
            Assert.Contains("N1234567", text);
            Assert.Contains("حسن كاظم", text);
            Assert.Contains("حي الأنصار قرب جامع الأنصار", text);
            Assert.Contains("إني الموقع أدناه أقر واعترف باني مدين", text);
        }

        [Fact]
        public void Clause4_AlwaysIncludesRationCardEvenWhenRationEmpty()
        {
            var sale = Draft();
            sale.RationCenterNumber = null;
            var values = OfficialSalesDocumentText.FromSale(sale);
            var text = string.Join("\n", OfficialSalesDocumentText.BuildContractParagraphs(values).Select(p => p.PlainText));
            Assert.Contains("هويته وبطاقة سكنه وبطاقة تموينه", text);
            Assert.Contains("( .............................. )", text);
        }
    }
}
