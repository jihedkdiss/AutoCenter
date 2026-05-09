using System;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace AutoCenter;

public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        if (DecideRedirection())
        {
            return 0;
        }

        Application.Start(p =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }

    private static bool DecideRedirection()
    {
        var keyInstance = AppInstance.FindOrRegisterForKey("AutoCenter.SingleInstance");
        if (keyInstance.IsCurrent)
        {
            return false;
        }

        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        keyInstance.RedirectActivationToAsync(activatedArgs).AsTask().Wait();
        return true;
    }
}
