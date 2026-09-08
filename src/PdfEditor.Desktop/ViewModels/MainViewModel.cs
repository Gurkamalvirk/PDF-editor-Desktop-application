using Microsoft.Win32;
using PdfEditor.Desktop.Infrastructure;
using PdfEditor.Desktop.Models;
using PdfEditor.Desktop.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PdfEditor.Desktop.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private PdfDocumentService? _pdf;
    private PdfDocumentService Pdf => _pdf ??= new PdfDocumentService();
    private readonly GeometryService _geometry = new();
    private readonly EditHistoryService _history = new();
    private readonly ExportService _export = new();
    private CancellationTokenSource? _pageCts;

    private string? _sourcePath;
    private BitmapImage? _pageImage;
    private int _currentPage;
    private int _pageCount;
    private double _zoom = 1.25;
    private bool _isBusy;
    private string _status = "Open a PDF to begin.";
    private TextRegionViewModel? _selectedRegion;
    private double _pageWidth;
    private double _pageHeight;

    public MainViewModel()
    {
        OpenCommand = new AsyncRelayCommand(OpenAsync, () => !IsBusy);
        SaveAsCommand = new AsyncRelayCommand(SaveAsAsync, () => !IsBusy && SourcePath is not null);
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync, () => !IsBusy && CurrentPage > 0);
        NextPageCommand = new AsyncRelayCommand(NextPageAsync, () => !IsBusy && CurrentPage + 1 < PageCount);
        ZoomInCommand = new AsyncRelayCommand(() => ChangeZoomAsync(Zoom + 0.25), () => SourcePath is not null && !IsBusy);
        ZoomOutCommand = new AsyncRelayCommand(() => ChangeZoomAsync(Math.Max(0.5, Zoom - 0.25)), () => SourcePath is not null && !IsBusy);
        UndoCommand = new RelayCommand(Undo, () => _history.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => _history.CanRedo);
        ApplyPropertiesCommand = new RelayCommand(ApplySelectedProperties, () => SelectedRegion is not null && !IsBusy);
    }

    public ObservableCollection<TextRegionViewModel> Regions { get; } = new();

    public string? SourcePath { get => _sourcePath; private set { if (SetProperty(ref _sourcePath, value)) OnPropertyChanged(nameof(WindowTitle)); } }
    public BitmapImage? PageImage { get => _pageImage; private set => SetProperty(ref _pageImage, value); }
    public int CurrentPage { get => _currentPage; private set { if (SetProperty(ref _currentPage, value)) OnPropertyChanged(nameof(PageDisplay)); } }
    public int PageCount { get => _pageCount; private set { if (SetProperty(ref _pageCount, value)) OnPropertyChanged(nameof(PageDisplay)); } }
    public double Zoom { get => _zoom; private set { if (SetProperty(ref _zoom, value)) OnPropertyChanged(nameof(ZoomDisplay)); } }
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) RaiseCommandStates(); } }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public TextRegionViewModel? SelectedRegion
    {
        get => _selectedRegion;
        private set
        {
            if (SetProperty(ref _selectedRegion, value))
                ApplyPropertiesCommand?.RaiseCanExecuteChanged();
        }
    }
    public double PageWidth { get => _pageWidth; private set => SetProperty(ref _pageWidth, value); }
    public double PageHeight { get => _pageHeight; private set => SetProperty(ref _pageHeight, value); }

    public string PageDisplay => PageCount == 0 ? "0 / 0" : $"{CurrentPage + 1} / {PageCount}";
    public string ZoomDisplay => $"{Zoom:P0}";
    public string WindowTitle => $"PDF Text Editor{(_history.IsDirty ? " *" : "")}{(SourcePath is null ? "" : " — " + Path.GetFileName(SourcePath))}";

    public ICommand OpenCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand PreviousPageCommand { get; }
    public ICommand NextPageCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand ApplyPropertiesCommand { get; }

    public async Task OpenPathAsync(string path)
    {
        if (!File.Exists(path)) return;
        IsBusy = true;
        try
        {
            Status = "Opening PDF…";
            await Pdf.OpenAsync(path);
            SourcePath = path;
            PageCount = Pdf.PageCount;
            CurrentPage = 0;
            _history.Clear();
            OnPropertyChanged(nameof(WindowTitle));
            await LoadCurrentPageAsync();
            Status = PageCount > 0 ? "Ready. Double-click a highlighted text region to edit." : "PDF has no pages.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Could not open PDF", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Status = "Open failed.";
        }
        finally { IsBusy = false; RaiseCommandStates(); }
    }

    private async Task OpenAsync()
    {
        var dialog = new OpenFileDialog { Filter = "PDF documents (*.pdf)|*.pdf", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog() == true) await OpenPathAsync(dialog.FileName);
    }

    private async Task SaveAsAsync()
    {
        if (SourcePath is null) return;
        var dialog = new SaveFileDialog
        {
            Filter = "PDF documents (*.pdf)|*.pdf",
            DefaultExt = ".pdf",
            AddExtension = true,
            FileName = Path.GetFileNameWithoutExtension(SourcePath) + "-edited.pdf"
        };
        if (dialog.ShowDialog() != true) return;
        if (string.Equals(Path.GetFullPath(dialog.FileName), Path.GetFullPath(SourcePath), StringComparison.OrdinalIgnoreCase))
        {
            System.Windows.MessageBox.Show("Choose a new output file. The editor keeps the opened source PDF untouched.", "Choose another file", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }

        IsBusy = true;
        try
        {
            Status = "Exporting safely…";
            await _export.ExportAsync(SourcePath, dialog.FileName, _history.ActiveOperations, Pdf);
            Status = $"Saved: {dialog.FileName}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "Export failed", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Status = "Export failed; the source PDF was not changed.";
        }
        finally { IsBusy = false; }
    }

    private async Task LoadCurrentPageAsync()
    {
        if (PageCount == 0) return;
        _pageCts?.Cancel();
        _pageCts?.Dispose();
        _pageCts = new CancellationTokenSource();
        var token = _pageCts.Token;

        Regions.Clear();
        SelectedRegion = null;
        var pageSize = Pdf.PageSizes[CurrentPage];
        PageWidth = pageSize.Width * Zoom;
        PageHeight = pageSize.Height * Zoom;
        Status = $"Rendering page {CurrentPage + 1}…";

        var renderTask = Pdf.RenderPageAsync(CurrentPage, Zoom, token);
        var regionsTask = Pdf.ExtractRegionsAsync(CurrentPage, token);
        await Task.WhenAll(renderTask, regionsTask);
        token.ThrowIfCancellationRequested();

        PageImage = renderTask.Result;
        foreach (var model in regionsTask.Result)
        {
            var effective = _history.EffectiveOperation(model.Id);
            var effectiveText = effective?.NewText ?? model.Text;
            var vm = new TextRegionViewModel(model)
            {
                DisplayText = effectiveText,
                DraftText = effectiveText,
                FontSize = effective?.FontSize ?? model.EstimatedFontSize,
                FontFamily = effective?.FontFamily ?? model.FontFamily,
                IsBold = effective?.IsBold ?? model.IsBold,
                IsItalic = effective?.IsItalic ?? model.IsItalic
            };
            vm.SetDisplayBounds(_geometry.PdfToView(model.Bounds, pageSize.Height, Zoom), Zoom);
            Regions.Add(vm);
        }
        Status = Regions.Count == 0
            ? "No usable text layer detected on this page. OCR is not bundled in this MVP."
            : $"{Regions.Count} editable text region(s) detected.";
        RaiseCommandStates();
    }

    public void SelectRegion(TextRegionViewModel region)
    {
        foreach (var item in Regions) item.IsSelected = false;
        region.IsSelected = true;
        SelectedRegion = region;
    }

    public void BeginEdit(TextRegionViewModel region)
    {
        SelectRegion(region);
        region.DraftText = region.DisplayText;
        region.IsEditing = true;
    }

    public void CancelEdit(TextRegionViewModel region)
    {
        region.DraftText = region.DisplayText;
        region.IsEditing = false;
    }

    public void CommitEdit(TextRegionViewModel region)
    {
        var newText = region.DraftText ?? string.Empty;
        region.IsEditing = false;
        CommitRegionState(region, newText);
    }

    private void ApplySelectedProperties()
    {
        if (SelectedRegion is null) return;
        CommitRegionState(SelectedRegion, SelectedRegion.DisplayText);
    }

    private void CommitRegionState(TextRegionViewModel region, string newText)
    {
        var previous = _history.EffectiveOperation(region.Model.Id);
        var previousText = previous?.NewText ?? region.Model.Text;
        var previousFontSize = previous?.FontSize ?? region.Model.EstimatedFontSize;
        var previousFontFamily = previous?.FontFamily ?? region.Model.FontFamily;
        var previousBold = previous?.IsBold ?? region.Model.IsBold;
        var previousItalic = previous?.IsItalic ?? region.Model.IsItalic;
        var normalizedFamily = string.IsNullOrWhiteSpace(region.FontFamily) ? region.Model.FontFamily : region.FontFamily.Trim();
        if (newText == previousText && Math.Abs(region.FontSize - previousFontSize) < 0.01 &&
            string.Equals(normalizedFamily, previousFontFamily, StringComparison.OrdinalIgnoreCase) &&
            region.IsBold == previousBold && region.IsItalic == previousItalic)
            return;

        var operation = new ReplaceTextOperation(
            Guid.NewGuid(),
            region.Model.PageIndex,
            region.Model.Id,
            previousText,
            newText,
            region.Model.Bounds,
            region.FontSize,
            normalizedFamily,
            region.IsBold,
            region.IsItalic,
            DateTimeOffset.Now);

        _history.Add(operation);
        region.DisplayText = newText;
        region.DraftText = newText;
        region.FontFamily = normalizedFamily;
        OnPropertyChanged(nameof(WindowTitle));
        RaiseCommandStates();
        Status = region.IsOverflow
            ? "Edit committed. Warning: replacement text may overflow the original region."
            : "Edit committed. Save As writes the modified PDF without overwriting the source.";
    }

    private void Undo()
    {
        var op = _history.Undo();
        if (op is null) return;
        var region = Regions.FirstOrDefault(r => r.Model.Id == op.RegionId);
        if (region is not null)
        {
            var effective = _history.EffectiveOperation(op.RegionId);
            region.DisplayText = effective?.NewText ?? region.Model.Text;
            region.DraftText = region.DisplayText;
            region.FontSize = effective?.FontSize ?? region.Model.EstimatedFontSize;
            region.FontFamily = effective?.FontFamily ?? region.Model.FontFamily;
            region.IsBold = effective?.IsBold ?? region.Model.IsBold;
            region.IsItalic = effective?.IsItalic ?? region.Model.IsItalic;
        }
        OnPropertyChanged(nameof(WindowTitle));
        RaiseCommandStates();
    }

    private void Redo()
    {
        var op = _history.Redo();
        if (op is null) return;
        var region = Regions.FirstOrDefault(r => r.Model.Id == op.RegionId);
        if (region is not null)
        {
            var effective = _history.EffectiveOperation(op.RegionId);
            region.DisplayText = effective?.NewText ?? region.Model.Text;
            region.DraftText = region.DisplayText;
            region.FontSize = effective?.FontSize ?? region.Model.EstimatedFontSize;
            region.FontFamily = effective?.FontFamily ?? region.Model.FontFamily;
            region.IsBold = effective?.IsBold ?? region.Model.IsBold;
            region.IsItalic = effective?.IsItalic ?? region.Model.IsItalic;
        }
        OnPropertyChanged(nameof(WindowTitle));
        RaiseCommandStates();
    }

    private async Task PreviousPageAsync()
    {
        if (CurrentPage <= 0) return;
        CurrentPage--;
        await LoadCurrentPageAsync();
    }

    private async Task NextPageAsync()
    {
        if (CurrentPage + 1 >= PageCount) return;
        CurrentPage++;
        await LoadCurrentPageAsync();
    }

    private async Task ChangeZoomAsync(double zoom)
    {
        Zoom = Math.Clamp(zoom, 0.5, 4.0);
        await LoadCurrentPageAsync();
    }

    private void RaiseCommandStates()
    {
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        ApplyPropertiesCommand.RaiseCanExecuteChanged();
        (OpenCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (SaveAsCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (PreviousPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (NextPageCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ZoomInCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        (ZoomOutCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _pageCts?.Cancel();
        _pageCts?.Dispose();
        _pdf?.Dispose();
    }
}
