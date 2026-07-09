using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Flippo.Core.Domain;

namespace Flippo.App.Views;

/// <summary>Lernmodus-Auswahl vor dem Session-Start (Android-Parität). Rückgabe null = abgebrochen.</summary>
public partial class ModeChooserWindow : Window
{
    public ModeChooserWindow() => InitializeComponent();

    private void OnFlashcard(object? sender, RoutedEventArgs e) => Close(LearningMode.Flashcard);
    private void OnFreeText(object? sender, RoutedEventArgs e) => Close(LearningMode.FreeText);
    private void OnMultipleChoice(object? sender, RoutedEventArgs e) => Close(LearningMode.MultipleChoice);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(null); e.Handled = true; }
        base.OnKeyDown(e);
    }
}
