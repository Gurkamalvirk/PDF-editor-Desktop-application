using System.IO;
using PdfEditor.Desktop.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf.IO;

namespace PdfEditor.Desktop.Services;

public sealed class ExportService
{
    public async Task ExportAsync(
        string sourcePath,
        string destinationPath,
        IReadOnlyList<ReplaceTextOperation> operations,
        PdfDocumentService verifier,
        CancellationToken cancellationToken = default)
    {
        if (operations.Count == 0)
        {
            File.Copy(sourcePath, destinationPath, true);
            return;
        }

        var directory = Path.GetDirectoryName(destinationPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var temp = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp.pdf");

        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var document = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Modify);
                foreach (var op in operations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = document.Pages[op.PageIndex];
                    using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

                    var pageHeight = page.Height.Point;
                    var x = op.Bounds.Left;
                    var y = pageHeight - op.Bounds.Top;
                    var width = Math.Max(1, op.Bounds.Width);
                    var height = Math.Max(1, op.Bounds.Height);

                    gfx.DrawRectangle(XBrushes.White, x - 0.75, y - 0.75, width + 1.5, height + 1.5);

                    // Preserve the PDFium-detected font size/family/style. We deliberately do
                    // not auto-shrink replacement text. If it is too long, the UI warns instead.
                    var requestedSize = Math.Clamp(op.FontSize, 5, 96);
                    var font = CreateFontWithFallback(op.FontFamily, requestedSize, op.IsBold, op.IsItalic);
                    var brush = XBrushes.Black;
                    var textRect = new XRect(x, y, width, Math.Max(height, requestedSize * 1.4));
                    gfx.DrawString(op.NewText, font, brush, textRect, XStringFormats.TopLeft);
                }

                document.Save(temp);
            }, cancellationToken);

            await verifier.VerifyReadableAsync(temp, cancellationToken);

            if (File.Exists(destinationPath)) File.Delete(destinationPath);
            File.Move(temp, destinationPath);
        }
        catch
        {
            if (File.Exists(temp)) File.Delete(temp);
            throw;
        }
    }

    private static XFont CreateFontWithFallback(string family, double size, bool bold, bool italic)
    {
        var style = bold && italic ? XFontStyleEx.BoldItalic
            : bold ? XFontStyleEx.Bold
            : italic ? XFontStyleEx.Italic
            : XFontStyleEx.Regular;

        try
        {
            return new XFont(string.IsNullOrWhiteSpace(family) ? "Segoe UI" : family, size, style);
        }
        catch
        {
            try { return new XFont("Arial", size, style); }
            catch { return new XFont("Arial", size, XFontStyleEx.Regular); }
        }
    }
}
