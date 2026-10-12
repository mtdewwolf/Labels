using System.Globalization;
using System.Text.Json.Serialization;
using ZXing.OneD;

namespace Labels.Core;

public sealed record LabelDesign(
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Barcode,
    [property: JsonRequired] decimal Price)
{
    [JsonIgnore]
    public string PriceText => Price.ToString("C2", CultureInfo.GetCultureInfo("en-US"));

    public BarcodeLayout Validate()
    {
        if (Name is null || Name.Length > 40 || Name.Any(char.IsControl))
            throw new ArgumentException("Product name must be one line, up to 40 characters.");
        if (Price < 0 || Price > 9999.99m || decimal.Round(Price, 2) != Price)
            throw new ArgumentException("Price must be between $0.00 and $9,999.99 with at most two decimal places.");
        return BarcodeLayout.Create(Barcode);
    }
}

public sealed record BarcodeLayout(bool[] Bars, int ModuleDots)
{
    public const int QuietModules = 10;
    public int WidthDots => (Bars.Length + 2 * QuietModules) * ModuleDots;

    public static BarcodeLayout Create(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 40 || value.Any(c => c < 32 || c > 126))
            throw new ArgumentException("Enter a barcode using printable ASCII characters (letters, digits, or punctuation).");
        var bars = new Code128Writer().encode(value);
        int moduleDots = LabelGeometry.ContentWidth / (bars.Length + 2 * QuietModules);
        if (moduleDots < 2)
            throw new ArgumentException("Barcode is too long for this label. Try an even-length numeric ID (up to 10 digits), or up to 5 letters. Use a shorter item ID; the barcode will not be shrunk.");
        return new BarcodeLayout(bars, moduleDots);
    }
}

public static class LabelGeometry
{
    public const int Dpi = 300;
    public const int PaperDots = 272;
    // Brother's DK-1221 table: 236 x 202 printable dots, with 18 / 35 dot offsets.
    public const int ContentWidth = 236;
    public const int ContentHeight = 202;
    public const int OffsetX = 18;
    public const int OffsetY = 35;
    public static float Hundredths(int dots) => dots * 100f / Dpi;
    public static bool IsSquareLabel(int width, int height) =>
        Math.Abs(width - 23 / 25.4 * 100) <= 2 && Math.Abs(height - 23 / 25.4 * 100) <= 2;
}
