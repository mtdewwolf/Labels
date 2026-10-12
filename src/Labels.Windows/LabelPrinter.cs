using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using Labels.Core;

namespace Labels.Windows;

internal static class LabelPrinter
{
    public static void Print(IWin32Window owner, LabelDesign design, string printerName, int copies)
    {
        using var bitmap = LabelRenderer.Render(design);
        using var document = new PrintDocument { DocumentName = $"Label - {design.Barcode}" };
        document.PrinterSettings.PrinterName = printerName;
        if (!document.PrinterSettings.IsValid) throw new InvalidOperationException("Select an installed Brother QL-810 printer.");
        // Use the driver's media definition instead of inventing a custom paper size.
        var paper = document.PrinterSettings.PaperSizes.Cast<PaperSize>()
            .FirstOrDefault(p => LabelGeometry.IsSquareLabel(p.Width, p.Height));
        if (paper is null)
            throw new InvalidOperationException("The printer driver does not list 23 × 23 mm media. Install the Brother QL-810W driver and select DK-1221 in Printing Preferences.");
        document.DefaultPageSettings.PaperSize = paper;
        document.DefaultPageSettings.Landscape = false;
        document.DefaultPageSettings.Margins = new Margins(0, 0, 0, 0);
        document.PrinterSettings.Copies = (short)copies;
        document.OriginAtMargins = false;

        using var dialog = new PrintDialog
        {
            Document = document, UseEXDialog = true, AllowSomePages = false,
            AllowSelection = false, AllowPrintToFile = false
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;
        if (!document.PrinterSettings.PrinterName.Contains("QL-810", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Select a Brother QL-810W printer for these labels.");
        int remaining = document.PrinterSettings.Copies;
        if (remaining < 1 || remaining > 1000) throw new ArgumentException("Print between 1 and 1,000 labels at a time.");
        document.PrinterSettings.Copies = 1; // Each copy is a page; do not multiply driver copies.
        document.DefaultPageSettings.Landscape = false;
        document.PrintPage += (_, e) =>
        {
            if (!LabelGeometry.IsSquareLabel(e.PageSettings.PaperSize.Width, e.PageSettings.PaperSize.Height))
                throw new InvalidOperationException("Choose 23 × 23 mm (DK-1221) media in the print dialog.");
            float left = LabelGeometry.Hundredths(LabelGeometry.OffsetX);
            float top = LabelGeometry.Hundredths(LabelGeometry.OffsetY);
            float right = left + LabelGeometry.Hundredths(LabelGeometry.ContentWidth);
            float bottom = top + LabelGeometry.Hundredths(LabelGeometry.ContentHeight);
            var area = e.PageSettings.PrintableArea;
            if (area.Left > left + 1 || area.Top > top + 1 || area.Right < right - 1 || area.Bottom < bottom - 1)
                throw new InvalidOperationException("The driver's printable area is too small. Check DK-1221 media and portrait orientation in Printing Preferences.");
            var g = e.Graphics ?? throw new InvalidOperationException("Windows did not provide a print surface.");
            g.PageUnit = GraphicsUnit.Display;
            g.TranslateTransform(-e.PageSettings.HardMarginX, -e.PageSettings.HardMarginY);
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(bitmap, new RectangleF(left, top,
                LabelGeometry.Hundredths(LabelGeometry.ContentWidth), LabelGeometry.Hundredths(LabelGeometry.ContentHeight)),
                new RectangleF(LabelGeometry.OffsetX, LabelGeometry.OffsetY, LabelGeometry.ContentWidth, LabelGeometry.ContentHeight), GraphicsUnit.Pixel);
            e.HasMorePages = --remaining > 0;
        };
        document.Print();
    }
}
