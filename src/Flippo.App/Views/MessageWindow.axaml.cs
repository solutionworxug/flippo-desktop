using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Flippo.App.Views;

public partial class MessageWindow : Window
{
    public MessageWindow() => InitializeComponent();

    public MessageWindow(string title, string message) : this()
    {
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close();
}
