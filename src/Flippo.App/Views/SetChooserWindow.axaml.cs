using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Flippo.Core.Domain;

namespace Flippo.App.Views;

/// <summary>Kleiner Auswahl-Dialog: Ziel-Kartei für „Als Karte übernehmen". Rückgabe null = abgebrochen.</summary>
public partial class SetChooserWindow : Window
{
    public SetChooserWindow() => InitializeComponent();

    public SetChooserWindow(IReadOnlyList<VocabularySet> sets) : this()
    {
        SetsList.ItemsSource = sets;
        if (sets.Count > 0) SetsList.SelectedIndex = 0;
    }

    private void OnChoose(object? sender, RoutedEventArgs e)
    {
        if (SetsList.SelectedItem is VocabularySet set) Close(set);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
