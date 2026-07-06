using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.App.ViewModels;

namespace Flippo.App.Views;

public partial class DictionaryEditorWindow : Window
{
    public DictionaryEditorWindow() => InitializeComponent();

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DictionaryEditorViewModel vm || !vm.CanSave) return;
        Close(vm.Build(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
