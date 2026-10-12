using System.Text.Json;
using Labels.Core;
using ZXing;
using ZXing.Common;

int checks = 0;
void Check(bool value, string description)
{
    if (!value) throw new Exception(description);
    checks++;
}
void Reject(Action action, string description)
{
    try { action(); }
    catch (ArgumentException) { checks++; return; }
    throw new Exception(description);
}

// Decode the actual integer-dot layout, not just the encoder's return value.
foreach (string value in new[] { "12345678", "0000123456", "ABCDE", "A-12", "1", "1234567" })
{
    var layout = BarcodeLayout.Create(value);
    Check(layout.ModuleDots >= 2 && layout.WidthDots <= 236, "Barcode geometry must fit without fractional dots.");
    const int height = 76;
    var pixels = Enumerable.Repeat((byte)255, layout.WidthDots * height).ToArray();
    for (int y = 0; y < height; y++)
        for (int x = 0; x < layout.Bars.Length; x++)
            if (layout.Bars[x])
                for (int dot = 0; dot < layout.ModuleDots; dot++)
                    pixels[y * layout.WidthDots + (x + BarcodeLayout.QuietModules) * layout.ModuleDots + dot] = 0;
    var source = new RGBLuminanceSource(pixels, layout.WidthDots, height, RGBLuminanceSource.BitmapFormat.Gray8);
    var result = new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(source)));
    Check(result?.Text == value && result.BarcodeFormat == BarcodeFormat.CODE_128, $"Code 128 round trip failed: {value}");
    Check(pixels.Take(10 * layout.ModuleDots).All(p => p == 255), "Left quiet zone must remain white.");
    Check(pixels.Skip(layout.WidthDots - 10 * layout.ModuleDots).Take(10 * layout.ModuleDots).All(p => p == 255), "Right quiet zone must remain white.");
}
foreach (string value in new[] { "", "123456789", "123456789012", "ABCDEF", "a\nb", "é", new string('1', 100) })
    Reject(() => BarcodeLayout.Create(value), $"Should reject invalid/oversized barcode: {value}");
foreach (decimal value in new[] { -1m, 10000m, 1.001m })
    Reject(() => new LabelDesign("Item", "1234", value).Validate(), "Invalid price accepted.");
Reject(() => new LabelDesign("line\nbreak", "1234", 1m).Validate(), "Multiline product accepted.");
Reject(() => new LabelDesign(null!, "1234", 1m).Validate(), "Null name accepted.");
Check(new LabelDesign("Item", "1234", 9.99m).PriceText == "$9.99", "Price formatting failed.");
var design = new LabelDesign("Item", "00001234", 12.50m);
Check(JsonSerializer.Deserialize<LabelDesign>(JsonSerializer.Serialize(design)) == design, "Label file round trip failed.");
try
{
    JsonSerializer.Deserialize<LabelDesign>("{\"Name\":\"Item\",\"Barcode\":\"1234\"}");
    throw new Exception("Missing price accepted.");
}
catch (JsonException) { checks++; }
Check(LabelGeometry.IsSquareLabel(91, 91), "Driver media rounding failed.");
Check(!LabelGeometry.IsSquareLabel(114, 114) && !LabelGeometry.IsSquareLabel(91, 244), "Wrong driver media accepted.");
Check(LabelGeometry.OffsetX * 2 + LabelGeometry.ContentWidth == LabelGeometry.PaperDots, "Horizontal media geometry failed.");
Check(LabelGeometry.OffsetY * 2 + LabelGeometry.ContentHeight == LabelGeometry.PaperDots, "Vertical media geometry failed.");
Console.WriteLine($"Passed {checks} checks: barcode decoding, quiet zones, validation, persistence, and media geometry.");
