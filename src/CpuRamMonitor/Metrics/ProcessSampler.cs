using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CpuRamMonitor.Metrics;

/// <summary>Identifies a process instance; CreateTime guards against PID reuse.</summary>
public readonly record struct ProcessKey(int Pid, long CreateTime);

/// <param name="CpuTime">User + kernel time, in 100 ns ticks.</param>
/// <param name="MemoryBytes">Private working set (what Task Manager shows as "Memory").</param>
public readonly record struct ProcessSample(int Pid, long CreateTime, string Name, int SessionId, long CpuTime, long MemoryBytes)
{
    public ProcessKey Key => new(Pid, CreateTime);
}

public sealed record ProcessGroup(string Name, int Count, double CpuPercent, long MemoryBytes);

/// <summary>
/// Snapshots every process with a single NtQuerySystemInformation call (no per-process handles,
/// so protected processes like VmmemWSL are visible too) and groups them by name.
/// </summary>
public sealed class ProcessSampler
{
    private Dictionary<ProcessKey, long> _previousCpu = [];
    private long _previousTimestamp;
    private int _bufferSize = 1 << 20;

    public IReadOnlyList<ProcessGroup> Sample(Func<ProcessSample, bool> include)
    {
        var samples = ReadAll();
        long now = Stopwatch.GetTimestamp();
        long elapsed = _previousTimestamp == 0 ? 0 : Stopwatch.GetElapsedTime(_previousTimestamp, now).Ticks;

        var groups = Aggregate(samples, _previousCpu, elapsed, Environment.ProcessorCount, include);

        _previousCpu = samples.ToDictionary(s => s.Key, s => s.CpuTime);
        _previousTimestamp = now;
        return groups;
    }

    /// <param name="elapsed">Wall time between samples, in 100 ns ticks.</param>
    internal static List<ProcessGroup> Aggregate(
        IEnumerable<ProcessSample> samples,
        IReadOnlyDictionary<ProcessKey, long> previousCpu,
        long elapsed,
        int cores,
        Func<ProcessSample, bool> include)
    {
        var acc = new Dictionary<string, (string Name, int Count, long CpuDelta, long Memory)>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in samples)
        {
            if (!include(s)) continue;
            long delta = previousCpu.TryGetValue(s.Key, out var prev) ? Math.Max(0, s.CpuTime - prev) : 0;
            acc[s.Name] = acc.TryGetValue(s.Name, out var a)
                ? (a.Name, a.Count + 1, a.CpuDelta + delta, a.Memory + s.MemoryBytes)
                : (s.Name, 1, delta, s.MemoryBytes);
        }

        double capacity = (double)elapsed * cores;
        return acc.Values
            .Select(a => new ProcessGroup(a.Name, a.Count, capacity > 0 ? Math.Min(100, 100.0 * a.CpuDelta / capacity) : 0, a.Memory))
            .ToList();
    }

    internal unsafe List<ProcessSample> ReadAll()
    {
        while (true)
        {
            var buffer = NativeMemory.Alloc((nuint)_bufferSize);
            try
            {
                int status = Native.NtQuerySystemInformation(Native.SystemProcessInformation, (IntPtr)buffer, _bufferSize, out int needed);
                if (status == Native.StatusInfoLengthMismatch)
                {
                    _bufferSize = Math.Max(_bufferSize * 2, needed + (64 << 10));
                    continue;
                }
                if (status != 0) throw new Win32Exception($"NtQuerySystemInformation failed: 0x{status:X8}");
                return Parse((byte*)buffer);
            }
            finally
            {
                NativeMemory.Free(buffer);
            }
        }
    }

    private static unsafe List<ProcessSample> Parse(byte* entry)
    {
        var result = new List<ProcessSample>(512);
        while (true)
        {
            var info = (Native.SystemProcessInfo*)entry;
            int pid = (int)info->UniqueProcessId;
            if (pid != 0 && info->ImageName.Buffer != IntPtr.Zero)
            {
                var name = new string((char*)info->ImageName.Buffer, 0, info->ImageName.Length / 2);
                result.Add(new ProcessSample(pid, info->CreateTime, ProcessRules.NormalizeName(name), (int)info->SessionId,
                    info->UserTime + info->KernelTime, info->WorkingSetPrivateSize));
            }
            if (info->NextEntryOffset == 0) return result;
            entry += info->NextEntryOffset;
        }
    }
}
