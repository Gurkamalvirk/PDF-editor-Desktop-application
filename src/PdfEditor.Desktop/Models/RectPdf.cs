namespace PdfEditor.Desktop.Models;

public readonly record struct RectPdf(double Left, double Bottom, double Right, double Top)
{
    public double Width => Math.Max(0, Right - Left);
    public double Height => Math.Max(0, Top - Bottom);

    public static RectPdf FromRaw(double x, double y, double width, double height)
    {
        var x2 = x + width;
        var y2 = y + height;
        return FromPoints(x, y, x2, y2);
    }

    public static RectPdf FromPoints(double x1, double y1, double x2, double y2)
        => new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));

    public static RectPdf Union(IEnumerable<RectPdf> rects)
    {
        var list = rects.ToList();
        if (list.Count == 0) return default;
        return new RectPdf(list.Min(r => r.Left), list.Min(r => r.Bottom), list.Max(r => r.Right), list.Max(r => r.Top));
    }
}
