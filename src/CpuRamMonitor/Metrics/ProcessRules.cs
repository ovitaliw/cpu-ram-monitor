using System.Diagnostics;

namespace CpuRamMonitor.Metrics;

/// <summary>Which processes count as "user heavy users" worth showing.</summary>
public static class ProcessRules
{
    public static readonly int CurrentSession = GetCurrentSession();

    // Killing any of these breaks the desktop session; never list them.
    private static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Registry", "Memory Compression", "Secure System", "smss", "csrss", "wininit",
        "winlogon", "services", "lsass", "svchost", "dwm", "explorer", "sihost", "fontdrvhost",
        "ctfmon", "LogonUI", "CpuRamMonitor",
    };

    /// <summary>WSL2 / Docker Desktop / k3d all run inside this VM process (session 0).</summary>
    public static bool IsWslVm(string name) =>
        name.Equals("VmmemWSL", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("vmmem", StringComparison.OrdinalIgnoreCase);

    public static bool IsUserProcess(ProcessSample s) =>
        s.Pid != Environment.ProcessId &&
        !Critical.Contains(s.Name) &&
        (s.SessionId == CurrentSession || IsWslVm(s.Name));

    /// <summary>"Node.EXE " → "Node"</summary>
    public static string NormalizeName(string name)
    {
        name = name.Trim();
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private static int GetCurrentSession()
    {
        using var self = Process.GetCurrentProcess();
        return self.SessionId;
    }
}
