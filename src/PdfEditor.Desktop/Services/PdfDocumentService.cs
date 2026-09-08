using PdfEditor.Desktop.Models;
using PdfiumViewer;
using PdfiumViewer.Core;
using PdfiumViewer.Enums;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using System.Windows.Media.Imaging;

namespace PdfEditor.Desktop.Services;

public sealed class PdfDocumentService : IDisposable
{
    private PdfDocument? _document;
    private IReadOnlyList<WpfSize> _pageSizes = Array.Empty<WpfSize>();
    private static bool _fontMetadataAvailable = true;

    public int PageCount => _document?.PageCount ?? 0;
    public IReadOnlyList<WpfSize> PageSizes => _pageSizes;

    public Task OpenAsync(string path, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        _document?.Dispose();
        _document = PdfDocument.Load(path);
        _pageSizes = _document.Pages
            .Select(page => new WpfSize(page.Width, page.Height))
            .ToArray();
    }, cancellationToken);

    public Task<BitmapImage> RenderPageAsync(int pageIndex, double zoom, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        var document = _document ?? throw new InvalidOperationException("No PDF is open.");
        cancellationToken.ThrowIfCancellationRequested();
        var page = document.Pages[pageIndex];
        var width = Math.Max(1, (int)Math.Round(page.Width * zoom));
        var height = Math.Max(1, (int)Math.Round(page.Height * zoom));

        using var image = page.Render(width, height, 96f, 96f, PdfRotation.Rotate0, PdfRenderFlags.Annotations);
        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        ms.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }, cancellationToken);

    public Task<IReadOnlyList<TextRegion>> ExtractRegionsAsync(int pageIndex, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<TextRegion>>(() =>
    {
        var document = _document ?? throw new InvalidOperationException("No PDF is open.");
        cancellationToken.ThrowIfCancellationRequested();
        var page = document.Pages[pageIndex];
        var pageText = page.GetText() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(pageText)) return Array.Empty<TextRegion>();

        var regions = new List<TextRegion>();
        var matches = Regex.Matches(pageText, @"[^\r\n]{1,120}");
        foreach (Match match in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var raw = match.Value;
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var startTrim = raw.Length - raw.TrimStart().Length;
            var trimmed = raw.Trim();
            if (trimmed.Length == 0) continue;
            var offset = match.Index + startTrim;

            var viewRects = page.GetTextBounds(offset, trimmed.Length);
            if (viewRects.Count == 0) continue;

            var pdfRects = new List<RectPdf>(viewRects.Count);
            foreach (var rect in viewRects)
            {
                var a = page.DeviceToPage(new WpfPoint(rect.Left, rect.Top));
                var b = page.DeviceToPage(new WpfPoint(rect.Right, rect.Bottom));
                var normalized = RectPdf.FromPoints(a.X, a.Y, b.X, b.Y);
                if (normalized.Width > 0.5 && normalized.Height > 0.5)
                    pdfRects.Add(normalized);
            }
            if (pdfRects.Count == 0) continue;

            var bounds = RectPdf.Union(pdfRects);
            var font = GetBestFontMetadata(page, offset, trimmed.Length, bounds);
            var regionId = StableRegionId(pageIndex, offset, trimmed.Length, bounds);
            regions.Add(new TextRegion(
                regionId,
                pageIndex,
                trimmed,
                bounds,
                offset,
                trimmed.Length,
                font.Size,
                font.Family,
                font.IsBold,
                font.IsItalic));
        }

        return regions;
    }, cancellationToken);

    public Task VerifyReadableAsync(string path, CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var verification = PdfDocument.Load(path);
        if (verification.PageCount <= 0)
            throw new InvalidDataException("Exported PDF contains no readable pages.");
    }, cancellationToken);

    private static FontMetadata GetBestFontMetadata(PdfPage page, int offset, int length, RectPdf bounds)
    {
        // Old builds estimated size from glyph-box height. That can be dramatically smaller
        // than the actual typographic font size. Prefer PDFium's per-character metadata.
        var fallbackSize = Math.Clamp(bounds.Height / 0.75, 6.0, 72.0);
        if (!_fontMetadataAvailable || length <= 0)
            return new FontMetadata("Segoe UI", fallbackSize, false, false);

        try
        {
            var samples = new List<FontMetadata>();
            var sampleCount = Math.Min(length, 48);
            var step = Math.Max(1, length / sampleCount);

            for (var relative = 0; relative < length && samples.Count < 48; relative += step)
            {
                var index = offset + relative;
                var size = PdfiumTextNative.FPDFText_GetFontSize(page.TextPage, index);
                if (size <= 0 || double.IsNaN(size) || double.IsInfinity(size))
                    continue;

                var rawName = GetFontName(page.TextPage, index, out var flags);
                var weight = TryGetFontWeight(page.TextPage, index);
                var parsed = ParseFont(rawName, flags, weight);
                samples.Add(new FontMetadata(parsed.Family, Math.Clamp(size, 5.0, 96.0), parsed.IsBold, parsed.IsItalic));
            }

            if (samples.Count == 0)
                return new FontMetadata("Segoe UI", fallbackSize, false, false);

            var dominant = samples
                .GroupBy(s => (s.Family, s.IsBold, s.IsItalic))
                .OrderByDescending(g => g.Count())
                .First();

            var sizes = dominant.Select(s => s.Size).OrderBy(v => v).ToArray();
            var medianSize = sizes[sizes.Length / 2];
            return new FontMetadata(dominant.Key.Family, medianSize, dominant.Key.IsBold, dominant.Key.IsItalic);
        }
        catch (EntryPointNotFoundException)
        {
            _fontMetadataAvailable = false;
            return new FontMetadata("Segoe UI", fallbackSize, false, false);
        }
        catch (DllNotFoundException)
        {
            _fontMetadataAvailable = false;
            return new FontMetadata("Segoe UI", fallbackSize, false, false);
        }
        catch
        {
            // Font metadata must never make a page fail to open. Geometry-based fallback
            // is less accurate but keeps editing available.
            return new FontMetadata("Segoe UI", fallbackSize, false, false);
        }
    }

    private static string? GetFontName(IntPtr textPage, int index, out int flags)
    {
        flags = 0;
        var required = PdfiumTextNative.FPDFText_GetFontInfo(textPage, index, IntPtr.Zero, 0, out flags);
        if (required == 0 || required > 4096)
            return null;

        var buffer = Marshal.AllocHGlobal((int)required);
        try
        {
            var written = PdfiumTextNative.FPDFText_GetFontInfo(textPage, index, buffer, required, out flags);
            if (written == 0)
                return null;

            var bytes = new byte[(int)written];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            var nul = Array.IndexOf(bytes, (byte)0);
            if (nul < 0) nul = bytes.Length;
            return Encoding.UTF8.GetString(bytes, 0, nul);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int TryGetFontWeight(IntPtr textPage, int index)
    {
        try { return PdfiumTextNative.FPDFText_GetFontWeight(textPage, index); }
        catch (EntryPointNotFoundException) { return -1; }
    }

    private static ParsedFont ParseFont(string? rawName, int flags, int weight)
    {
        var name = string.IsNullOrWhiteSpace(rawName) ? "Segoe UI" : rawName.Trim().TrimStart('/');

        // Embedded subset fonts commonly look like ABCDEF+Calibri. The six-letter prefix
        // is not part of the Windows family name.
        if (name.Length > 7 && name[6] == '+' && name[..6].All(char.IsUpper))
            name = name[7..];

        var lower = name.ToLowerInvariant();
        var isBold = weight >= 600 ||
                     lower.Contains("bold") || lower.Contains("semibold") || lower.Contains("demibold") ||
                     lower.Contains("black") || lower.Contains("heavy") || (flags & 0x40000) != 0;
        var isItalic = lower.Contains("italic") || lower.Contains("oblique") || (flags & 0x40) != 0;

        string family;
        if (lower.StartsWith("timesnewromanps") || lower.StartsWith("times-new-roman") || lower.StartsWith("timesroman") || lower == "times-roman")
            family = "Times New Roman";
        else if (lower.StartsWith("arial"))
            family = "Arial";
        else if (lower.StartsWith("couriernewps") || lower == "courier" || lower.StartsWith("courier-"))
            family = "Courier New";
        else if (lower.StartsWith("helvetica"))
            family = "Arial"; // closest standard Windows substitute
        else if (lower.StartsWith("calibri"))
            family = "Calibri";
        else if (lower.StartsWith("cambria"))
            family = "Cambria";
        else if (lower.StartsWith("georgia"))
            family = "Georgia";
        else if (lower.StartsWith("verdana"))
            family = "Verdana";
        else if (lower.StartsWith("tahoma"))
            family = "Tahoma";
        else
            family = CleanPostScriptFamily(name);

        return new ParsedFont(string.IsNullOrWhiteSpace(family) ? "Segoe UI" : family, isBold, isItalic);
    }

    private static string CleanPostScriptFamily(string name)
    {
        var cleaned = Regex.Replace(name,
            @"(?i)([-_, ]?(bolditalic|boldoblique|semibolditalic|semibold|demibold|bold|italic|oblique|regular|roman|medium|light|black|heavy))+$",
            string.Empty);
        cleaned = Regex.Replace(cleaned, @"(?i)MT$", string.Empty);
        cleaned = cleaned.Replace('-', ' ').Replace('_', ' ').Trim();
        return Regex.Replace(cleaned, @"\s+", " ");
    }

    private static Guid StableRegionId(int pageIndex, int offset, int length, RectPdf bounds)
    {
        var key = $"{pageIndex}|{offset}|{length}|{bounds.Left:F3}|{bounds.Bottom:F3}|{bounds.Right:F3}|{bounds.Top:F3}";
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }

    public void Dispose()
    {
        _document?.Dispose();
        _document = null;
        _pageSizes = Array.Empty<WpfSize>();
    }

    private readonly record struct FontMetadata(string Family, double Size, bool IsBold, bool IsItalic);
    private readonly record struct ParsedFont(string Family, bool IsBold, bool IsItalic);

    private static class PdfiumTextNative
    {
        [DllImport("pdfium.dll")]
        internal static extern double FPDFText_GetFontSize(IntPtr textPage, int index);

        [DllImport("pdfium.dll")]
        internal static extern uint FPDFText_GetFontInfo(IntPtr textPage, int index, IntPtr buffer, uint buflen, out int flags);

        [DllImport("pdfium.dll")]
        internal static extern int FPDFText_GetFontWeight(IntPtr textPage, int index);
    }
}
