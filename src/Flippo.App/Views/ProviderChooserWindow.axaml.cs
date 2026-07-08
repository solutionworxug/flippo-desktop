using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.Cloud.Abstractions;

namespace Flippo.App.Views;

/// <summary>Kleiner Provider-Chooser für „Ziel hinzufügen". Rückgabe null = abgebrochen.</summary>
public partial class ProviderChooserWindow : Window
{
    public ProviderChooserWindow() => InitializeComponent();

    private void OnFolder(object? sender, RoutedEventArgs e) => Close(BackupDestinationKind.LocalFolder);
    private void OnGoogleDrive(object? sender, RoutedEventArgs e) => Close(BackupDestinationKind.GoogleDrive);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
