using PdfEditor.Desktop.Models;
using System.Windows;

namespace PdfEditor.Desktop.Services;

public sealed class GeometryService
{
    public Rect PdfToView(RectPdf rect, double pageHeightPdf, double zoom)
    {
        var x = rect.Left * zoom;
        var y = (pageHeightPdf - rect.Top) * zoom;
        return new Rect(x, y, Math.Max(1, rect.Width * zoom), Math.Max(1, rect.Height * zoom));
    }
}
