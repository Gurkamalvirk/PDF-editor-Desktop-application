# PDF Text Editor — Windows Desktop MVP

A local-first Windows desktop PDF text editor based on the supplied **Desktop/System-Based PDF Text Editor** technical blueprint.

## What works

- Open PDF from disk (`Ctrl+O`) or drag/drop a `.pdf` file.
- Render pages locally with PDFium.
- Extract text and PDF-space geometry.
- Highlight editable text regions.
- Double-click a region to edit it directly on the page.
- `Ctrl+Enter` commits an edit; `Esc` cancels.
- Undo/redo (`Ctrl+Z`, `Ctrl+Y`).
- Page navigation and zoom.
- Preserve PDFium-detected font family, real typographic size, bold, and italic styling where available.
- Font family / size / bold / italic adjustment for the selected replacement, committed through **Apply formatting**.
- Safe **Save As** that starts from the untouched source PDF, applies all active edit operations, writes a temporary output, verifies the output opens with PDFium, then moves it to the destination.
- All ordinary document processing is local; no browser or web server is used.

## Important editing model

PDFs are final-layout documents. This MVP uses the blueprint's recommended **visual replacement** strategy: cover the original region (white background in this build) and draw replacement text at the same PDF coordinates. It does not guarantee arbitrary content-stream deletion, exact font recovery, paragraph reflow, or perfect editing over complex image/gradient backgrounds.

## Requirements

- Windows 10/11 x64
- Visual Studio 2022 with **.NET desktop development**, or .NET 8 SDK
- Internet access for the first NuGet restore

NuGet dependencies pinned by this project:

- `PdfiumViewer.Net.WPF` 3.0.4
- `PDFsharp-WPF` 6.2.4

## Run from Visual Studio

1. Open `PdfTextEditor.sln`.
2. Restore NuGet packages.
3. Set `PdfEditor.Desktop` as startup project.
4. Run x64 (`F5`).

## Build a portable Windows executable

From PowerShell in the project root:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build-release.ps1
```

The output is placed in `publish\win-x64\`.

## Usage

1. Open a text-based PDF.
2. Move to the desired page.
3. Double-click a blue-highlighted text region.
4. Type replacement text.
5. Press `Ctrl+Enter`. The replacement keeps the detected original font settings by default. If you change family/size/bold/italic in the right panel, click **Apply formatting**.
6. Use **Save As** to create the edited PDF.

## Current MVP limits

- Whiteout background only.
- Text extraction is grouped conservatively; unusual PDFs may expose smaller or larger regions than expected.
- No paragraph reflow across columns/pages.
- Scanned/image-only pages are detected by the absence of a usable text layer, but Tesseract OCR is intentionally not bundled yet. `IOcrService` can be added as the next phase without changing the editing/export model.
- Embedded/subset PDF fonts that are not installed as Windows fonts may still use a close fallback during export.
- A region containing multiple mixed fonts is represented by the dominant detected style in this MVP.
- Complex scripts depend on the selected font and PDFsharp shaping path.
- Password-protected/malformed PDFs may be rejected.

## Recommended next upgrades

1. Tesseract 5.x local OCR for scanned pages.
2. Background color sampling / raster patch replacement.
3. Better font resolver and complex-script shaping.
4. Thumbnail sidebar and recent files.
5. Advanced native/commercial PDF engine for true content-stream editing when Acrobat-level fidelity is required.


### Build note (namespace-collision fix)
This revision is WPF-only at the application layer. It intentionally does **not** set `UseWindowsForms=true`; PDFium can still use its `System.Drawing.Common` dependency internally. The build script now stops immediately if `dotnet restore` or `dotnet publish` returns a non-zero exit code.

## If the EXE appears to do nothing

Version v4 publishes as a normal self-contained **folder** instead of a single-file bundle because the PDF renderer requires a native `pdfium.dll` runtime binary. Keep every file inside `publish\win-x64` together; do not copy only `PdfTextEditor.exe` elsewhere.

If the program still closes immediately:

1. Run `RUN_DIAGNOSTIC.bat` from the project root.
2. The app writes startup diagnostics to `%LOCALAPPDATA%\PdfTextEditor\startup-error.log`.
3. Send the contents of that log if startup still fails.


## v6 font-fidelity fix

Earlier builds estimated font size from the extracted glyph bounding-box height and defaulted extracted text to Segoe UI. That caused edited text to look substantially resized or like a different font. v6 reads PDFium's per-character font size and font information, removes common embedded subset prefixes, maps common PostScript font names to Windows families, and preserves bold/italic styling. The WPF replacement preview now binds those same properties, and export no longer auto-shrinks text to force it into the old box.
