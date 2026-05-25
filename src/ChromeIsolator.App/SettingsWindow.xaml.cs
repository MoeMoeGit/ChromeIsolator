using System.Windows;
using ChromeIsolator.Services;
using ChromeIsolator.ViewModels;

namespace ChromeIsolator;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
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

    protected override void OnClosed(EventArgs e)
    {
        (_viewModel as IDisposable)?.Dispose();
        base.OnClosed(e);
    }
}
