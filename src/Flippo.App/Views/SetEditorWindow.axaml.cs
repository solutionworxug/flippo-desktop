using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.App.ViewModels;

namespace Flippo.App.Views;

public partial class SetEditorWindow : Window
{
    public SetEditorWindow() => InitializeComponent();

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SetEditorViewModel vm || string.IsNullOrWhiteSpace(vm.Title))
            return;
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Close(vm.Build(now));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
