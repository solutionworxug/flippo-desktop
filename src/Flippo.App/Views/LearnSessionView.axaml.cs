using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Flippo.App.Views;

public partial class LearnSessionView : UserControl
{
    public LearnSessionView()
    {
        InitializeComponent();
    }

    // Fokus in die View holen, damit die KeyBindings (Leertaste, 1–4, Strg+Z, Esc) ankommen (vgl. SetDetailView).
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }
}
