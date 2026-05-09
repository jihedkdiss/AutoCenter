using System.Runtime.InteropServices;
using AutoCenter.Models;

namespace AutoCenter.Services;

public sealed class MonitorService
{
    public IReadOnlyList<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        NativeMethods.EnumDisplayMonitors(
            IntPtr.Zero,
            IntPtr.Zero,
            (monitorHandle, _, _, _) =>
            {
                if (TryGetMonitor(monitorHandle, monitors.Count, out var monitor))
                {
                    monitors.Add(monitor);
                }

                return true;
            },
            IntPtr.Zero);

        return monitors;
    }

    public MonitorInfo? GetPrimaryMonitor()
    {
        return GetMonitors().FirstOrDefault(monitor => monitor.IsPrimary);
    }

    public MonitorInfo? GetMonitorFromWindow(IntPtr windowHandle)
    {
        var monitorHandle = NativeMethods.MonitorFromWindow(windowHandle, NativeMethods.MonitorDefaultToNearest);
        return TryGetMonitor(monitorHandle, 0, out var monitor) ? monitor : null;
    }

    public MonitorInfo? GetMonitorByDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        return GetMonitors().FirstOrDefault(monitor =>
            string.Equals(monitor.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetMonitor(IntPtr monitorHandle, int index, out MonitorInfo monitor)
    {
        var nativeInfo = new NativeMethods.MonitorInfoEx
        {
            Size = Marshal.SizeOf<NativeMethods.MonitorInfoEx>()
        };

        if (!NativeMethods.GetMonitorInfo(monitorHandle, ref nativeInfo))
        {
            monitor = null!;
            return false;
        }

        monitor = new MonitorInfo(
            monitorHandle,
            nativeInfo.DeviceName,
            (nativeInfo.Flags & NativeMethods.MonitorInfoPrimary) != 0,
            ToDisplayRect(nativeInfo.Monitor),
            ToDisplayRect(nativeInfo.WorkArea),
            index);
        return true;
    }

    private static DisplayRect ToDisplayRect(NativeMethods.Rect rect)
    {
        return new DisplayRect(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }

    private static class NativeMethods
    {
        public const uint MonitorDefaultToNearest = 0x00000002;
        public const uint MonitorInfoPrimary = 0x00000001;

        public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct MonitorInfoEx
        {
            public int Size;
            public Rect Monitor;
            public Rect WorkArea;
            public uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
        }
    }
}
