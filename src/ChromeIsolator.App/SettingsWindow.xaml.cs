using System.Windows;
using ChromeIsolator.Services;
using ChromeIsolator.ViewModels;

namespace ChromeIsolator;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        IconHelper.ApplyIcon(this);
    }
}
