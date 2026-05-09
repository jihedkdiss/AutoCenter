using Windows.ApplicationModel;

namespace AutoCenter.Services;

public static class StartupService
{
    private const string TaskId = "AutoCenterStartup";

    public static async Task SetStartupAsync(bool enable)
    {
        var task = await StartupTask.GetAsync(TaskId);

        if (enable)
        {
            if (task.State == StartupTaskState.Disabled)
            {
                await task.RequestEnableAsync();
            }
        }
        else if (task.State == StartupTaskState.Enabled)
        {
            task.Disable();
        }
    }

    public static async Task<bool> IsStartupEnabledAsync()
    {
        var task = await StartupTask.GetAsync(TaskId);
        return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
    }
}
