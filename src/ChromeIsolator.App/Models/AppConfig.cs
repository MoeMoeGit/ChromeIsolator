namespace ChromeIsolator.Models;

public sealed class AppConfig
{
    public List<Profile> Profiles { get; set; } =
    [
        new() { Folder = "p1" },
        new() { Folder = "p2" },
        new() { Folder = "p3" }
    ];

    public bool ShowAdvancedDetails { get; set; }
    public string? Language { get; set; }
    public bool FirstRunCompleted { get; set; }
    public bool AllowEdgeFallback { get; set; }
    public string? ExternalLinkProfileFolder { get; set; }
    public double? MainWindowLeft { get; set; }
    public double? MainWindowTop { get; set; }
    public double? MainWindowWidth { get; set; }
    public double? MainWindowHeight { get; set; }
    public bool MainWindowIsMaximized { get; set; }
    public double? MainWindowLeftPaneWidth { get; set; }
}
