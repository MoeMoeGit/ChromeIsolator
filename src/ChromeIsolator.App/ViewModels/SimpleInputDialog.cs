using System.Windows;
using System.Windows.Controls;

namespace ChromeIsolator.ViewModels;

public static class SimpleInputDialog
{
    public static string? Show(string title, string message, string initialValue)
    {
        var input = new TextBox
        {
            Text = initialValue,
            MinWidth = 280,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var okButton = new Button
        {
            Content = "确定",
            IsDefault = true,
            MinWidth = 76,
            Margin = new Thickness(8, 0, 0, 0)
        };
        var cancelButton = new Button
        {
            Content = "取消",
            IsCancel = true,
            MinWidth = 76
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
            Children =
            {
                cancelButton,
                okButton
            }
        };

        var content = new StackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap
                },
                input,
                buttonPanel
            }
        };

        var window = new Window
        {
            Title = title,
            Width = 380,
            Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = content
        };

        string? result = null;
        okButton.Click += (_, _) =>
        {
            result = input.Text;
            window.DialogResult = true;
        };

        input.SelectAll();
        input.Focus();
        window.ShowDialog();
        return result;
    }
}
