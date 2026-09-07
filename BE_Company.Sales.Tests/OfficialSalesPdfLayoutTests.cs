using BE_Company.Sales.DTO;
using BE_Company.Sales.Services;
using UglyToad.PdfPig;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class OfficialSalesPdfLayoutTests
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
            DownPayment = 75000,
            CompletedAt = new DateTime(2026, 9, 5),
            Items =
            [
                new SalesDraftItemDTO { ProductId = 5, ProductName = "ثلاجة سامسونج 18 قدم", Quantity = 1 }
            ]
        };

        private static SalesDraftDTO LongDraft() => new()
        {
            SaleId = 11,
            UserName = "مندوب المبيعات عبد الله حسين كاظم الشمري",
            CityName = "النجف الأشرف",
            FullName = "أحمد علي محمد حسن الجابري الخفاجي النجفي",
            Phone = "07701234567",
            Province = "النجف الأشرف",
            NationalCardNumber = "N1234567890123",
            Address = "محلة الأنصار زقاق 14 دار 27 قرب سوق الحسينية الجديدة",
            NearestLandmark = "بجانب جامع الأنصار مقابل مدرسة الفرات الابتدائية للبنين",
            MukhtarName = "حسن كاظم عبد الأمير الموسوي",
            RationCenterNumber = "44127890",
            FinalSalePrice = 12500000,
            DailyInstallment = 85000,
            DownPayment = 625000,
            CompletedAt = new DateTime(2026, 9, 5),
            Items =
            [
                new SalesDraftItemDTO { ProductId = 1, ProductName = "ثلاجة سامسونج 18 قدم ستيل مع ضمان الشركة", Quantity = 1 },
                new SalesDraftItemDTO { ProductId = 2, ProductName = "غسالة ملابس أوتوماتيك 8 كيلو إل جي", Quantity = 1 },
                new SalesDraftItemDTO { ProductId = 3, ProductName = "مكيف هواء سبليت 1.5 طن بارد حار", Quantity = 2 }
            ]
        };

        [Fact]
        public void Contract_NormalData_IsSingleA4Page()
        {
            AssertSingleA4(OfficialSalesPdfRenderer.BuildContract(Draft()));
        }

        [Fact]
        public void PromissoryNote_NormalData_IsSingleA4Page()
        {
            AssertSingleA4(OfficialSalesPdfRenderer.BuildPromissoryNote(Draft()));
        }

        [Fact]
        public void Contract_LongData_IsSingleA4Page()
        {
            AssertSingleA4(OfficialSalesPdfRenderer.BuildContract(LongDraft()));
        }

        [Fact]
        public void PromissoryNote_LongData_IsSingleA4Page()
        {
            AssertSingleA4(OfficialSalesPdfRenderer.BuildPromissoryNote(LongDraft()));
        }

        [Fact]
        public void ContractFields_HaveRealSpacesAroundValues()
        {
            var values = OfficialSalesDocumentText.FromSale(Draft());
            var text = string.Join("\n", OfficialSalesDocumentText.BuildContractParagraphs(values).Select(p => p.PlainText));
            Assert.Contains("الطرف الثاني ( أحمد علي محمد ) والذي يحمل", text);
            Assert.Contains("المرقمة ( N1234567 ) والساكن", text);
            Assert.Contains("محافظة ( النجف )", text);
            Assert.Contains("رقم الهاتف ( 07701234567 ) واتساب", text);
            Assert.Contains("بسعر كلي والبالغ قدره رقما ( 1,500,000 ) كتابة", text);
            Assert.Contains("أني الموقع ادناه ( موظف تجريبي ) أعمل", text);
        }

        [Fact]
        public void ReceiptFields_HaveRealSpacesAroundValues()
        {
            var values = OfficialSalesDocumentText.FromSale(Draft());
            var text = string.Join("\n", OfficialSalesDocumentText.BuildReceiptParagraphs(values).Select(p => p.PlainText));
            Assert.Contains("المبلغ رقما: ( 1,500,000 )", text);
            Assert.Contains("أسم المستلم: ( أحمد علي محمد )", text);
            Assert.Contains("رقم البطاقة الوطنية: ( N1234567 )", text);
        }

        [Fact]
        public void CombinedSaleDocuments_AreTwoIndependentA4Pages()
        {
            Assert.Equal(80f, OfficialSalesPdfRenderer.DebtorFingerprintHeight);
            using var stream = new MemoryStream(OfficialSalesPdfRenderer.BuildSaleDocuments(Draft()));
            using var document = PdfDocument.Open(stream);
            Assert.Equal(2, document.NumberOfPages);
            var page1 = document.GetPage(1);
            var page2 = document.GetPage(2);
            Assert.InRange(page1.Width, 590, 600);
            Assert.InRange(page1.Height, 835, 850);
            Assert.InRange(page2.Width, 590, 600);
            Assert.InRange(page2.Height, 835, 850);
        }

        [Fact]
        public void CombinedSaleDocuments_LongData_StayOnTwoA4Pages()
        {
            using var stream = new MemoryStream(OfficialSalesPdfRenderer.BuildSaleDocuments(LongDraft()));
            using var document = PdfDocument.Open(stream);
            Assert.Equal(2, document.NumberOfPages);
        }

        private static void AssertSingleA4(byte[] pdf)
        {
            using var stream = new MemoryStream(pdf);
            using var document = PdfDocument.Open(stream);
            Assert.Equal(1, document.NumberOfPages);
            var page = document.GetPage(1);
            Assert.InRange(page.Width, 590, 600);
            Assert.InRange(page.Height, 835, 850);
        }
    }
}
