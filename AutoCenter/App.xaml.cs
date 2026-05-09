using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using WinRT.Interop;

namespace AutoCenter;

public partial class App : Application
{
    private MainWindow? _window;
    private readonly string _crashLogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AutoCenter",
        "crash.log");

    public App()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_crashLogPath)!);
        UnhandledException += OnUnhandledException;
        InitializeComponent();

        AppInstance.GetCurrent().Activated += OnAppInstanceActivated;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();

        var startMinimized =
            Environment.GetCommandLineArgs().Contains("--minimized")
            || AppInstance.GetCurrent().GetActivatedEventArgs()?.Kind == ExtendedActivationKind.StartupTask;

        if (!startMinimized)
        {
            _window.Activate();
        }
    }

    private void OnAppInstanceActivated(object? sender, AppActivationArguments args)
    {
        if (_window is null)
        {
            return;
        }

        _window.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window is null)
            {
                return;
            }

            var hwnd = WindowNative.GetWindowHandle(_window);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            appWindow.Show();
            _window.Activate();
            NativeMethods.SetForegroundWindow(hwnd);
        });
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        File.AppendAllText(_crashLogPath, $"{DateTime.Now:u} {e.Exception}\n\n");
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
