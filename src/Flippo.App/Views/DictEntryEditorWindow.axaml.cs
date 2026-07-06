using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.App.ViewModels;

namespace Flippo.App.Views;

public partial class DictEntryEditorWindow : Window
{
    public DictEntryEditorWindow() => InitializeComponent();

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DictEntryEditorViewModel vm || !vm.CanSave) return;
        Close(vm.Build());
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
