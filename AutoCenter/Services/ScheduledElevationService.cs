using System.Diagnostics;
using System.Xml.Linq;

namespace AutoCenter.Services;

public static class ScheduledElevationService
{
    private const string OpenTask = "AutoCenter Elevated Open";
    private const string MinimizedTask = "AutoCenter Elevated Minimized";
    private static readonly XNamespace TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    public static bool TryRun(bool minimized)
    {
        var taskName = minimized ? MinimizedTask : OpenTask;
        if (!MatchesCurrentExecutable(taskName, minimized)) return false;
        return RunSchtasks("/Run", "/TN", taskName).ExitCode == 0;
    }

    public static bool EnsureTasks()
    {
        if (!ElevationService.IsElevated) return false;
        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable)) return false;

        return EnsureTask(OpenTask, executable, false) &&
               EnsureTask(MinimizedTask, executable, true);
    }

    public static void RemoveTasks()
    {
        if (!ElevationService.IsElevated) return;
        RunSchtasks("/Delete", "/TN", OpenTask, "/F");
        RunSchtasks("/Delete", "/TN", MinimizedTask, "/F");
    }

    private static bool EnsureTask(string name, string executable, bool minimized)
    {
        if (MatchesCurrentExecutable(name, minimized)) return true;

        var user = System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrEmpty(user)) return false;

        var xml = new XDocument(
            new XElement(TaskNamespace + "Task",
                new XAttribute("version", "1.4"),
                new XElement(TaskNamespace + "RegistrationInfo",
                    new XElement(TaskNamespace + "Description", "Launch Auto Center with administrator rights on demand.")),
                new XElement(TaskNamespace + "Triggers"),
                new XElement(TaskNamespace + "Principals",
                    new XElement(TaskNamespace + "Principal",
                        new XAttribute("id", "Author"),
                        new XElement(TaskNamespace + "UserId", user),
                        new XElement(TaskNamespace + "LogonType", "InteractiveToken"),
                        new XElement(TaskNamespace + "RunLevel", "HighestAvailable"))),
                new XElement(TaskNamespace + "Settings",
                    new XElement(TaskNamespace + "MultipleInstancesPolicy", "Parallel"),
                    new XElement(TaskNamespace + "AllowStartOnDemand", "true"),
                    new XElement(TaskNamespace + "Enabled", "true"),
                    new XElement(TaskNamespace + "ExecutionTimeLimit", "PT0S")),
                new XElement(TaskNamespace + "Actions",
                    new XAttribute("Context", "Author"),
                    new XElement(TaskNamespace + "Exec",
                        new XElement(TaskNamespace + "Command", executable),
                        new XElement(TaskNamespace + "Arguments", minimized ? "--minimized" : string.Empty)))));

        var tempFile = Path.Combine(Path.GetTempPath(), $"AutoCenterTask-{Guid.NewGuid():N}.xml");
        try
        {
            xml.Save(tempFile);
            return RunSchtasks("/Create", "/TN", name, "/XML", tempFile, "/F").ExitCode == 0;
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    private static bool MatchesCurrentExecutable(string name, bool minimized)
    {
        var result = RunSchtasks("/Query", "/TN", name, "/XML");
        if (result.ExitCode != 0) return false;
        try
        {
            var document = XDocument.Parse(result.Output);
            var command = document.Descendants(TaskNamespace + "Command").FirstOrDefault()?.Value;
            var arguments = document.Descendants(TaskNamespace + "Arguments").FirstOrDefault()?.Value ?? string.Empty;
            var runLevel = document.Descendants(TaskNamespace + "RunLevel").FirstOrDefault()?.Value;
            var user = document.Descendants(TaskNamespace + "UserId").FirstOrDefault()?.Value;
            return string.Equals(command, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase) &&
                   arguments == (minimized ? "--minimized" : string.Empty) &&
                   runLevel == "HighestAvailable" &&
                   user == System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value;
        }
        catch
        {
            return false;
        }
    }

    private static (int ExitCode, string Output) RunSchtasks(params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("schtasks.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
            using var process = Process.Start(startInfo);
            if (process is null) return (-1, string.Empty);
            var output = process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output);
        }
        catch
        {
            return (-1, string.Empty);
        }
    }
}
