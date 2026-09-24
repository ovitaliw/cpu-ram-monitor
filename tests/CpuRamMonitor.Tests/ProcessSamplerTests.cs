using System.Runtime.InteropServices;
using CpuRamMonitor.Metrics;

namespace CpuRamMonitor.Tests;

public class ProcessSamplerTests
{
    private const long OneSecond = 10_000_000; // 100 ns ticks

    private static ProcessSample S(int pid, string name, long cpu, long mem = 0, long created = 1) =>
        new(pid, created, name, 1, cpu, mem);

    [Fact]
    public void Groups_by_name_and_sums_cpu_and_memory()
    {
        var previous = new Dictionary<ProcessKey, long>
        {
            [new(10, 1)] = 0,
            [new(11, 1)] = 0,
            [new(20, 1)] = 0,
        };
        var current = new[]
        {
            S(10, "node", cpu: OneSecond, mem: 100),
            S(11, "Node", cpu: OneSecond, mem: 50),
            S(20, "chrome", cpu: OneSecond / 2, mem: 10),
        };

        var groups = ProcessSampler.Aggregate(current, previous, elapsed: OneSecond, cores: 4, _ => true)
            .ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);

        // 2 s of CPU over 1 s wall time on 4 cores = 50 %
        Assert.Equal(2, groups["node"].Count);
        Assert.Equal(50, groups["node"].CpuPercent, precision: 6);
        Assert.Equal(150, groups["node"].MemoryBytes);
        Assert.Equal(12.5, groups["chrome"].CpuPercent, precision: 6);
    }

    [Fact]
    public void New_or_reused_pids_contribute_no_cpu_on_first_sight()
    {
        var previous = new Dictionary<ProcessKey, long> { [new(10, CreateTime: 1)] = 0 };
        var current = new[] { S(10, "node", cpu: 5 * OneSecond, created: 2) }; // same PID, different process

        var group = Assert.Single(ProcessSampler.Aggregate(current, previous, OneSecond, 1, _ => true));
        Assert.Equal(0, group.CpuPercent);
    }

    [Fact]
    public void Cpu_is_capped_at_100_and_excluded_samples_are_ignored()
    {
        var previous = new Dictionary<ProcessKey, long> { [new(1, 1)] = 0, [new(2, 1)] = 0 };
        var current = new[] { S(1, "busy", cpu: 10 * OneSecond), S(2, "hidden", cpu: OneSecond) };

        var group = Assert.Single(ProcessSampler.Aggregate(current, previous, OneSecond, 1, s => s.Name != "hidden"));
        Assert.Equal("busy", group.Name);
        Assert.Equal(100, group.CpuPercent);
    }

    [Fact]
    public void Native_struct_matches_x64_layout()
    {
        Assert.Equal(8, IntPtr.Size);
        Assert.Equal(40, (int)Marshal.OffsetOf<Native.SystemProcessInfo>(nameof(Native.SystemProcessInfo.UserTime)));
        Assert.Equal(56, (int)Marshal.OffsetOf<Native.SystemProcessInfo>(nameof(Native.SystemProcessInfo.ImageName)));
        Assert.Equal(80, (int)Marshal.OffsetOf<Native.SystemProcessInfo>(nameof(Native.SystemProcessInfo.UniqueProcessId)));
        Assert.Equal(100, (int)Marshal.OffsetOf<Native.SystemProcessInfo>(nameof(Native.SystemProcessInfo.SessionId)));
        Assert.Equal(144, (int)Marshal.OffsetOf<Native.SystemProcessInfo>(nameof(Native.SystemProcessInfo.WorkingSetSize)));
    }

    [Fact]
    public void Live_snapshot_contains_the_current_process()
    {
        var self = Assert.Single(new ProcessSampler().ReadAll(), s => s.Pid == Environment.ProcessId);
        Assert.Equal(ProcessRules.CurrentSession, self.SessionId);
        Assert.True(self.MemoryBytes > 0);
        Assert.True(self.CpuTime > 0);
        Assert.False(string.IsNullOrEmpty(self.Name));
    }
}
