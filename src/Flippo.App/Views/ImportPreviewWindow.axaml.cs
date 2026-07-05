using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.App.Services;
using Flippo.App.ViewModels;

namespace Flippo.App.Views;

public partial class ImportPreviewWindow : Window
{
    public ImportPreviewWindow() => InitializeComponent();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var applySettings = (DataContext as ImportPreviewViewModel)?.ApplySettings ?? false;
        Close(new ImportConfirmation(applySettings));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
