namespace PdfEditor.Desktop.Models;

public sealed record ReplaceTextOperation(
    Guid Id,
    int PageIndex,
    Guid RegionId,
    string OriginalText,
    string NewText,
    RectPdf Bounds,
    double FontSize,
    string FontFamily,
    bool IsBold,
    bool IsItalic,
    DateTimeOffset CreatedAt);
