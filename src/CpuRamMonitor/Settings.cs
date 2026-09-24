using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CpuRamMonitor;

public enum WidgetMode { Compact, Expanded }

/// <summary>Window state the app owns and rewrites freely (position, mode, opacity).</summary>
public sealed class AppSettings
{
    public WidgetMode Mode { get; set; } = WidgetMode.Compact;
    public double Opacity { get; set; } = 0.85;
    public double? Left { get; set; }
    public double? Top { get; set; }
}

public static class AppPaths
{
    public static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CpuRamMonitor");

    public static readonly string Settings = Path.Combine(Folder, "settings.json");
    public static readonly string Exclusions = Path.Combine(Folder, "exclusions.txt");
}

public static class SettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.Settings), Json) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.Folder);
            File.WriteAllText(AppPaths.Settings, JsonSerializer.Serialize(settings, Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing a window position is not worth crashing over.
        }
    }
}
