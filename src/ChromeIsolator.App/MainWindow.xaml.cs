using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ChromeIsolator.Services;
using ChromeIsolator.ViewModels;
using WpfApplication = System.Windows.Application;

namespace ChromeIsolator;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        IconHelper.ApplyIcon(this);
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public async void ExitFromTray()
    {
        _allowClose = true;
        await _viewModel.StopAllAsync();
        Close();
        WpfApplication.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
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
}
