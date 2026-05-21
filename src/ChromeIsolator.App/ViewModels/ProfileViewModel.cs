using System.Globalization;
using System.Windows.Media;
using ChromeIsolator.Models;
using ChromeIsolator.Services;

namespace ChromeIsolator.ViewModels;

public sealed class ProfileViewModel : ObservableObject
{
    private bool _isRunning;
    private int? _debugPort;
    private string? _error;
    private DateTime? _lastUsed;

    public ProfileViewModel(Profile model)
    {
        Model = model;
    }

    public Profile Model { get; }
    public string Folder => Model.Folder;
    public string ProfilePath => AppPaths.ProfileDir(Model.Folder);

    public string Title
    {
        get
        {
            var defaultName = $"环境{Model.InstanceNumber}";
            return string.IsNullOrWhiteSpace(Model.DisplayName)
                ? defaultName
                : $"{defaultName} - {Model.DisplayName}";
        }
    }

    public string Subtitle => LastUsedText == "-" ? "未使用" : $"最近使用 {LastUsedText}";

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public int? DebugPort
    {
        get => _debugPort;
        set
        {
            if (SetProperty(ref _debugPort, value))
            {
                OnPropertyChanged(nameof(DebugPortText));
            }
        }
    }

    public string? Error
    {
        get => _error;
        set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(ErrorText));
            }
        }
    }

    public DateTime? LastUsed
    {
        get => _lastUsed;
        set
        {
            if (SetProperty(ref _lastUsed, value))
            {
                OnPropertyChanged(nameof(LastUsedText));
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public string StatusText => IsRunning ? "运行中" : "未启动";
    public Brush StatusBrush => IsRunning ? Brushes.ForestGreen : Brushes.Gray;
    public string DebugPortText => DebugPort?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string ErrorText => string.IsNullOrWhiteSpace(Error) ? "-" : Error;
    public string LastUsedText => LastUsed?.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture) ?? "-";

    public void RefreshTitle()
    {
        OnPropertyChanged(nameof(Title));
    }
}
