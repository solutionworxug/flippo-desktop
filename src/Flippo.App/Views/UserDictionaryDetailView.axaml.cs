using Avalonia.Controls;
using Avalonia.Input;
using Flippo.App.ViewModels;

namespace Flippo.App.Views;

public partial class UserDictionaryDetailView : UserControl
{
    public UserDictionaryDetailView() => InitializeComponent();

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
        => (DataContext as UserDictionaryDetailViewModel)?.EditEntryCommand.Execute(null);
}
