using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Flippo.App.ViewModels;

namespace Flippo.App.Views;

public partial class SetDetailView : UserControl
{
    private SetDetailViewModel? _vm;

    public SetDetailView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.FocusSourceRequested -= FocusSource;

        _vm = DataContext as SetDetailViewModel;

        if (_vm is not null)
            _vm.FocusSourceRequested += FocusSource;
    }

    /// <summary>Fokus zurück auf das Quelle-Feld — nach Layout, damit das Panel sichtbar ist.</summary>
    private void FocusSource()
    {
        Dispatcher.UIThread.Post(() =>
        {
            SourceBox.Focus();
            SourceBox.SelectAll();
        }, DispatcherPriority.Background);
    }

    // Entf/Enter nur, wenn das DataGrid den Fokus hat (nicht beim Tippen in Textfeldern).
    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not SetDetailViewModel vm) return;

        switch (e.Key)
        {
            case Key.Delete:
                vm.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter:
                vm.EditSelectedCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
        => (DataContext as SetDetailViewModel)?.EditSelectedCommand.Execute(null);
}
