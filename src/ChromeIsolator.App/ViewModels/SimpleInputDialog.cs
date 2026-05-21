using System.Windows;
using ChromeIsolator.Services;
using WpfButton = System.Windows.Controls.Button;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace ChromeIsolator.ViewModels;

public static class SimpleInputDialog
{
    public static string? Show(string title, string message, string initialValue)
    {
        var input = new WpfTextBox
        {
            Text = initialValue,
            MinWidth = 280,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var okButton = new WpfButton
        {
            Content = L10n.GetString("BtnOk"),
            IsDefault = true,
            MinWidth = 76,
            Margin = new Thickness(8, 0, 0, 0)
        };
        var cancelButton = new WpfButton
        {
            Content = L10n.GetString("BtnCancel"),
            IsCancel = true,
            MinWidth = 76
        };

        var buttonPanel = new WpfStackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
            Children =
            {
                cancelButton,
                okButton
            }
        };

        var content = new WpfStackPanel
        {
            Margin = new Thickness(18),
            Children =
            {
                new WpfTextBlock
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
