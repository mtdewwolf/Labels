using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Labels.Core;

namespace Labels.Windows;

internal static class LabelRenderer
{
    public static Bitmap Render(LabelDesign design)
    {
        var barcode = design.Validate();
        var bitmap = new Bitmap(LabelGeometry.PaperDots, LabelGeometry.PaperDots);
        bitmap.SetResolution(LabelGeometry.Dpi, LabelGeometry.Dpi);
        try
        {
            using var g = Graphics.FromImage(bitmap);
            g.Clear(Color.White);
            g.TranslateTransform(LabelGeometry.OffsetX, LabelGeometry.OffsetY);
            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            if (design.Name.Length > 0) DrawText(g, design.Name, new RectangleF(0, 0, 236, 32), 23, 14, FontStyle.Bold);
            DrawText(g, design.PriceText, new RectangleF(0, 34, 236, 44), 38, 24, FontStyle.Bold);
            int x = (LabelGeometry.ContentWidth - barcode.WidthDots) / 2 + BarcodeLayout.QuietModules * barcode.ModuleDots;
            for (int i = 0; i < barcode.Bars.Length; i++)
                if (barcode.Bars[i]) g.FillRectangle(Brushes.Black, x + i * barcode.ModuleDots, 86, barcode.ModuleDots, 76);
            DrawText(g, design.Barcode, new RectangleF(0, 170, 236, 28), 20, 14, FontStyle.Regular);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    private static void DrawText(Graphics g, string text, RectangleF bounds, int maxSize, int minSize, FontStyle style)
    {
        using var format = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };
        for (int size = maxSize; size >= minSize; size--)
        {
            using var font = new Font("Arial", size, style, GraphicsUnit.Pixel);
            if (g.MeasureString(text, font, int.MaxValue, format).Width <= bounds.Width)
            {
                g.DrawString(text, font, Brushes.Black, bounds, format);
                return;
            }
        }
        throw new ArgumentException("Text does not fit on this label. Shorten the product name or barcode.");
    }
}
