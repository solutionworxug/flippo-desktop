using Avalonia.Markup.Xaml;

namespace Flippo.App.Localization;

/// <summary>
/// XAML-Markup-Extension für lokalisierte Strings: <c>Text="{loc:T SetsTitle}"</c>.
/// Löst zur Ladezeit gegen die aktuelle UI-Sprache auf (Sprachwechsel wirkt nach Neustart).
/// </summary>
public sealed class TExtension : MarkupExtension
{
    public TExtension() { }
    public TExtension(string key) => Key = key;

    public string Key { get; set; } = "";

    public override object ProvideValue(IServiceProvider serviceProvider) => L.T(Key);
}
