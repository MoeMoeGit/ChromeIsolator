using System.Globalization;
using ChromeIsolator.Models;
using ChromeIsolator.Services;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace ChromeIsolator.ViewModels;

public sealed class ProfileViewModel : ObservableObject
{
    private bool _isRunning;
    private bool _isStarting;
    private bool _isStopping;
    private int? _debugPort;
    private string? _error;
    private DateTime? _lastUsed;
    private long _diskSizeBytes;

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
            var defaultName = string.Format(L10n.GetString("LabelFolder") == "Folder" ? "Profile {0}" : "环境{0}", Model.InstanceNumber);
            return string.IsNullOrWhiteSpace(Model.DisplayName)
                ? defaultName
                : $"{defaultName} - {Model.DisplayName}";
        }
    }

    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (LastUsedText != "-") parts.Add(L10n.Format("StatusRecentUsed", LastUsedText));
            if (DiskSizeText != "-") parts.Add(DiskSizeText);
            return parts.Count > 0 ? string.Join(" · ", parts) : L10n.GetString("StatusNotUsed");
        }
    }

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

    public bool IsStarting
    {
        get => _isStarting;
        set
        {
            if (SetProperty(ref _isStarting, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public bool IsStopping
    {
        get => _isStopping;
        set
        {
            if (SetProperty(ref _isStopping, value))
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

    public long DiskSizeBytes
    {
        get => _diskSizeBytes;
        set
        {
            if (SetProperty(ref _diskSizeBytes, value))
            {
                OnPropertyChanged(nameof(DiskSizeText));
                OnPropertyChanged(nameof(Subtitle));
            }
        }
    }

    public string StatusText
    {
        get
        {
            if (_isStarting) return L10n.GetString("StatusStarting");
            if (_isStopping) return L10n.GetString("StatusStopping");
            return _isRunning ? L10n.GetString("StatusRunning") : L10n.GetString("StatusStopped");
        }
    }

    public MediaBrush StatusBrush
    {
        get
        {
            if (_isStarting || _isStopping) return MediaBrushes.Orange;
            return _isRunning ? MediaBrushes.ForestGreen : MediaBrushes.Gray;
        }
    }

    public string DebugPortText => DebugPort?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public string ErrorText => string.IsNullOrWhiteSpace(Error) ? "-" : Error;

    public string LastUsedText
    {
        get
        {
            if (_lastUsed is null) return "-";
            var date = _lastUsed.Value;
            var now = DateTime.Now;
            var span = now - date;

            if (span.TotalMinutes < 1) return L10n.GetString("DateJustNow");
            if (span.TotalHours < 1) return L10n.Format("DateMinutesAgo", (int)span.TotalMinutes);
            if (date.Date == now.Date) return L10n.GetString("DateToday");
            if (date.Date == now.Date.AddDays(-1)) return L10n.GetString("DateYesterday");
            if (span.TotalDays < 7) return L10n.Format("DateDaysAgo", (int)span.TotalDays);
            if (date.Year == now.Year) return date.ToString("MM/dd", CultureInfo.CurrentCulture);
            return date.ToString("yyyy-MM-dd", CultureInfo.CurrentCulture);
        }
    }

    public string DiskSizeText
    {
        get
        {
            if (_diskSizeBytes <= 0) return "-";
            if (_diskSizeBytes < 1024 * 1024) return $"{_diskSizeBytes / 1024.0:F0} KB";
            if (_diskSizeBytes < 1024L * 1024 * 1024) return $"{_diskSizeBytes / (1024.0 * 1024.0):F1} MB";
            return $"{_diskSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    public string DiskSizeRaw
    {
        get
        {
            if (_diskSizeBytes <= 0) return "0 B";
            if (_diskSizeBytes < 1024) return $"{_diskSizeBytes} B";
            if (_diskSizeBytes < 1024 * 1024) return $"{_diskSizeBytes / 1024.0:F0} KB";
            if (_diskSizeBytes < 1024L * 1024 * 1024) return $"{_diskSizeBytes / (1024.0 * 1024.0):F1} MB";
            return $"{_diskSizeBytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    public void RefreshTitle()
    {
        OnPropertyChanged(nameof(Title));
    }

    public void RefreshDiskSize()
    {
        var dir = AppPaths.ProfileDir(Model.Folder);
        DiskSizeBytes = Directory.Exists(dir) ? GetDirectorySize(dir) : 0;
    }

    public static async Task<long> GetDirectorySizeAsync(string path)
    {
        return await Task.Run(() =>
        {
            long size = 0;
            try
            {
                foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                {
                    try { size += new FileInfo(file).Length; }
                    catch { /* skip inaccessible files */ }
                }
            }
            catch { /* skip inaccessible directories */ }
            return size;
        });
    }

    public async Task RefreshDiskSizeAsync()
    {
        var dir = AppPaths.ProfileDir(Model.Folder);
        DiskSizeBytes = Directory.Exists(dir) ? await GetDirectorySizeAsync(dir) : 0;
    }

    public void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(LastUsedText));
    }

    private static long GetDirectorySize(string path)
    {
        long size = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
            {
                try { size += new FileInfo(file).Length; }
                catch { /* skip inaccessible files */ }
            }
        }
        catch { /* skip inaccessible directories */ }
        return size;
    }
}
