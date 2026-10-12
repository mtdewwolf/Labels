using System.Drawing.Imaging;
using Labels.Core;
using Labels.Windows;
using ZXing;
using ZXing.Common;

namespace Labels.Windows.Checks;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Directory.CreateDirectory("dist/checks");
        foreach (var design in new[]
        {
            new LabelDesign("Sample item", "12345678", 9.99m),
            new LabelDesign("Item", "0000123456", 9999.99m),
            new LabelDesign("", "ABCDE", 0m)
        })
        {
            using var bitmap = LabelRenderer.Render(design);
            if (bitmap.Width != 272 || bitmap.Height != 272 || Math.Abs(bitmap.HorizontalResolution - 300) > 0.01)
                throw new Exception("Incorrect export size or DPI.");
            var pixels = new byte[bitmap.Width * bitmap.Height];
            for (int y = 0; y < bitmap.Height; y++)
                for (int x = 0; x < bitmap.Width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    pixels[y * bitmap.Width + x] = pixel.R;
                    if ((x < 18 || x >= 254 || y < 35 || y >= 237) && pixel.ToArgb() != Color.White.ToArgb())
                        throw new Exception("Artwork extends outside Brother's printable area.");
                }
            var source = new RGBLuminanceSource(pixels, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.Gray8);
            var decoded = new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(source)));
            if (decoded?.Text != design.Barcode) throw new Exception("Rendered label barcode could not be decoded.");
            bitmap.Save($"dist/checks/{design.Barcode}.png", ImageFormat.Png);
        }
        bool rejected = false;
        try { using var image = LabelRenderer.Render(new LabelDesign(new string('W', 40), "1234", 1m)); }
        catch (ArgumentException) { rejected = true; }
        if (!rejected) throw new Exception("Oversized product name was not rejected.");
        using var form = new MainForm();
        form.Show();
        Application.DoEvents();
        using var screenshot = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(screenshot, new Rectangle(Point.Empty, form.Size));
        screenshot.Save("dist/checks/app.png", ImageFormat.Png);
        form.Close();
        Console.WriteLine("Passed Windows rendering, barcode decoding, printable bounds, text overflow, and form startup checks.");
    }
}
