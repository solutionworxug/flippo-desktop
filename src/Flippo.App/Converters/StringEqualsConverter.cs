using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Flippo.App.Converters;

/// <summary>
/// Vergleicht den gebundenen Wert mit <c>ConverterParameter</c> auf String-Gleichheit.
/// Präsentations-Infrastruktur (z. B. Sidebar-Aktiv-Hervorhebung), kein Domänen-Code.
/// Nutzung: <c>Classes.nav-active="{Binding ActiveNav, Converter={x:Static conv:StringEqualsConverter.Instance}, ConverterParameter=Dashboard}"</c>
/// </summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public static readonly StringEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() == parameter?.ToString();

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
