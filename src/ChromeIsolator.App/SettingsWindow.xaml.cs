using System.Windows;
using ChromeIsolator.ViewModels;

namespace ChromeIsolator;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
