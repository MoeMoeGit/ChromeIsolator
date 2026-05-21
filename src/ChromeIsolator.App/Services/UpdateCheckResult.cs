namespace ChromeIsolator.Services;

public enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? LatestVersion = null, string? ErrorMessage = null);
