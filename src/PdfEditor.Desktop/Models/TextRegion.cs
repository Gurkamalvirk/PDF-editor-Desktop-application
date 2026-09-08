namespace PdfEditor.Desktop.Models;

public sealed record TextRegion(
    Guid Id,
    int PageIndex,
    string Text,
    RectPdf Bounds,
    int SourceOffset,
    int SourceLength,
    double EstimatedFontSize,
    string FontFamily = "Segoe UI",
    bool IsBold = false,
    bool IsItalic = false);
