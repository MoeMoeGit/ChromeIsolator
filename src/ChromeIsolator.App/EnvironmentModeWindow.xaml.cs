using System.Windows;
using ChromeIsolator.Services;
using ChromeIsolator.ViewModels;

namespace ChromeIsolator;

public partial class EnvironmentModeWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public EnvironmentModeWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        IconHelper.ApplyIcon(this);
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _viewModel.RefreshProfileModeStates();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
