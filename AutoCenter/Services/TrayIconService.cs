using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace AutoCenter.Services;

public sealed class TrayIconService : IDisposable
{
    private const int WmTrayIcon = 0x0401;
    private const int NidAdd = 0x00000000;
    private const int NidModify = 0x00000001;
    private const int NidDelete = 0x00000002;
    private const int NifMessage = 0x00000001;
    private const int NifIcon = 0x00000002;
    private const int NifTip = 0x00000004;

    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonUp = 0x0205;

    private const int MfString = 0x00000000;
    private const int MfSeparator = 0x00000800;
    private const int MfByPosition = 0x00000400;
    private const int TpmLeftAlign = 0x0000;
    private const int TpmRightButton = 0x0002;
    private const int TpmNonotify = 0x0080;
    private const int TpmReturnCmd = 0x0100;
    private const int WmCommand = 0x0111;
    private const int WmInitMenuPopup = 0x0117;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int MenuExitCmd = 1001;

    private readonly Window _window;
    private IntPtr _hwnd;
    private bool _isIconAdded;
    private NativeMethods.WndProc? _newWndProc;
    private IntPtr _oldWndProc;
    private IntPtr _contextMenu;
    private IntPtr _hIcon;

    public int MinWidth { get; set; } = 760;
    public int MinHeight { get; set; } = 560;
    public bool IconVisible { get; private set; }

    public TrayIconService(Window window)
    {
        _window = window;
    }

    public event EventHandler? TrayIconClicked;
    public event EventHandler? ExitRequested;

    public void Initialize()
    {
        _hwnd = WindowNative.GetWindowHandle(_window);
        CreateContextMenu();
        EnsureSubclassed();
        AddIcon();
    }

    public void Dispose()
    {
        DestroyContextMenu();
        RemoveIcon();
        if (_hwnd != IntPtr.Zero && _oldWndProc != IntPtr.Zero)
        {
            NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GwlpWndProc, _oldWndProc);
            _oldWndProc = IntPtr.Zero;
        }
    }

    private void CreateContextMenu()
    {
        _contextMenu = NativeMethods.CreatePopupMenu();
        NativeMethods.AppendMenuW(_contextMenu, MfString, (IntPtr)MenuExitCmd, "Exit AutoCenter");
    }

    private void DestroyContextMenu()
    {
        if (_contextMenu != IntPtr.Zero)
        {
            NativeMethods.DestroyMenu(_contextMenu);
            _contextMenu = IntPtr.Zero;
        }
    }

    private void EnsureSubclassed()
    {
        _newWndProc = WndProc;
        _oldWndProc = NativeMethods.SetWindowLongPtr(_hwnd, NativeMethods.GwlpWndProc, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    private IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == WmTrayIcon)
        {
            var eventId = (int)(lParam.ToInt64() & 0xFFFF);
            if (eventId == WmLeftButtonUp)
            {
                TrayIconClicked?.Invoke(this, EventArgs.Empty);
            }
            else if (eventId == WmRightButtonUp)
            {
                ShowContextMenu();
            }
        }
        else if (message == WmCommand)
        {
            var cmdId = wParam.ToInt64() & 0xFFFF;
            if (cmdId == MenuExitCmd)
            {
                ExitRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        else if (message == WmGetMinMaxInfo && MinWidth > 0 && MinHeight > 0)
        {
            var info = Marshal.PtrToStructure<NativeMethods.MinMaxInfo>(lParam);
            var dpi = NativeMethods.GetDpiForWindow(hwnd);
            var scale = dpi <= 0 ? 1.0 : dpi / 96.0;
            info.PtMinTrackSize.X = (int)Math.Round(MinWidth * scale);
            info.PtMinTrackSize.Y = (int)Math.Round(MinHeight * scale);
            Marshal.StructureToPtr(info, lParam, true);
        }

        return NativeMethods.CallWindowProc(_oldWndProc, hwnd, message, wParam, lParam);
    }

    public void SetIconVisible(bool visible)
    {
        if (visible == IconVisible)
        {
            return;
        }

        if (visible)
        {
            AddIcon();
        }
        else
        {
            RemoveIcon();
        }
    }

    private void ShowContextMenu()
    {
        NativeMethods.GetCursorPos(out var pt);
        NativeMethods.SetForegroundWindow(_hwnd);
        NativeMethods.TrackPopupMenu(_contextMenu, TpmLeftAlign | TpmRightButton, pt.X, pt.Y, 0, _hwnd, IntPtr.Zero);
        NativeMethods.PostMessage(_hwnd, 0x0000, IntPtr.Zero, IntPtr.Zero);
    }

    private void AddIcon()
    {
        _hIcon = LoadAppIcon();

        var nid = new NativeMethods.NotifyIconData
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            HWnd = _hwnd,
            UId = 1,
            UFlags = NifMessage | NifIcon | NifTip,
            UCallbackMessage = WmTrayIcon,
            HIcon = _hIcon,
            SzTip = "Auto Center"
        };

        _isIconAdded = NativeMethods.Shell_NotifyIcon(NidAdd, ref nid);
        IconVisible = _isIconAdded;
    }

    private static IntPtr LoadAppIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AutoCenter.ico");
        if (File.Exists(iconPath))
        {
            var cx = NativeMethods.GetSystemMetrics(NativeMethods.SmCxSmIcon);
            var cy = NativeMethods.GetSystemMetrics(NativeMethods.SmCySmIcon);
            var handle = NativeMethods.LoadImage(
                IntPtr.Zero,
                iconPath,
                NativeMethods.ImageIcon,
                cx,
                cy,
                NativeMethods.LrLoadFromFile | NativeMethods.LrDefaultSize);
            if (handle != IntPtr.Zero)
            {
                return handle;
            }
        }

        return NativeMethods.LoadIcon(IntPtr.Zero, (IntPtr)32512);
    }

    private void RemoveIcon()
    {
        if (!_isIconAdded) return;

        var nid = new NativeMethods.NotifyIconData
        {
            CbSize = (uint)Marshal.SizeOf<NativeMethods.NotifyIconData>(),
            HWnd = _hwnd,
            UId = 1
        };

        NativeMethods.Shell_NotifyIcon(NidDelete, ref nid);
        _isIconAdded = false;
        IconVisible = false;

        if (_hIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }
    }

    private static class NativeMethods
    {
        public const int GwlpWndProc = -4;
        public const uint ImageIcon = 1;
        public const uint LrLoadFromFile = 0x00000010;
        public const uint LrDefaultSize = 0x00000040;
        public const int SmCxSmIcon = 49;
        public const int SmCySmIcon = 50;

        public delegate IntPtr WndProc(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct NotifyIconData
        {
            public uint CbSize;
            public IntPtr HWnd;
            public uint UId;
            public uint UFlags;
            public uint UCallbackMessage;
            public IntPtr HIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string SzTip;
            public uint DwState;
            public uint DwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string SzInfo;
            public uint UTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string SzInfoTitle;
            public uint DwInfoFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MinMaxInfo
        {
            public Point PtReserved;
            public Point PtMaxSize;
            public Point PtMaxPosition;
            public Point PtMinTrackSize;
            public Point PtMaxTrackSize;
        }

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(IntPtr hwnd);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern bool Shell_NotifyIcon(uint dwMessage, [In] ref NotifyIconData lpData);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, IntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        public static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out Point lpPoint);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
