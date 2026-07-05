using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Flippo.App.Views;

public partial class SessionSummaryView : UserControl
{
    public SessionSummaryView()
    {
        InitializeComponent();
    }

    // Fokus holen, damit Enter/R als KeyBindings ankommen (vgl. LearnSessionView).
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => Focus(), DispatcherPriority.Loaded);
    }
}
