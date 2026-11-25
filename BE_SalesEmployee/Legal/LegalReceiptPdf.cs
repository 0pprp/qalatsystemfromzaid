using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BE_SalesEmployee.Legal;

/// <summary>
/// The receipt of one تسديد as an A5 PDF: who paid, the date, the receipt number and the amount.
/// It deliberately says nothing about what the debtor still owes.
/// </summary>
public static class LegalReceiptPdf
{
    private const string Regular = "LegalCairo";
    private const string Bold = "LegalCairoBold";
    private const string Teal = "#128488";
    private const string Ink = "#2F2B3D";
    private const string Muted = "#6D6B77";
    private const string Line = "#DBDADE";

    private static readonly object Gate = new();
    private static bool _ready;
    private static byte[]? _logo;

    public static byte[] Render(LegalStore.ReceiptData receipt)
    {
        EnsureReady();
        var at = receipt.AtUtc.AddHours(3); // Iraq time
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                // As wide as A5, as tall as its content.
                page.ContinuousSize(PageSizes.A5.Width);
                page.Margin(28);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(t => t.FontFamily(Regular).FontSize(11).FontColor(Ink));

                page.Header().Column(header =>
                {
                    header.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("شركة قلعة الضمان").FontFamily(Bold).FontSize(17).FontColor(Teal);
                            c.Item().Text("القانونية").FontSize(11).FontColor(Muted);
                        });
                        if (_logo is not null)
                            row.ConstantItem(58).Height(58).AlignLeft().Image(_logo).FitArea();
                    });
                    header.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor(Teal);
                });

                page.Content().PaddingTop(14).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("وصل تسديد").FontFamily(Bold).FontSize(20);
                        row.ConstantItem(170).Border(1).BorderColor(Line).Background("#F6F8F8").Padding(8).Column(c =>
                        {
                            c.Item().AlignCenter().Text("رقم الوصل").FontSize(9).FontColor(Muted);
                            c.Item().AlignCenter().Text(receipt.ReceiptNumber.ToString()).FontFamily(Bold).FontSize(17).LetterSpacing(0.05f);
                        });
                    });

                    col.Item().Border(1).BorderColor(Line).Column(box =>
                    {
                        Field(box, "اسم المسدِّد", receipt.FullName, strong: true);
                        Field(box, "التاريخ", at.ToString("yyyy/MM/dd"), last: string.IsNullOrWhiteSpace(receipt.Note));
                        if (!string.IsNullOrWhiteSpace(receipt.Note))
                            Field(box, "ملاحظة", receipt.Note!, last: true);
                    });

                    col.Item().Background(Teal).Padding(14).Row(row =>
                    {
                        row.RelativeItem().AlignMiddle().Text("المبلغ المسدَّد").FontFamily(Bold).FontSize(13).FontColor(Colors.White);
                        row.RelativeItem().AlignLeft().Text(text =>
                        {
                            text.Span(receipt.Total.ToString("N0")).FontFamily(Bold).FontSize(22).FontColor(Colors.White);
                            text.Span("  دينار عراقي").FontSize(11).FontColor(Colors.White);
                        });
                    });
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().PaddingTop(14).LineHorizontal(0.75f).LineColor(Line);
                    footer.Item().PaddingTop(6).AlignCenter()
                        .Text("وصل صادر إلكترونياً من نظام القانونية — يُعتمد برقمه.").FontSize(8.5f).FontColor(Muted);
                });
            });
        }).GeneratePdf();
    }

    private static void Field(ColumnDescriptor box, string label, string value, bool strong = false, bool last = false)
    {
        var item = box.Item();
        if (!last)
            item = item.BorderBottom(1).BorderColor(Line);
        item.PaddingVertical(8).PaddingHorizontal(12).Row(row =>
        {
            row.ConstantItem(96).Text(label).FontColor(Muted);
            var text = row.RelativeItem().Text(value);
            if (strong)
                text.FontFamily(Bold).FontSize(13);
        });
    }

    private static void EnsureReady()
    {
        if (_ready)
            return;
        lock (Gate)
        {
            if (_ready)
                return;
            QuestPDF.Settings.License = LicenseType.Community;
            using (var regular = Asset("Cairo-Regular.ttf"))
                QuestPDF.Drawing.FontManager.RegisterFontWithCustomName(Regular, regular);
            using (var bold = Asset("Cairo-Bold.ttf"))
                QuestPDF.Drawing.FontManager.RegisterFontWithCustomName(Bold, bold);
            using (var logo = Asset("receipt-logo.png"))
            using (var buffer = new MemoryStream())
            {
                logo.CopyTo(buffer);
                _logo = buffer.ToArray();
            }

            _ready = true;
        }
    }

    private static Stream Asset(string fileName)
    {
        var assembly = typeof(LegalReceiptPdf).Assembly;
        var name = assembly.GetManifestResourceNames().First(n => n.EndsWith("." + fileName, StringComparison.Ordinal));
        return assembly.GetManifestResourceStream(name)!;
    }
}
