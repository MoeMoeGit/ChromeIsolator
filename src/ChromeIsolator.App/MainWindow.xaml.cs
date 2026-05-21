using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using ChromeIsolator.ViewModels;

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
    }

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    public void ExitFromTray()
    {
        _allowClose = true;
        _viewModel.StopAll();
        Close();
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
        _viewModel.StartSelected();
    }
}
