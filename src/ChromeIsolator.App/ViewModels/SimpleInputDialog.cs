using System.Windows;
using ChromeIsolator.Services;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfApplication = System.Windows.Application;
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
            MinWidth = 300,
            FontSize = 13,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 12, 0, 0),
            VerticalContentAlignment = VerticalAlignment.Center
        };

        var okButton = new WpfButton
        {
            Content = L10n.GetString("BtnOk"),
            IsDefault = true,
            MinWidth = 80,
            Margin = new Thickness(8, 0, 0, 0)
        };
        var cancelButton = new WpfButton
        {
            Content = L10n.GetString("BtnCancel"),
            IsCancel = true,
            MinWidth = 80
        };

        // Apply styles from theme resources if available
        var app = WpfApplication.Current;
        if (app?.Resources.Contains("PrimaryButton") == true)
        {
            okButton.Style = (Style)app.Resources["PrimaryButton"];
        }
        if (app?.Resources.Contains("SecondaryButton") == true)
        {
            cancelButton.Style = (Style)app.Resources["SecondaryButton"];
        }

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

        var messageBlock = new WpfTextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = app?.Resources.Contains("TextPrimaryBrush") == true
                ? (MediaBrush)app.Resources["TextPrimaryBrush"]
                : MediaBrushes.Black
        };

        var content = new WpfStackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                messageBlock,
                input,
                buttonPanel
            }
        };

        var window = new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = content,
            Background = app?.Resources.Contains("WindowBackgroundBrush") == true
                ? (MediaBrush)app.Resources["WindowBackgroundBrush"]
                : MediaBrushes.White
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
