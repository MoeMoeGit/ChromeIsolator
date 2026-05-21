namespace ChromeIsolator.Models;

public sealed class AppConfig
{
    public List<Profile> Profiles { get; set; } =
    [
        new() { Folder = "p1" },
        new() { Folder = "p2" },
        new() { Folder = "p3" }
    ];
}
