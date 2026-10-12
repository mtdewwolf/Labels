using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using Labels.Core;

namespace Labels.Windows;

internal static class LabelPrinter
{
    public static void Print(IWin32Window owner, LabelDesign design, string printerName, int copies)
    {
        if (copies < 1 || copies > PrintList.MaxLabels)
            throw new ArgumentException("Print between 1 and 1,000 labels at a time.");
        PrintPlan(owner, printerName, [new PrintListEntry(design, copies)], useDialogCopies: true);
    }

    public static void PrintJob(IWin32Window owner, string printerName, IReadOnlyList<PrintListEntry> entries) =>
        PrintPlan(owner, printerName, entries, useDialogCopies: false);

    private static void PrintPlan(IWin32Window owner, string printerName, IReadOnlyList<PrintListEntry> entries, bool useDialogCopies)
    {
        var plan = entries.ToArray();
        if (plan.Length == 0) throw new InvalidOperationException("Add at least one label to the print list.");
        int total = 0;
        foreach (var entry in plan)
        {
            if (entry.Design is null) throw new ArgumentException("This label is missing.");
            entry.Design.Validate();
            if (entry.Quantity < 1 || entry.Quantity > PrintList.MaxLineQuantity)
                throw new ArgumentException($"Each label needs a quantity from 1 to {PrintList.MaxLineQuantity}.");
            total += entry.Quantity;
        }
        if (total < 1 || total > PrintList.MaxLabels)
            throw new ArgumentException($"A print job can include at most {PrintList.MaxLabels} labels.");

        using var document = new PrintDocument();
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
        document.OriginAtMargins = false;
        document.DocumentName = useDialogCopies ? $"Label - {plan[0].Design.Barcode}" : $"Labels - {total}";
        document.PrinterSettings.Copies = useDialogCopies ? (short)plan[0].Quantity : (short)1;

        using var dialog = new PrintDialog
        {
            Document = document, UseEXDialog = true, AllowSomePages = false,
            AllowSelection = false, AllowPrintToFile = false
        };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;
        if (!document.PrinterSettings.PrinterName.Contains("QL-810", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Select a Brother QL-810W printer for these labels.");
        if (useDialogCopies)
        {
            int copies = document.PrinterSettings.Copies;
            if (copies < 1 || copies > PrintList.MaxLabels)
                throw new ArgumentException("Print between 1 and 1,000 labels at a time.");
            plan = [new PrintListEntry(plan[0].Design, copies)];
            total = copies;
        }
        else if (document.PrinterSettings.Copies != 1)
        {
            // List quantities are the job. Driver copies would multiply every staged label.
            throw new InvalidOperationException("Set each quantity in the print list. Leave Copies at 1 in the print dialog.");
        }
        document.PrinterSettings.Copies = 1; // Each label is a page; do not multiply driver copies.
        document.DefaultPageSettings.Landscape = false;

        int page = 0;
        LabelDesign? shown = null;
        Bitmap? bitmap = null;
        document.PrintPage += (_, e) =>
        {
            var design = PrintList.DesignAt(plan, page);
            if (shown != design)
            {
                bitmap?.Dispose();
                bitmap = null;
                bitmap = LabelRenderer.Render(design);
                shown = design;
            }
            if (bitmap is null) throw new InvalidOperationException("The label artwork could not be prepared.");
            Draw(e, bitmap);
            e.HasMorePages = ++page < total;
        };
        try { document.Print(); }
        finally { bitmap?.Dispose(); }
    }

    private static void Draw(PrintPageEventArgs e, Bitmap bitmap)
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
    }
}
