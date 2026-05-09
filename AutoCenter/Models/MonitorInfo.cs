namespace AutoCenter.Models;

public sealed class MonitorInfo
{
    public MonitorInfo(IntPtr handle, string deviceName, bool isPrimary, DisplayRect bounds, DisplayRect workingArea, int index)
    {
        Handle = handle;
        Index = index;
        DeviceName = deviceName;
        IsPrimary = isPrimary;
        Bounds = bounds;
        WorkingArea = workingArea;
    }

    public IntPtr Handle { get; }
    public int Index { get; }
    public string DeviceName { get; }
    public bool IsPrimary { get; }
    public DisplayRect Bounds { get; }
    public DisplayRect WorkingArea { get; }

    public string DisplayName
    {
        get
        {
            var primary = IsPrimary ? " primary" : string.Empty;
            return $"Monitor {Index + 1}{primary} - {WorkingArea.Width}x{WorkingArea.Height}";
        }
    }
}

public readonly record struct DisplayRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}
