using System;
using System.Threading;
using AutoCenter.Services;
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

        // Launch through a registered elevated task before claiming the regular instance key.
        var startMinimized = args.Contains("--minimized") ||
            AppInstance.GetCurrent().GetActivatedEventArgs()?.Kind == ExtendedActivationKind.StartupTask;
        if (new SettingsStore().Load().AlwaysRunAsAdministrator)
        {
            if (ElevationService.IsElevated)
            {
                ScheduledElevationService.EnsureTasks();
            }
            else if (ScheduledElevationService.TryRun(startMinimized) ||
                     ElevationService.RelaunchAsAdmin(startMinimized ? "--minimized" : null))
            {
                return 0;
            }
        }

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
        var keyInstance = AppInstance.FindOrRegisterForKey(
            ElevationService.IsElevated ? "AutoCenter.AdminInstance" : "AutoCenter.SingleInstance");
        if (keyInstance.IsCurrent)
        {
            return false;
        }

        var activatedArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        keyInstance.RedirectActivationToAsync(activatedArgs).AsTask().Wait();
        return true;
    }
}
