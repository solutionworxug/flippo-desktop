using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Flippo.App.Views;

public partial class ThemeSetPickerWindow : Window
{
    public ThemeSetPickerWindow() => InitializeComponent();

    // Ergebnis (ob importiert wurde) liest der Aufrufer aus dem ViewModel (AnyImported).
    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
