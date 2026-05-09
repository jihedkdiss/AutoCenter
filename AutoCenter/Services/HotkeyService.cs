using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using AutoCenter.Models;

namespace AutoCenter.Services;

public sealed class HotkeyService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeyup = 0x0106;

    private readonly Window _window;
    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private AppSettings _settings;

    private int _sequenceCounter;
    private long _lastKeyPressTicks;

    public HotkeyService(Window window)
    {
        _window = window;
        _proc = HookCallback;
        _settings = new AppSettings(); // Default, will be updated
    }

    public event EventHandler? HotkeyPressed;

    public void UpdateSettings(AppSettings settings)
    {
        _settings = settings;
        if (settings.CenterWithHotkey)
        {
            StartHook();
        }
        else
        {
            StopHook();
        }
    }

    private void StartHook()
    {
        if (_hookId != IntPtr.Zero) return;
        _hookId = SetHook(_proc);
    }

    private void StopHook()
    {
        if (_hookId == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    public bool Register(int modifiers, int virtualKey)
    {
        // This method is kept for compatibility with existing calls, 
        // but now the hook handles everything.
        StartHook();
        return true;
    }

    public void Unregister()
    {
        StopHook();
    }

    public void Dispose()
    {
        StopHook();
    }

    private IntPtr SetHook(NativeMethods.LowLevelKeyboardProc proc)
    {
        var hook = NativeMethods.SetWindowsHookEx(WhKeyboardLl, proc, NativeMethods.GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"[HotkeyService] SetWindowsHookEx failed with error {error}");
        }
        return hook;
    }

    private static int NormalizeModifierVk(int vkCode) => vkCode switch
    {
        0xA0 or 0xA1 => 0x10, // L/R Shift -> Shift
        0xA2 or 0xA3 => 0x11, // L/R Ctrl  -> Ctrl
        0xA4 or 0xA5 => 0x12, // L/R Alt   -> Alt
        _ => vkCode
    };

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WmKeyup || wParam == (IntPtr)WmSyskeyup))
        {
            int rawVk = Marshal.ReadInt32(lParam);
            int vkCode = NormalizeModifierVk(rawVk);

            if (_settings.UseHotkeySequence && vkCode == _settings.HotkeySequenceKey)
            {
                long currentTicks = DateTime.Now.Ticks;
                long elapsedMs = (currentTicks - _lastKeyPressTicks) / TimeSpan.TicksPerMillisecond;

                if (elapsedMs < _settings.HotkeySequenceTimeoutMs)
                {
                    _sequenceCounter++;
                }
                else
                {
                    _sequenceCounter = 1;
                }

                _lastKeyPressTicks = currentTicks;

                if (_sequenceCounter >= _settings.HotkeySequenceCount)
                {
                    _sequenceCounter = 0;
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                }
            }
            else if (!_settings.UseHotkeySequence && vkCode == _settings.HotkeyVirtualKey)
            {
                // Simple modifier check for standard hotkey mode
                bool alt = (NativeMethods.GetKeyState(0x12) & 0x8000) != 0;
                bool ctrl = (NativeMethods.GetKeyState(0x11) & 0x8000) != 0;
                bool shift = (NativeMethods.GetKeyState(0x10) & 0x8000) != 0;
                bool win = (NativeMethods.GetKeyState(0x5B) & 0x8000) != 0 || (NativeMethods.GetKeyState(0x5C) & 0x8000) != 0;

                int currentModifiers = 0;
                if (alt) currentModifiers |= 0x0001;
                if (ctrl) currentModifiers |= 0x0002;
                if (shift) currentModifiers |= 0x0004;
                if (win) currentModifiers |= 0x0008;

                if (currentModifiers == _settings.HotkeyModifiers)
                {
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static class NativeMethods
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);
    }
}
