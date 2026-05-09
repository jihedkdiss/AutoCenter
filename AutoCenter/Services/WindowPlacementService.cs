using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using AutoCenter.Models;

namespace AutoCenter.Services;

public sealed class WindowPlacementService : IDisposable
{
    private readonly object _lock = new();
    private readonly HashSet<IntPtr> _excludedWindows = new();
    private readonly HashSet<IntPtr> _movedWindows = new();
    private readonly MonitorService _monitorService = new();
    private AppSettings _settings;
    private CancellationTokenSource? _listenerCts;
    private IntPtr _lastForegroundWindow;
    private IntPtr _lastExternalForegroundWindow;

    public WindowPlacementService(AppSettings settings)
    {
        _settings = settings;
    }

    public event EventHandler<string>? StatusChanged;

    public bool IsAutoListening => _listenerCts is { IsCancellationRequested: false };

    public void UpdateSettings(AppSettings settings)
    {
        lock (_lock)
        {
            _settings = settings;
        }

        if (settings.AutoCenterNewWindows && !IsAutoListening)
        {
            StartAutoListener();
        }
        else if (!settings.AutoCenterNewWindows && IsAutoListening)
        {
            StopAutoListener();
        }
    }

    public void StartAutoListener()
    {
        if (IsAutoListening)
        {
            return;
        }

        _listenerCts = new CancellationTokenSource();
        _ = Task.Run(() => ListenForForegroundWindowsAsync(_listenerCts.Token));
        RaiseStatus("Auto centering is active");
    }

