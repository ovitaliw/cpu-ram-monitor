using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using CpuRamMonitor.Metrics;

namespace CpuRamMonitor;

/// <summary>Monitor work areas in WPF device-independent units.</summary>
internal static class ScreenUtil
{
    public static Rect WorkAreaNearest(Visual visual, Point dipPoint)
    {
        var monitor = Native.MonitorFromPoint(ToDevicePoint(visual, dipPoint), Native.MonitorDefaultToNearest);
        var info = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
        if (!Native.GetMonitorInfo(monitor, ref info)) return SystemParameters.WorkArea;

        var fromDevice = ToDevice(visual);
        fromDevice.Invert();
        return new Rect(
            fromDevice.Transform(new Point(info.Work.Left, info.Work.Top)),
            fromDevice.Transform(new Point(info.Work.Right, info.Work.Bottom)));
    }

    public static bool IsOnAnyMonitor(Visual visual, Point dipPoint) =>
        Native.MonitorFromPoint(ToDevicePoint(visual, dipPoint), Native.MonitorDefaultToNull) != IntPtr.Zero;

    private static Native.Point32 ToDevicePoint(Visual visual, Point dipPoint)
    {
        var p = ToDevice(visual).Transform(dipPoint);
        return new Native.Point32 { X = (int)p.X, Y = (int)p.Y };
    }

    private static Matrix ToDevice(Visual visual) =>
        PresentationSource.FromVisual(visual)?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
}
