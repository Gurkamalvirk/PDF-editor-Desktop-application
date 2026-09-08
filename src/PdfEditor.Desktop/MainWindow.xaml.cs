using PdfEditor.Desktop.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PdfEditor.Desktop;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();
    private bool _cancelInlineCommit;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();
    }

    private void RegionBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TextRegionViewModel region) return;
        _viewModel.SelectRegion(region);

        if (e.ClickCount >= 2)
        {
            _viewModel.BeginEdit(region);
            Dispatcher.BeginInvoke(() =>
            {
                var textBox = FindVisualChild<TextBox>((DependencyObject)((FrameworkElement)sender).Parent);
                textBox?.Focus();
                textBox?.SelectAll();
            });
        }
        e.Handled = true;
    }

    private void InlineEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TextRegionViewModel region) return;
        if (e.Key == Key.Escape)
        {
            _cancelInlineCommit = true;
            _viewModel.CancelEdit(region);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _cancelInlineCommit = false;
            _viewModel.CommitEdit(region);
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private void InlineEditor_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not TextRegionViewModel region || !region.IsEditing) return;
        if (_cancelInlineCommit)
        {
            _cancelInlineCommit = false;
            _viewModel.CancelEdit(region);
            return;
        }
        _viewModel.CommitEdit(region);
    }

    private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
        var pdf = files.FirstOrDefault(f => string.Equals(System.IO.Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase));
        if (pdf is not null) await _viewModel.OpenPathAsync(pdf);
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent is null) return null;
        for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typed) return typed;
            var nested = FindVisualChild<T>(child);
            if (nested is not null) return nested;
        }
        return null;
    }
}
