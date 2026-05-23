using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ChromeIsolator.Services;

public sealed class UpdateService
{
    public const string ReleasesUrl = "https://github.com/MoeMoeGit/ChromeIsolator/releases";
    public const string LatestReleaseUrl = "https://github.com/MoeMoeGit/ChromeIsolator/releases/latest";
    public const string IssuesUrl = "https://github.com/MoeMoeGit/ChromeIsolator/issues";
    private const string LatestReleaseApi = "https://api.github.com/repos/MoeMoeGit/ChromeIsolator/releases/latest";

    private static readonly HttpClient HttpClient = new();

    public string CurrentVersion
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            var normalized = NormalizeVersion(informationalVersion);
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                return normalized;
            }

            var version = assembly.GetName().Version;
            return version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.UserAgent.ParseAdd($"ChromeIsolator/{CurrentVersion}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await HttpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return await CheckForUpdatesFromReleaseRedirectAsync($"HTTP {(int)response.StatusCode}", cancellationToken);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (!json.RootElement.TryGetProperty("tag_name", out var tagElement))
            {
                return new UpdateCheckResult(UpdateCheckStatus.Failed, ErrorMessage: "无法解析版本信息");
            }

            var latestTag = tagElement.GetString();
            if (string.IsNullOrWhiteSpace(latestTag))
            {
                return new UpdateCheckResult(UpdateCheckStatus.Failed, ErrorMessage: "最新版本为空");
            }

            var latestVersion = NormalizeVersion(latestTag) ?? latestTag.TrimStart('v', 'V');
            var current = CurrentVersion;
            return IsNewer(latestVersion, current)
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, latestTag)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate, latestTag);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new UpdateCheckResult(UpdateCheckStatus.Failed, ErrorMessage: ex.Message);
        }
    }

    private async Task<UpdateCheckResult> CheckForUpdatesFromReleaseRedirectAsync(string apiError, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd($"ChromeIsolator/{CurrentVersion}");

            using var response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateCheckResult(UpdateCheckStatus.Failed, ErrorMessage: $"{apiError}; fallback HTTP {(int)response.StatusCode}");
            }

            var finalUri = response.RequestMessage?.RequestUri?.ToString();
            var latestTag = finalUri?
                .Split('/', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault();
            if (string.IsNullOrWhiteSpace(latestTag) ||
                string.Equals(latestTag, "latest", StringComparison.OrdinalIgnoreCase))
            {
                return new UpdateCheckResult(UpdateCheckStatus.Failed, ErrorMessage: apiError);
            }

            var latestVersion = NormalizeVersion(latestTag) ?? latestTag.TrimStart('v', 'V');
            var current = CurrentVersion;
            return IsNewer(latestVersion, current)
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, latestTag)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate, latestTag);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new UpdateCheckResult(UpdateCheckStatus.Failed, ErrorMessage: apiError);
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        if (Version.TryParse(latest, out var latestVersion) &&
            Version.TryParse(current, out var currentVersion))
        {
            return latestVersion > currentVersion;
        }

        return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static string? NormalizeVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var match = Regex.Match(value.Trim(), @"v?(?<version>\d+\.\d+\.\d+)");
        return match.Success ? match.Groups["version"].Value : null;
    }
}
