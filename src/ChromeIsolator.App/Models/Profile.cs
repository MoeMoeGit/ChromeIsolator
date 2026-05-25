namespace ChromeIsolator.Models;

public sealed class Profile
{
    public string Folder { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Note { get; set; } = "";
    public bool EnableEnvironmentVariation { get; set; }
    public DateTime? LastUsed { get; set; }

    public int InstanceNumber
    {
        get
        {
            if (Folder.Length > 1 && int.TryParse(Folder[1..], out var value))
            {
                return value;
            }

            return 0;
        }
    }
}
