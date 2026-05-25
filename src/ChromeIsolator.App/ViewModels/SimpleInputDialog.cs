using System.Windows;
using ChromeIsolator.Services;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfApplication = System.Windows.Application;
using WpfButton = System.Windows.Controls.Button;
using WpfClipboard = System.Windows.Clipboard;
using WpfDockPanel = System.Windows.Controls.DockPanel;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfStackPanel = System.Windows.Controls.StackPanel;
using WpfTextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace ChromeIsolator.ViewModels;

public static class SimpleInputDialog
{
    public static string? Show(string title, string message, string initialValue, int? maxLength = null, bool multiline = false)
    {
        var input = new WpfTextBox
        {
            Text = initialValue,
            MinWidth = 340,
            MaxWidth = 360,
            FontSize = 13,
            Margin = new Thickness(0, 14, 0, 0),
            VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            AcceptsReturn = multiline,
            MinHeight = multiline ? 92 : 0,
            MaxHeight = multiline ? 120 : double.PositiveInfinity,
            VerticalScrollBarVisibility = multiline ? System.Windows.Controls.ScrollBarVisibility.Auto : System.Windows.Controls.ScrollBarVisibility.Disabled
        };
        if (maxLength is not null)
        {
            input.MaxLength = maxLength.Value;
        }

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
            Margin = new Thickness(0, 20, 0, 0),
            Children =
            {
                cancelButton,
                okButton
            }
        };

