<img src="assets/icon.svg" width="64" height="64" alt="">

# Labels for Windows

A native Windows label editor for the Brother QL-810W and **DK-1221 0.9-inch square labels (23 × 23 mm)**. Create a product label with a USD price and Code 128 barcode, see a live preview, save/open label files, export a 300 DPI PNG, and print one or more copies.

## Run

1. Use Windows 10/11 on an x64 PC. Download the `Labels-windows-x64` artifact from a successful [Windows build](https://github.com/mtdewwolf/Labels/actions/workflows/windows.yml), or the ZIP from a published release. Extract all files.
2. Install Brother's Windows QL-810W printer driver and connect the printer over USB or Wi-Fi through a Windows print queue. Load DK-1221 square labels. See [Brother support](https://support.brother.com/g/b/producttop.aspx?c=us&lang=en&prod=lpql810weus).
3. Run `Labels.exe`. No separate .NET installation is required. The executable is currently unsigned.
4. Enter the product name, barcode value, price, and quantity. Select the Brother printer; use **Refresh printers** after installing it.
5. Choose **Print labels…**. Confirm the printer and **23 × 23 mm / DK-1221** media in the Windows print dialog. The dialog's final copy count is used. First print one label and scan it before printing a batch.

Printing requires the Brother driver, including when the printer is connected over Wi-Fi. A successful return from printing means Windows accepted the job; check the Windows queue for printer/offline/media errors. This app does not directly monitor printer hardware.

## Small-label limits

The physical label is 272 × 272 dots at 300 DPI. Brother specifies a printable area of **236 × 202 dots**, with 18-dot horizontal and 35-dot vertical offsets. Artwork stays inside that area; the preview, PNG, and printed page use the same renderer. Printing uses the driver's existing square-label media definition, compensates for hardware margins, and does not stretch the artwork to fill the sheet.

- Code 128 accepts printable ASCII. Leading zeroes are preserved. This generates an internal item-ID barcode, not a UPC/EAN or GS1 barcode.
- The encoder keeps at least two printer dots per narrow module and ten quiet modules on each side. Oversized barcodes are rejected, never squeezed to fit. Even-length numeric IDs up to 10 digits or text IDs up to 5 characters normally fit. Mixed content and odd-length numbers can take more space.
- Short product names work best. Text fits down to a minimum size and is rejected if it still overflows. Prices range from $0.00 to $9,999.99.
- Two-dot modules are small (about 0.169 mm); scanner performance depends on equipment and print quality. Test the actual label with your scanner. This is not a claim of GS1 compliance or verified barcode quality.
- Saved `.label.json` files contain product name, barcode, and price. Printer and quantity remain local editor choices. PNG exports include the full physical label and its white margins.

Media dimensions come from [Brother's raster reference, page 15](https://download.brother.com/welcome/docp100278/cv_ql800_eng_raster_101.pdf) (PDF page 19). Physical printing, alignment, cutting, and scanning require verification with an actual QL-810W and DK-1221 roll.

## Build and check

Install the .NET 10 SDK. From the repository root:

```sh
dotnet run --project tests/Labels.Checks -c Release
dotnet publish src/Labels.Windows -c Release -r win-x64 --self-contained true -o dist/win-x64
```

The standalone Windows executable is `dist/win-x64/Labels.exe`. Cross-compilation is supported; running the editor and printing require Windows. On Windows also run:

```sh
dotnet run --project tests/Labels.Windows.Checks -c Release
```

The core checks decode the actual barcode dot layout and test quiet zones, validation, saved labels, and media geometry. Windows checks decode rendered labels, check printable bounds, reject overflowing text, and open the editor. CI also launches and closes the packaged executable. CI uploads screenshots and sample labels alongside the Windows build artifact.

To prepare a release, push a `v*` tag after verification. The workflow builds and checks Windows, then creates a **draft** GitHub release with a ZIP. Review and publish that draft after the physical-printer check. The application version is set in `src/Labels.Windows/Labels.Windows.csproj`; update it before tagging. No release is published automatically.
