using System.Diagnostics;

namespace CpuRamMonitor.Metrics;

public static class ProcessKiller
{
    /// <summary>
    /// Kills every process with this name in the current session (including their child trees),
    /// looked up at kill time so workers spawned since the last sample are caught too.
    /// The WSL VM can't be killed from user mode, so it gets a clean `wsl --shutdown` instead.
    /// </summary>
    public static string KillByName(string name)
    {
        if (ProcessRules.IsWslVm(name)) return ShutdownWsl();

        int killed = 0, failed = 0;
        string? firstError = null;
        foreach (var p in Process.GetProcessesByName(name))
        {
            using (p)
            {
                try
                {
                    if (p.Id == Environment.ProcessId || p.SessionId != ProcessRules.CurrentSession) continue;
                    p.Kill(entireProcessTree: true);
                    killed++;
                }
                catch (Exception ex)
                {
                    failed++;
                    firstError ??= ex.Message;
                }
            }
        }

        if (killed == 0 && failed == 0) return $"{name}: nenhum processo encontrado";
        if (failed == 0) return $"{name}: {killed} encerrado(s)";
        return $"{name}: {killed} encerrado(s), {failed} falha(s) — {firstError}";
    }

    private static string ShutdownWsl()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("wsl.exe", "--shutdown")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            });
            if (p is null) return "wsl --shutdown: não foi possível iniciar";
            if (!p.WaitForExit(30_000)) return "wsl --shutdown: ainda executando…";
            return p.ExitCode == 0 ? "WSL desligado (wsl --shutdown)" : $"wsl --shutdown saiu com código {p.ExitCode}";
        }
        catch (Exception ex)
        {
            return $"wsl --shutdown falhou — {ex.Message}";
        }
    }
}
