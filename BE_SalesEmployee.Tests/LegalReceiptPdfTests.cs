using System.Text;
using BE_SalesEmployee.Legal;
using Xunit;

namespace BE_SalesEmployee.Tests;

public class LegalReceiptPdfTests
{
    [Fact]
    public void Render_ProducesAPdf()
    {
        var pdf = LegalReceiptPdf.Render(new LegalStore.ReceiptData
        {
            Id = 1,
            ReceiptNumber = 4827193056,
            FullName = "اسم المدين الثلاثي",
            ArchiveCodes = "NJ25-1-2086",
            DebtAmount = 250_000,
            PenaltyAmount = 100_000,
            Actor = "محاسب القانونية",
            AtUtc = new DateTime(2026, 11, 25, 9, 30, 0, DateTimeKind.Utc),
        });

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));

        // Set LEGAL_RECEIPT_SAMPLE to a file path to keep the rendered sample for review.
        var sample = Environment.GetEnvironmentVariable("LEGAL_RECEIPT_SAMPLE");
        if (!string.IsNullOrWhiteSpace(sample))
            File.WriteAllBytes(sample, pdf);
    }
}
