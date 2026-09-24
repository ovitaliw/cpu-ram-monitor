using System.Runtime.InteropServices;

namespace CpuRamMonitor.Metrics;

public readonly record struct SystemSnapshot(double CpuPercent, double RamPercent, ulong RamUsedBytes, ulong RamTotalBytes);

/// <summary>Machine-wide CPU and RAM usage. CPU is a delta between consecutive calls.</summary>
public sealed class SystemMetrics
{
    private long _prevIdle, _prevKernel, _prevUser;
    private bool _hasPrevious;

    public SystemSnapshot Sample()
    {
        double cpu = 0;
        if (Native.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            if (_hasPrevious)
            {
                // Kernel time includes idle time.
                long total = (kernel - _prevKernel) + (user - _prevUser);
                long busy = total - (idle - _prevIdle);
                if (total > 0) cpu = Math.Clamp(100.0 * busy / total, 0, 100);
            }
            (_prevIdle, _prevKernel, _prevUser, _hasPrevious) = (idle, kernel, user, true);
        }

        var mem = new Native.MemoryStatusEx { Length = (uint)Marshal.SizeOf<Native.MemoryStatusEx>() };
        if (!Native.GlobalMemoryStatusEx(ref mem) || mem.TotalPhys == 0)
            return new SystemSnapshot(cpu, 0, 0, 0);

        ulong used = mem.TotalPhys - mem.AvailPhys;
        return new SystemSnapshot(cpu, 100.0 * used / mem.TotalPhys, used, mem.TotalPhys);
    }
}
