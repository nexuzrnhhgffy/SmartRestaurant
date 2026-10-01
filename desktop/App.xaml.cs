using System.IO;
using System.Text.Json;
using System.Windows;
using SmartRestaurant.Desktop.Core;

namespace SmartRestaurant.Desktop;

public partial class App : Application
{
    public static ApiClient Api { get; private set; } = null!;
    public static RealtimeService Realtime { get; private set; } = null!;
    public static AudioService Audio { get; private set; } = null!;
    public static DesktopSettings Settings { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = DesktopSettings.Load();
        Api = new ApiClient(Settings);
        Realtime = new RealtimeService(Settings);
        Audio = new AudioService();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Settings.Save();
        base.OnExit(e);
    }
}

/// <summary>Persisted desktop settings (server URL etc.) under %AppData%/Zafaran.</summary>
public class DesktopSettings
{
    public string ServerUrl { get; set; } = "http://localhost:5000";
    public string? LastUserName { get; set; }
    public bool KdsSound { get; set; } = true;
    public string? KdsStation { get; set; }

    public static string FilePath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zafaran");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "desktop-settings.json");
        }
    }

    public static DesktopSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(FilePath)) ?? new DesktopSettings();
        }
        catch { }
        return new DesktopSettings();
    }

    public void Save()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }
}
