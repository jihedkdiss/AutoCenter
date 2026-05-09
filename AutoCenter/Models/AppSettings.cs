namespace AutoCenter.Models;

public sealed class AppSettings
{
    public bool AutoCenterNewWindows { get; set; } = true;
    public bool CenterWithHotkey { get; set; } = true;
    public bool OnlyOncePerWindow { get; set; }
    public bool SmartFilter { get; set; } = true;
    public bool SkipMaximizedWindows { get; set; } = true;
    public bool IncludeFixedSizeWindows { get; set; }
    public bool UseWorkingArea { get; set; } = true;
    public PlacementAlignment Alignment { get; set; } = PlacementAlignment.Center;
    public TargetMonitorMode TargetMonitorMode { get; set; } = TargetMonitorMode.CurrentWindow;
    public string? SpecificMonitorDeviceName { get; set; }
    public ResizeMode ResizeMode { get; set; } = ResizeMode.UniformMargin;
    public int WidthPercent { get; set; } = 72;
    public int HeightPercent { get; set; } = 78;
    public int WidthPixels { get; set; } = 1280;
    public int HeightPixels { get; set; } = 820;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public bool UseUniformMargin { get; set; } = true;
    public int UniformMargin { get; set; } = 20;
    public int MarginX { get; set; } = 24;
    public int MarginY { get; set; } = 24;
    public int PollIntervalMs { get; set; } = 180;
    public int ActivationDelayMs { get; set; } = 120;
    public int HotkeyModifiers { get; set; } = 0x0002 | 0x0004;
    public int HotkeyVirtualKey { get; set; } = 0x43;
    public bool HotkeyBypassFixedSize { get; set; } = true;
    public bool HotkeyBypassMaximized { get; set; }
    public bool UseHotkeySequence { get; set; } = true;
    public int HotkeySequenceKey { get; set; } = 0x10; // VK_SHIFT
    public int HotkeySequenceCount { get; set; } = 2;
    public int HotkeySequenceTimeoutMs { get; set; } = 500;
    public static readonly IReadOnlyList<string> DefaultExcludedProcessNames = new List<string>
    {
        "ShellExperienceHost",
        "StartMenuExperienceHost",
        "SearchHost",
        "LockApp",
        "Taskmgr"
    }.AsReadOnly();

    public List<string> ExcludedProcessNames { get; set; } = new(DefaultExcludedProcessNames);
    public bool LaunchAtStartup { get; set; } = true;
    public bool HideTrayIcon { get; set; }
    public int Theme { get; set; }
}

public enum TargetMonitorMode
{
    CurrentWindow,
    Primary,
    Specific
}

public enum ResizeMode
{
    PreserveCurrentSize,
    PercentOfMonitor,
    FixedPixels,
    UniformMargin,
    SeparateMargins
}

public enum PlacementAlignment
{
    TopLeft,
    Top,
    TopRight,
    Left,
    Center,
    Right,
    BottomLeft,
    Bottom,
    BottomRight
}
