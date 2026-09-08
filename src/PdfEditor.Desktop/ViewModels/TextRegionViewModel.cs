using PdfEditor.Desktop.Infrastructure;
using PdfEditor.Desktop.Models;
using System.Windows;

namespace PdfEditor.Desktop.ViewModels;

public sealed class TextRegionViewModel : ObservableObject
{
    public TextRegion Model { get; }
    private Rect _displayBounds;
    private bool _isSelected;
    private bool _isEditing;
    private string _displayText;
    private string _draftText;
    private double _fontSize;
    private string _fontFamily;
    private bool _isBold;
    private bool _isItalic;
    private double _zoom = 1.0;

    public TextRegionViewModel(TextRegion model)
    {
        Model = model;
        _displayText = model.Text;
        _draftText = model.Text;
        _fontSize = model.EstimatedFontSize;
        _fontFamily = model.FontFamily;
        _isBold = model.IsBold;
        _isItalic = model.IsItalic;
    }

    public Rect DisplayBounds { get => _displayBounds; set => SetProperty(ref _displayBounds, value); }
    public double Left => DisplayBounds.Left;
    public double Top => DisplayBounds.Top;
    public double Width => Math.Max(8, DisplayBounds.Width);
    public double Height => Math.Max(12, DisplayBounds.Height);
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }
    public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }
    public string DisplayText
    {
        get => _displayText;
        set
        {
            if (SetProperty(ref _displayText, value))
            {
                OnPropertyChanged(nameof(IsModified));
                OnPropertyChanged(nameof(IsOverflow));
            }
        }
    }
    public string DraftText { get => _draftText; set => SetProperty(ref _draftText, value); }
    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (SetProperty(ref _fontSize, Math.Clamp(value, 5, 96)))
            {
                OnPropertyChanged(nameof(DisplayFontSize));
                OnPropertyChanged(nameof(IsModified));
                OnPropertyChanged(nameof(IsOverflow));
                OnPropertyChanged(nameof(OverlayHeight));
            }
        }
    }

    // The page renderer is sized in PDF page units * zoom, so keeping the same numeric
    // font size and applying zoom gives the closest WPF overlay to the rendered glyphs.
    public double DisplayFontSize => Math.Max(5, FontSize * _zoom);

    public string FontFamily
    {
        get => _fontFamily;
        set
        {
            if (SetProperty(ref _fontFamily, value))
                OnPropertyChanged(nameof(IsModified));
        }
    }

    public bool IsBold
    {
        get => _isBold;
        set
        {
            if (SetProperty(ref _isBold, value))
            {
                OnPropertyChanged(nameof(DisplayFontWeight));
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }

    public bool IsItalic
    {
        get => _isItalic;
        set
        {
            if (SetProperty(ref _isItalic, value))
            {
                OnPropertyChanged(nameof(DisplayFontStyle));
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }

    public FontWeight DisplayFontWeight => IsBold ? FontWeights.Bold : FontWeights.Normal;
    public FontStyle DisplayFontStyle => IsItalic ? FontStyles.Italic : FontStyles.Normal;

    // Let the editor use a natural line height instead of visually squeezing a larger
    // original PDF font into a very tight glyph bounding box.
    public double OverlayHeight => Math.Max(Height, DisplayFontSize * 1.30);

    public bool IsModified =>
        !string.Equals(DisplayText, Model.Text, StringComparison.Ordinal) ||
        Math.Abs(FontSize - Model.EstimatedFontSize) > 0.01 ||
        !string.Equals(FontFamily, Model.FontFamily, StringComparison.OrdinalIgnoreCase) ||
        IsBold != Model.IsBold ||
        IsItalic != Model.IsItalic;

    public bool IsOverflow =>
        !string.IsNullOrEmpty(DisplayText) &&
        DisplayText.Length * FontSize * 0.52 > Math.Max(1, Model.Bounds.Width);

    public void SetDisplayBounds(Rect bounds, double zoom)
    {
        DisplayBounds = bounds;
        _zoom = zoom;
        OnPropertyChanged(nameof(Left));
        OnPropertyChanged(nameof(Top));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(DisplayFontSize));
        OnPropertyChanged(nameof(OverlayHeight));
    }
}
