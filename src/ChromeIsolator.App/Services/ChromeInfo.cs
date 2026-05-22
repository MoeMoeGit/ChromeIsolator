namespace ChromeIsolator.Services;

public enum BrowserEngineKind
{
    Chrome,
    Edge
}

public sealed record ChromeInfo(BrowserEngineKind Kind, string ExecutablePath, string Source, string? Version);
