using BE_Company.Sales.Services;
using Xunit;

namespace BE_Company.Tests;

public sealed class SalesRequestNotesSanitizeTests
{
    [Fact]
    public void SanitizeEmployeeFacingNotes_HidesWrongSaleTitle()
    {
        var raw = "القائمة: الميثاق (#5)\nتقييم المتجر / مبيعات خاطئة\nالمصدر: مندوب الجباية\nالتقييم: 10";
        var clean = SalesRequestService.SanitizeEmployeeFacingNotes(raw);
        Assert.NotNull(clean);
        Assert.DoesNotContain("مبيعات خاطئة", clean);
        Assert.DoesNotContain("تقييم المتجر", clean);
        Assert.Contains("المصدر: مندوب الجباية", clean);
        Assert.Contains("القائمة: الميثاق", clean);
        Assert.Contains("التقييم: 10", clean);
    }
}