        var titleBlock = new WpfTextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = app?.Resources.Contains("TextPrimaryBrush") == true
                ? (MediaBrush)app.Resources["TextPrimaryBrush"]
                : MediaBrushes.Black,
            TextWrapping = TextWrapping.Wrap
        };

        var messageBlock = new WpfTextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(0, 10, 0, 0),
            Foreground = app?.Resources.Contains("TextSecondaryBrush") == true
                ? (MediaBrush)app.Resources["TextSecondaryBrush"]
                : MediaBrushes.DimGray
        };

        var iconBlock = new WpfTextBlock
        {
            Text = title == L10n.GetString("MsgDeleteTitle") ? "\uE74D" : "\uE70F",
            FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons"),
            FontSize = 18,
            Width = 38,
            Height = 38,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0, 9, 0, 0),
            Foreground = title == L10n.GetString("MsgDeleteTitle")
                ? (app?.Resources.Contains("ErrorBrush") == true ? (MediaBrush)app.Resources["ErrorBrush"] : MediaBrushes.DarkRed)
                : (app?.Resources.Contains("AccentBrush") == true ? (MediaBrush)app.Resources["AccentBrush"] : MediaBrushes.RoyalBlue)
        };

        var textPanel = new WpfStackPanel
        {
            Margin = new Thickness(14, 0, 0, 0),
            Children =
            {
                titleBlock,
                messageBlock,
                input,
                buttonPanel
            }
        };

        var body = new WpfDockPanel
        {
            Children =
            {
                iconBlock,
                textPanel
            }
        };
        WpfDockPanel.SetDock(iconBlock, System.Windows.Controls.Dock.Left);

        var content = new System.Windows.Controls.Border
        {
            Margin = new Thickness(18),
            Padding = new Thickness(18),
            CornerRadius = app?.Resources.Contains("CardCornerRadius") == true
                ? (CornerRadius)app.Resources["CardCornerRadius"]
                : new CornerRadius(8),
            Background = app?.Resources.Contains("CardBackgroundBrush") == true
                ? (MediaBrush)app.Resources["CardBackgroundBrush"]
                : MediaBrushes.White,
            BorderBrush = app?.Resources.Contains("BorderBrush") == true
                ? (MediaBrush)app.Resources["BorderBrush"]
                : MediaBrushes.LightGray,
            BorderThickness = new Thickness(1),
            Child = body
        };

        var owner = WpfApplication.Current.MainWindow?.IsVisible == true ? WpfApplication.Current.MainWindow : null;
        var window = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            MaxHeight = 520,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = content,
            Owner = owner,
            Background = app?.Resources.Contains("WindowBackgroundBrush") == true
                ? (MediaBrush)app.Resources["WindowBackgroundBrush"]
                : MediaBrushes.White
        };
        IconHelper.ApplyIcon(window);

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

    public static void ShowCopyMessage(string title, string message, string copyText)
    {
        var app = WpfApplication.Current;
        var copyButton = new WpfButton
        {
            Content = L10n.GetString("BtnCopyLink"),
            MinWidth = 90,
            Margin = new Thickness(8, 0, 0, 0)
        };
        var closeButton = new WpfButton
        {
            Content = L10n.GetString("BtnClose"),
            IsCancel = true,
            MinWidth = 80
        };

        if (app?.Resources.Contains("PrimaryButton") == true)
        {
            copyButton.Style = (Style)app.Resources["PrimaryButton"];
        }
        if (app?.Resources.Contains("SecondaryButton") == true)
        {
            closeButton.Style = (Style)app.Resources["SecondaryButton"];
        }

        var buttonPanel = new WpfStackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Right,
            Margin = new Thickness(0, 20, 0, 0),
            Children =
            {
                closeButton,
                copyButton
            }
        };

        var titleBlock = new WpfTextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = app?.Resources.Contains("TextPrimaryBrush") == true
                ? (MediaBrush)app.Resources["TextPrimaryBrush"]
                : MediaBrushes.Black,
            TextWrapping = TextWrapping.Wrap
        };

        var messageBlock = new WpfTextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Margin = new Thickness(0, 10, 0, 0),
            Foreground = app?.Resources.Contains("TextSecondaryBrush") == true
                ? (MediaBrush)app.Resources["TextSecondaryBrush"]
                : MediaBrushes.DimGray
        };

        var iconBlock = new WpfTextBlock
        {
            Text = "\uE7BA",
            FontFamily = new System.Windows.Media.FontFamily("Segoe Fluent Icons"),
            FontSize = 18,
            Width = 38,
            Height = 38,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0, 9, 0, 0),
            Foreground = app?.Resources.Contains("AccentBrush") == true
                ? (MediaBrush)app.Resources["AccentBrush"]
                : MediaBrushes.RoyalBlue
        };

        var textPanel = new WpfStackPanel
        {
            Margin = new Thickness(14, 0, 0, 0),
            Children =
            {
                titleBlock,
                messageBlock,
                buttonPanel
            }
        };

        var body = new WpfDockPanel
        {
            Children =
            {
                iconBlock,
                textPanel
            }
        };
        WpfDockPanel.SetDock(iconBlock, System.Windows.Controls.Dock.Left);

        var content = new System.Windows.Controls.Border
        {
            Margin = new Thickness(18),
            Padding = new Thickness(18),
            CornerRadius = app?.Resources.Contains("CardCornerRadius") == true
                ? (CornerRadius)app.Resources["CardCornerRadius"]
                : new CornerRadius(8),
            Background = app?.Resources.Contains("CardBackgroundBrush") == true
                ? (MediaBrush)app.Resources["CardBackgroundBrush"]
                : MediaBrushes.White,
            BorderBrush = app?.Resources.Contains("BorderBrush") == true
                ? (MediaBrush)app.Resources["BorderBrush"]
                : MediaBrushes.LightGray,
            BorderThickness = new Thickness(1),
            Child = body
        };

        var owner = WpfApplication.Current.MainWindow?.IsVisible == true ? WpfApplication.Current.MainWindow : null;
        var window = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Content = content,
            Owner = owner,
            Background = app?.Resources.Contains("WindowBackgroundBrush") == true
                ? (MediaBrush)app.Resources["WindowBackgroundBrush"]
                : MediaBrushes.White
        };
        IconHelper.ApplyIcon(window);

        copyButton.Click += (_, _) =>
        {
            WpfClipboard.SetText(copyText);
            window.DialogResult = true;
        };

        window.ShowDialog();
    }
}
