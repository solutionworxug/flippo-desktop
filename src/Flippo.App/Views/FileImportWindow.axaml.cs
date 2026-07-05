using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.App.ViewModels;

namespace Flippo.App.Views;

public partial class FileImportWindow : Window
{
    public FileImportWindow() => InitializeComponent();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        var request = (DataContext as FileImportViewModel)?.BuildRequest();
        Close(request);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
