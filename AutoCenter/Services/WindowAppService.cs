using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoCenter.Services;

public static class WindowAppService
{
    public sealed record RunningApp(string DisplayName, string ProcessName);

    public static IReadOnlyList<RunningApp> GetRunningApps()
    {
        var apps = new Dictionary<string, RunningApp>(StringComparer.OrdinalIgnoreCase);
        NativeMethods.EnumWindows((window, _) =>
        {
            if (!NativeMethods.IsWindowVisible(window) ||
                IsSystemOrToolWindow(window) ||
                NativeMethods.GetWindowTextLength(window) == 0)
            {
                return true;
            }

            var processName = GetProcessNames(window)
                .FirstOrDefault(name => !name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(processName) ||
                processName.Equals("AutoCenter", StringComparison.OrdinalIgnoreCase) ||
                processName.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var title = new StringBuilder(512);
            NativeMethods.GetWindowText(window, title, title.Capacity);
            var displayName = $"{processName} — {title}";
            apps.TryAdd(processName, new RunningApp(displayName, processName));
            return true;
        }, IntPtr.Zero);

        return apps.Values.OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static IReadOnlyList<string> GetProcessNames(IntPtr window)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var owner = GetProcessName(window);
        if (!string.IsNullOrEmpty(owner)) names.Add(owner);

        if (owner.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) &&
            GetWindowClass(window).Equals("ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase))
        {
            NativeMethods.EnumChildWindows(window, (child, _) =>
            {
                var name = GetProcessName(child);
                if (!string.IsNullOrEmpty(name)) names.Add(name);
                return true;
            }, IntPtr.Zero);
        }

        return names.ToList();
    }

    public static bool IsUnresolvedHostedWindow(IntPtr window)
    {
        if (!GetProcessName(window).Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) ||
            !GetWindowClass(window).Equals("ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return GetProcessNames(window).All(name =>
            name.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSystemOrToolWindow(IntPtr window)
    {
        var className = GetWindowClass(window);
        if (className is "Progman" or "WorkerW") return true;
        const long toolWindowStyle = 0x80;
        return (NativeMethods.GetWindowLongPtr(window, -20).ToInt64() & toolWindowStyle) != 0;
    }

    private static string GetProcessName(IntPtr window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var processId);
        if (processId == 0) return string.Empty;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetWindowClass(IntPtr window)
    {
        var name = new StringBuilder(256);
        NativeMethods.GetClassName(window, name, name.Capacity);
        return name.ToString();
    }

    private static class NativeMethods
    {
        public delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        public static extern int GetWindowTextLength(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(IntPtr window, StringBuilder title, int capacity);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr window, StringBuilder className, int capacity);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        public static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    }
}
