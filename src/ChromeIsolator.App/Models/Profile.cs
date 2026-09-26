namespace ChromeIsolator.Models;

public sealed class Profile
{
    public string Folder { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Note { get; set; } = "";
    public bool EnableEnvironmentVariation { get; set; }
    public bool EnableCollectorDebug { get; set; }
    public DateTime? LastUsed { get; set; }

    public static Profile NewEnvironment(string folder) => new() { Folder = folder, EnableCollectorDebug = true };

    public int InstanceNumber
    {
        get
        {
            if (!string.IsNullOrEmpty(Folder) && Folder.Length > 1 && Folder[0] is 'p' or 'P' &&
                Folder[1..].All(char.IsAsciiDigit) && int.TryParse(Folder[1..], out var value) && value > 0)
            {
                return value;
            }

            return 0;
        }
    }
}
