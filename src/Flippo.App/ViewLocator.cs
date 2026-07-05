using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Flippo.App.ViewModels;

namespace Flippo.App;

/// <summary>
/// Konventions-basiertes VM→View-Mapping: Namespace ".ViewModels." → ".Views.",
/// Typ-Suffix "ViewModel" → "View". Registriert in App.axaml als DataTemplate.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "View nicht gefunden: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
