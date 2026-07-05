using Flippo.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Flippo.App.Services;

/// <summary>Seiten, die beim Anzeigen (asynchron) Daten laden.</summary>
public interface IActivatable
{
    Task OnActivatedAsync();
}

/// <summary>
/// VM-first-Navigation mit Back-Stack. Einzige Quelle der Wahrheit für die aktuelle Seite;
/// das <see cref="MainWindowViewModel"/> spiegelt <see cref="Current"/> in eine bindbare Property.
/// </summary>
public sealed class NavigationService
{
    private readonly Stack<ViewModelBase> _back = new();
    private readonly IServiceProvider _services;

    public NavigationService(IServiceProvider services) => _services = services;

    public ViewModelBase? Current { get; private set; }
    public bool CanGoBack => _back.Count > 0;
    public event Action? Navigated;

    public void NavigateTo(ViewModelBase page, bool clearStack = false)
    {
        if (clearStack)
            _back.Clear();
        else if (Current is not null)
            _back.Push(Current);

        Current = page;
        Navigated?.Invoke();
        Activate(page);
    }

    /// <summary>Löst die Seite aus dem DI-Container auf, optional konfiguriert (z.B. Set-Id setzen).</summary>
    public T NavigateTo<T>(Action<T>? configure = null, bool clearStack = false) where T : ViewModelBase
    {
        var page = _services.GetRequiredService<T>();
        configure?.Invoke(page);
        NavigateTo(page, clearStack);
        return page;
    }

    public void GoBack()
    {
        if (_back.Count == 0) return;
        Current = _back.Pop();
        Navigated?.Invoke();
        Activate(Current);
    }

    private static void Activate(ViewModelBase page)
    {
        if (page is IActivatable activatable)
            _ = activatable.OnActivatedAsync();
    }
}
