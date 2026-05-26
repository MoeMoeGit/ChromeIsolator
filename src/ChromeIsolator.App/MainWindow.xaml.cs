using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ChromeIsolator.Services;
using ChromeIsolator.ViewModels;
using WpfApplication = System.Windows.Application;

namespace ChromeIsolator;

public partial class MainWindow : Window
{
    private static readonly TimeSpan PlacementSaveDelay = TimeSpan.FromMilliseconds(250);
    private readonly MainViewModel _viewModel;
    private readonly DispatcherTimer _placementSaveTimer;
    private bool _allowClose;
    private bool _isRestoringPlacement;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        IconHelper.ApplyIcon(this);

        _placementSaveTimer = new DispatcherTimer { Interval = PlacementSaveDelay };
        _placementSaveTimer.Tick += (_, _) =>
        {
            _placementSaveTimer.Stop();
            SaveWindowPlacement();
        };

        LocationChanged += MainWindow_PlacementChanged;
        SizeChanged += MainWindow_PlacementChanged;
        StateChanged += MainWindow_PlacementChanged;
    }

    public void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    public void ApplySavedPlacement()
    {
        _isRestoringPlacement = true;
        try
        {
            RestoreWindowPlacement();
        }
        finally
        {
            _isRestoringPlacement = false;
        }
    }

    public async void ExitFromTray()
    {
        _allowClose = true;
        try
        {
            await _viewModel.StopAllAsync();
            Close();
            WpfApplication.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _allowClose = false;
            System.Windows.MessageBox.Show(ex.Message, Services.L10n.GetString("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _placementSaveTimer.Stop();
        SaveWindowPlacement();

        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private void ProfilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedProfile is not null)
        {
            _viewModel.ToggleProfile(_viewModel.SelectedProfile);
        }
    }

    private void ProfilesList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindParent<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item is not null)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void MainWindow_PlacementChanged(object? sender, EventArgs e)
    {
        if (_isRestoringPlacement || !IsLoaded)
        {
            return;
        }

        _placementSaveTimer.Stop();
        _placementSaveTimer.Start();
    }

    private void GridSplitter_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        SaveWindowPlacement();
    }

    private void RestoreWindowPlacement()
    {
        if (_viewModel.MainWindowWidth is > 0 && _viewModel.MainWindowHeight is > 0)
        {
            var savedWidth = NormalizeDimension(_viewModel.MainWindowWidth, MinWidth, Width);
            var savedHeight = NormalizeDimension(_viewModel.MainWindowHeight, MinHeight, Height);
            var screen = GetVirtualScreenBounds();

            if (screen.Width > 0 && screen.Height > 0)
            {
                savedWidth = Math.Min(savedWidth, screen.Width);
                savedHeight = Math.Min(savedHeight, screen.Height);
            }

            Width = savedWidth;
            Height = savedHeight;

            if (TryClampToScreen(
                    _viewModel.MainWindowLeft,
                    _viewModel.MainWindowTop,
                    savedWidth,
                    savedHeight,
                    screen,
                    out var clampedLeft,
                    out var clampedTop))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = clampedLeft;
                Top = clampedTop;
            }
        }

        var leftPaneWidth = _viewModel.MainWindowLeftPaneWidth;
        if (leftPaneWidth is > 0)
        {
            var clampedLeftPaneWidth = ClampLeftPaneWidth(leftPaneWidth.Value);
            LeftPaneColumn.Width = new GridLength(clampedLeftPaneWidth);
        }

        WindowState = _viewModel.MainWindowIsMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    private void SaveWindowPlacement()
    {
        if (_isRestoringPlacement || !IsLoaded)
        {
            return;
        }

        _viewModel.SaveMainWindowPlacement(this, LeftPaneColumn.ActualWidth);
    }

    private double ClampLeftPaneWidth(double requestedWidth)
    {
        var leftMargin = MainContentGrid.Margin.Left;
        var rightMargin = MainContentGrid.Margin.Right;
        var splitterWidth = PaneSplitter.Width;
        var availableWidth = Math.Max(0, Width - leftMargin - rightMargin - splitterWidth);
        var rightMinWidth = RightPaneColumn.MinWidth;
        var maxLeftWidth = Math.Max(LeftPaneColumn.MinWidth, availableWidth - rightMinWidth);

        if (!double.IsFinite(maxLeftWidth) || maxLeftWidth < LeftPaneColumn.MinWidth)
        {
            maxLeftWidth = LeftPaneColumn.MinWidth;
        }

        return Math.Max(LeftPaneColumn.MinWidth, Math.Min(requestedWidth, maxLeftWidth));
    }

    private static double NormalizeDimension(double? value, double minimum, double fallback)
    {
        if (value is not > 0 || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
        {
            return Math.Max(minimum, fallback);
        }

        return Math.Max(minimum, value.Value);
    }

    private static Rect GetVirtualScreenBounds()
    {
        return new Rect(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
    }

    private static bool TryClampToScreen(
        double? left,
        double? top,
        double width,
        double height,
        Rect screen,
        out double clampedLeft,
        out double clampedTop)
    {
        clampedLeft = 0;
        clampedTop = 0;

        if (!double.IsFinite(screen.Width) || !double.IsFinite(screen.Height) ||
            screen.Width <= 0 || screen.Height <= 0)
        {
            return false;
        }

        var candidateLeft = left ?? screen.Left + (screen.Width - width) / 2;
        var candidateTop = top ?? screen.Top + (screen.Height - height) / 2;

        var maxLeft = screen.Right - width;
        var maxTop = screen.Bottom - height;
        if (maxLeft < screen.Left || maxTop < screen.Top)
        {
            clampedLeft = screen.Left;
            clampedTop = screen.Top;
            return true;
        }

        clampedLeft = Math.Max(screen.Left, Math.Min(candidateLeft, maxLeft));
        clampedTop = Math.Max(screen.Top, Math.Min(candidateTop, maxTop));
        return true;
    }

    private static T? FindParent<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
