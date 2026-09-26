using System.IO;
using Microsoft.Win32;

namespace CpuRamMonitor;

/// <summary>Per-user autostart via HKCU\...\Run (no admin needed; also shows up in Task Manager → Startup apps).</summary>
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CpuRamMonitor";

    /// <summary>
    /// Registers or removes the Run entry. When enabled it always rewrites the path, so the entry
    /// follows the exe if it's moved (the last exe launched wins).
    /// </summary>
    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled && Environment.ProcessPath is { } exe)
                key.SetValue(ValueName, $"\"{exe}\"");
            else
                key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // Autostart is a convenience; never block the widget over it.
        }
    }
}
