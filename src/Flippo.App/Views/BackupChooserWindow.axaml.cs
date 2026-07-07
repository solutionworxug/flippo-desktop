using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.Cloud.Abstractions;

namespace Flippo.App.Views;

/// <summary>Auswahl eines vorhandenen Backups am Ziel. Rückgabe null = abgebrochen.</summary>
public partial class BackupChooserWindow : Window
{
    public BackupChooserWindow() => InitializeComponent();

    public BackupChooserWindow(IReadOnlyList<BackupFileInfo> backups) : this()
    {
        BackupList.ItemsSource = backups;
        if (backups.Count > 0) BackupList.SelectedIndex = 0;
    }

    private void OnChoose(object? sender, RoutedEventArgs e)
    {
        if (BackupList.SelectedItem is BackupFileInfo info) Close(info);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