    public void StopAutoListener()
    {
        _listenerCts?.Cancel();
        _listenerCts = null;
        RaiseStatus("Auto centering is paused");
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _movedWindows.Clear();
        }
        RaiseStatus("Moved window history cleared");
    }

    public bool CenterForegroundWindow()
    {
        var windowHandle = NativeMethods.GetForegroundWindow();
        if (IsExcluded(windowHandle) && _lastExternalForegroundWindow != IntPtr.Zero)
        {
            windowHandle = _lastExternalForegroundWindow;
        }

        return CenterWindow(windowHandle, "Centered the active window");
    }

    public int CenterAllWindows(IntPtr selfWindowHandle)
    {
        AppSettings settings;
        lock (_lock)
        {
            settings = _settings;
        }

        var centered = 0;
        NativeMethods.EnumWindows((handle, _) =>
        {
            if (handle == selfWindowHandle || handle == IntPtr.Zero)
            {
                return true;
            }
            if (!NativeMethods.IsWindowVisible(handle))
            {
                return true;
            }
            // Skip windows without a non-empty title to filter out invisible/system surfaces.
            var titleLength = NativeMethods.GetWindowTextLength(handle);
            if (titleLength <= 0)
            {
                return true;
            }
            // Skip excluded by class/process.
            if (IsExcluded(handle))
            {
                return true;
            }

            // Force-center: bypass SkipMaximized and IncludeFixedSize filters by using a temporary settings copy.
            var forceSettings = ClonePermissive(settings);

            if (TryBuildPlacement(handle, forceSettings, out var placement))
            {
                var style = GetWindowStyle(handle);
                if (style.HasFlag(WindowStyles.Maximize))
                {
                    NativeMethods.ShowWindow(handle, NativeMethods.SwRestore);
                    Thread.Sleep(40);
                }

                if (NativeMethods.SetWindowPos(handle, IntPtr.Zero, placement.X, placement.Y,
                    placement.Width, placement.Height,
                    NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate))
                {
                    centered++;
                }
            }

            return true;
        }, IntPtr.Zero);

        RaiseStatus(centered == 0
            ? "No eligible windows to center"
            : $"Centered {centered} window{(centered == 1 ? string.Empty : "s")}");
        return centered;
    }

    private static AppSettings ClonePermissive(AppSettings settings)
    {
        return new AppSettings
        {
            AutoCenterNewWindows = settings.AutoCenterNewWindows,
            CenterWithHotkey = settings.CenterWithHotkey,
            OnlyOncePerWindow = false,
            SmartFilter = settings.SmartFilter,
            SkipMaximizedWindows = false,
            IncludeFixedSizeWindows = true,
            UseWorkingArea = settings.UseWorkingArea,
            Alignment = settings.Alignment,
            TargetMonitorMode = settings.TargetMonitorMode,
            SpecificMonitorDeviceName = settings.SpecificMonitorDeviceName,
            ResizeMode = settings.ResizeMode,
            WidthPercent = settings.WidthPercent,
            HeightPercent = settings.HeightPercent,
            WidthPixels = settings.WidthPixels,
            HeightPixels = settings.HeightPixels,
            OffsetX = settings.OffsetX,
            OffsetY = settings.OffsetY,
            UseUniformMargin = settings.UseUniformMargin,
            UniformMargin = settings.UniformMargin,
            MarginX = settings.MarginX,
            MarginY = settings.MarginY,
            ExcludedProcessNames = settings.ExcludedProcessNames
        };
    }

    public void RegisterExcludedWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        lock (_lock)
        {
            _excludedWindows.Add(windowHandle);
        }
    }

    public bool CenterWindow(IntPtr windowHandle, string successMessage)
    {
        if (windowHandle == IntPtr.Zero || !NativeMethods.IsWindowVisible(windowHandle))
        {
            return false;
        }

        AppSettings settings;
        lock (_lock)
        {
            settings = _settings;
        }

        if (!ShouldCenter(windowHandle, settings, manual: successMessage.Contains("active", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!TryBuildPlacement(windowHandle, settings, out var placement))
        {
            return false;
        }

        var style = GetWindowStyle(windowHandle);
        if (style.HasFlag(WindowStyles.Maximize))
        {
            NativeMethods.ShowWindow(windowHandle, NativeMethods.SwRestore);
            Thread.Sleep(60);
        }

        var moved = NativeMethods.SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            placement.X,
            placement.Y,
            placement.Width,
            placement.Height,
            NativeMethods.SwpNoZOrder | NativeMethods.SwpNoActivate);

        if (moved)
        {
            lock (_lock)
            {
                _movedWindows.Add(windowHandle);
            }
            RaiseStatus(successMessage);
        }

        return moved;
    }

    public void Dispose()
    {
        StopAutoListener();
    }

    private async Task ListenForForegroundWindowsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            AppSettings settings;
            lock (_lock)
            {
                settings = _settings;
            }

            var windowHandle = NativeMethods.GetForegroundWindow();
            if (windowHandle != IntPtr.Zero && windowHandle != _lastForegroundWindow)
            {
                _lastForegroundWindow = windowHandle;
                if (!IsExcluded(windowHandle))
                {
                    _lastExternalForegroundWindow = windowHandle;
                }

                if (!IsExcluded(windowHandle))
                {
                    CenterWindow(windowHandle, "Centered a new foreground window");
                }
            }

            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
    }

    private bool ShouldCenter(IntPtr windowHandle, AppSettings settings, bool manual)
    {
        if (IsExcluded(windowHandle))
        {
            return false;
        }

        if (settings.OnlyOncePerWindow && !manual)
        {
            lock (_lock)
            {
                if (_movedWindows.Contains(windowHandle))
                {
                    return false;
                }
            }
        }

        var style = GetWindowStyle(windowHandle);
        if (settings.SkipMaximizedWindows && !(manual && settings.HotkeyBypassMaximized) && style.HasFlag(WindowStyles.Maximize))
        {
            return false;
        }

        if (!settings.IncludeFixedSizeWindows && !(manual && settings.HotkeyBypassFixedSize) && !style.HasFlag(WindowStyles.ThickFrame))
        {
            return false;
        }

        return true;
    }

    private bool IsExcluded(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return true;

        lock (_lock)
        {
            if (_excludedWindows.Contains(windowHandle))
            {
                return true;
            }

            var processName = GetProcessName(windowHandle);
            if (!string.IsNullOrEmpty(processName))
            {
                if (_settings.ExcludedProcessNames.Any(name => string.Equals(name, processName, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }
        }

        // Window class exclusions inspired by window-centering-helper
        var className = GetWindowClass(windowHandle);
        if (className.Contains("CoreWindow", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("WorkerW", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("Flyout", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("DV2ControlHost", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("NotifyIcon", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("NativeHWNDHost", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("Popup", StringComparison.OrdinalIgnoreCase) ||
            className.Contains("Progman", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string GetProcessName(IntPtr windowHandle)
    {
        _ = NativeMethods.GetWindowThreadProcessId(windowHandle, out var processId);
        if (processId == 0) return string.Empty;

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string GetWindowClass(IntPtr windowHandle)
    {
        var builder = new StringBuilder(256);
        NativeMethods.GetClassName(windowHandle, builder, builder.Capacity);
        return builder.ToString();
    }

    private static WindowStyles GetWindowStyle(IntPtr windowHandle)
    {
        return (WindowStyles)NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlStyle).ToInt64();
    }

    private bool TryBuildPlacement(IntPtr windowHandle, AppSettings settings, out WindowPlacement placement)
    {
        placement = default;

        if (!NativeMethods.GetWindowRect(windowHandle, out var currentRect))
        {
            return false;
        }

        // 1. Determine Target Monitor
        MonitorInfo? targetMonitor = settings.TargetMonitorMode switch
        {
            TargetMonitorMode.Primary => _monitorService.GetPrimaryMonitor(),
            TargetMonitorMode.Specific => _monitorService.GetMonitorByDeviceName(settings.SpecificMonitorDeviceName),
            _ => _monitorService.GetMonitorFromWindow(windowHandle)
        };

        if (targetMonitor is null)
        {
            targetMonitor = _monitorService.GetMonitorFromWindow(windowHandle) ?? _monitorService.GetPrimaryMonitor();
        }

        if (targetMonitor is null)
        {
            return false;
        }

        var targetArea = ComputeEffectiveArea(targetMonitor, settings);

        var marginX = settings.ResizeMode == ResizeMode.UniformMargin ? settings.UniformMargin
            : settings.ResizeMode == ResizeMode.SeparateMargins ? settings.MarginX
            : settings.UseUniformMargin ? settings.UniformMargin
            : settings.MarginX;
        var marginY = settings.ResizeMode == ResizeMode.UniformMargin ? settings.UniformMargin
            : settings.ResizeMode == ResizeMode.SeparateMargins ? settings.MarginY
            : settings.UseUniformMargin ? settings.UniformMargin
            : settings.MarginY;

        // 2. Determine Size
        var currentWidth = Math.Max(120, currentRect.Right - currentRect.Left);
        var currentHeight = Math.Max(90, currentRect.Bottom - currentRect.Top);

        int width, height;
        switch (settings.ResizeMode)
        {
            case ResizeMode.PercentOfMonitor:
                width = PercentOf(targetArea.Width, settings.WidthPercent);
                height = PercentOf(targetArea.Height, settings.HeightPercent);
                break;
            case ResizeMode.FixedPixels:
                width = settings.WidthPixels;
                height = settings.HeightPixels;
                break;
            case ResizeMode.UniformMargin:
                width = targetArea.Width - 2 * settings.UniformMargin;
                height = targetArea.Height - 2 * settings.UniformMargin;
                break;
            case ResizeMode.SeparateMargins:
                width = targetArea.Width - 2 * settings.MarginX;
                height = targetArea.Height - 2 * settings.MarginY;
                break;
            default:
                width = currentWidth;
                height = currentHeight;
                break;
        }

        // Clamp size to monitor
        width = Math.Clamp(width, 120, targetArea.Width);
        height = Math.Clamp(height, 90, targetArea.Height);

        // 3. Determine Position based on Alignment
        int x = 0, y = 0;

        switch (settings.Alignment)
        {
            case PlacementAlignment.TopLeft:
                x = targetArea.Left + marginX;
                y = targetArea.Top + marginY;
                break;
            case PlacementAlignment.Top:
                x = targetArea.Left + (targetArea.Width - width) / 2;
                y = targetArea.Top + marginY;
                break;
            case PlacementAlignment.TopRight:
                x = targetArea.Right - width - marginX;
                y = targetArea.Top + marginY;
                break;
            case PlacementAlignment.Left:
                x = targetArea.Left + marginX;
                y = targetArea.Top + (targetArea.Height - height) / 2;
                break;
            case PlacementAlignment.Center:
                x = targetArea.Left + (targetArea.Width - width) / 2;
                y = targetArea.Top + (targetArea.Height - height) / 2;
                break;
            case PlacementAlignment.Right:
                x = targetArea.Right - width - marginX;
                y = targetArea.Top + (targetArea.Height - height) / 2;
                break;
            case PlacementAlignment.BottomLeft:
                x = targetArea.Left + marginX;
                y = targetArea.Bottom - height - marginY;
                break;
            case PlacementAlignment.Bottom:
                x = targetArea.Left + (targetArea.Width - width) / 2;
                y = targetArea.Bottom - height - marginY;
                break;
            case PlacementAlignment.BottomRight:
                x = targetArea.Right - width - marginX;
                y = targetArea.Bottom - height - marginY;
                break;
        }

        // Apply offsets
        x += settings.OffsetX;
        y += settings.OffsetY;

        placement = new WindowPlacement(x, y, width, height);
        return true;
    }



    private static int PercentOf(int value, int percent)
    {
        return (int)Math.Round(value * (Math.Clamp(percent, 10, 100) / 100d));
    }

    private static DisplayRect GetEffectiveWorkingArea(MonitorInfo monitor)
    {
        var workingArea = monitor.WorkingArea;
        var bounds = monitor.Bounds;

        if (workingArea.Left > bounds.Left || workingArea.Top > bounds.Top ||
            workingArea.Right < bounds.Right || workingArea.Bottom < bounds.Bottom)
        {
            return workingArea;
        }

        var abd = new AppBarData { CbSize = Marshal.SizeOf<AppBarData>() };
        var state = NativeMethods.SHAppBarMessage(NativeMethods.AbmGetState, ref abd);

        if ((state & NativeMethods.AbsAutoHide) != 0)
        {
            abd = new AppBarData { CbSize = Marshal.SizeOf<AppBarData>() };
            NativeMethods.SHAppBarMessage(NativeMethods.AbmGetTaskBarPos, ref abd);

            var taskRect = abd.Rc;
            var taskEdge = abd.Edge;

            if (taskRect.Left < bounds.Right && taskRect.Right > bounds.Left &&
                taskRect.Top < bounds.Bottom && taskRect.Bottom > bounds.Top)
            {
                return taskEdge switch
                {
                    NativeMethods.AbELeft => new DisplayRect(bounds.Left + 2, bounds.Top, bounds.Right, bounds.Bottom),
                    NativeMethods.AbETop => new DisplayRect(bounds.Left, bounds.Top + 2, bounds.Right, bounds.Bottom),
                    NativeMethods.AbERight => new DisplayRect(bounds.Left, bounds.Top, bounds.Right - 2, bounds.Bottom),
                    _ => new DisplayRect(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom - 2)
                };
            }
        }

        return workingArea;
    }

    private static DisplayRect ComputeEffectiveArea(MonitorInfo monitor, AppSettings settings)
    {
        if (!settings.UseWorkingArea)
        {
            return monitor.Bounds;
        }

        return GetEffectiveWorkingArea(monitor);
    }



    private void RaiseStatus(string message)
    {
        StatusChanged?.Invoke(this, $"{DateTime.Now:t} - {message}");
    }

    private readonly record struct WindowPlacement(int X, int Y, int Width, int Height);

    [Flags]
    private enum WindowStyles : long
    {
        Maximize = 0x01000000,
        ThickFrame = 0x00040000
    }

    private static class NativeMethods
    {
        public const int GwlStyle = -16;
        public const int SwRestore = 9;
        public const uint SwpNoZOrder = 0x0004;
        public const uint SwpNoActivate = 0x0010;

        public const uint AbmGetState = 4;
        public const uint AbmGetTaskBarPos = 5;
        public const uint AbsAutoHide = 0x00000001;
        public const uint AbELeft = 0;
        public const uint AbETop = 1;
        public const uint AbERight = 2;
        public const uint AbEBottom = 3;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("shell32.dll")]
        public static extern uint SHAppBarMessage(uint dwMessage, ref AppBarData pData);

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int CbSize;
        public IntPtr HWnd;
        public uint UCallbackMessage;
        public uint Edge;
        public AppBarRect Rc;
        public IntPtr LParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
