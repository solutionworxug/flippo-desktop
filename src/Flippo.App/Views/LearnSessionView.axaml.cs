using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Flippo.App.ViewModels;

namespace Flippo.App.Views;

public partial class LearnSessionView : UserControl
{
    private LearnSessionViewModel? _vm;

    public LearnSessionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // Fokus in die View holen, damit die KeyBindings (Leertaste, 1–4, Enter, Strg+Z, Esc) ankommen.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.FocusInputRequested -= FocusInput;

        _vm = DataContext as LearnSessionViewModel;

        if (_vm is not null)
            _vm.FocusInputRequested += FocusInput;
    }

    // Freitext: Fokus in das Eingabefeld, damit man sofort tippen kann.
    private void FocusInput()
        => Dispatcher.UIThread.Post(() =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        }, DispatcherPriority.Background);
}
