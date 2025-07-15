using System;
using System.Linq;
using System.Security.Principal;
using Flow.Launcher.Infrastructure;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

#nullable enable

namespace Flow.Launcher.Helper;

public class AutoStartup
{
    private static readonly string ClassName = nameof(AutoStartup);

    private const string StartupPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string LogonTaskName = $"{Constant.FlowLauncher} Startup";
    private const string LogonTaskDesc = $"{Constant.FlowLauncher} Auto Startup";

    public static void CheckIsEnabled(bool useLogonTaskForStartup)
    {
        // We need to check both because if both of them are enabled,
        // Hide Flow Launcher on startup will not work since the later one will trigger main window show event
        var logonTaskEnabled = CheckLogonTask();
        var registryEnabled = CheckRegistry();
        if (useLogonTaskForStartup)
        {
            // Enable logon task
            if (!logonTaskEnabled)
            {
                Enable(true);
            }
            // Disable registry
            if (registryEnabled)
            {
                Disable(false);
            }
        }
        else
        {
            // Enable registry
            if (!registryEnabled)
            {
                Enable(false);
            }
            // Disable logon task
            if (logonTaskEnabled)
            {
                Disable(true);
            }
        }
    }

    private static bool CheckLogonTask()
    {
        using var taskService = new TaskService();
        var task = taskService.RootFolder.AllTasks.FirstOrDefault(t => t.Name == LogonTaskName);
        if (task != null)
        {
            try
            {
                // Check if the action is the same as the current executable path
                // If not, we need to unschedule and reschedule the task
                if (task.Definition.Actions.FirstOrDefault() is Microsoft.Win32.TaskScheduler.Action taskAction)
                {
                    var action = taskAction.ToString().Trim();
                    if (!action.Equals(Constant.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                    {
                        UnscheduleLogonTask();
                        ScheduleLogonTask();
                    }
                }

                return true;
            }
            catch (Exception e)
            {
                App.API.LogError(ClassName, $"Failed to check logon task: {e}");
                throw; // Throw exception so that App.AutoStartup can show error message
            }
        }

        return false;
    }

    private static bool CheckRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
            if (key != null)
            {
                // Check if the action is the same as the current executable path
                // If not, we need to unschedule and reschedule the task
                var action = (key.GetValue(Constant.FlowLauncher) as string) ?? string.Empty;
                if (!action.Equals(Constant.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    UnscheduleRegistry();
                    ScheduleRegistry();
                }

                return true;
            }

            return false;
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Failed to check registry: {e}");
            throw; // Throw exception so that App.AutoStartup can show error message
        }
    }

    public static void DisableViaLogonTaskAndRegistry()
    {
        Disable(true);
        Disable(false);
    }

    public static void ChangeToViaLogonTask()
    {
        Disable(false);
        Enable(true);
    }

    public static void ChangeToViaRegistry()
    {
        Disable(true);
        Enable(false);
    }

    private static void Disable(bool logonTask)
    {
        try
        {
            if (logonTask)
            {
                UnscheduleLogonTask();
            }
            else
            {
                UnscheduleRegistry();
            }
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Failed to disable auto-startup: {e}");
            throw;
        }
    }

    private static void Enable(bool logonTask)
    {
        try
        {
            if (logonTask)
            {
                ScheduleLogonTask();
            }
            else
            {
                ScheduleRegistry();
            }
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Failed to enable auto-startup: {e}");
            throw;
        }
    }

    private static bool ScheduleLogonTask()
    {
        using var td = TaskService.Instance.NewTask();
        td.RegistrationInfo.Description = LogonTaskDesc;
        td.Triggers.Add(new LogonTrigger { UserId = WindowsIdentity.GetCurrent().Name, Delay = TimeSpan.FromSeconds(2) });
        td.Actions.Add(Constant.ExecutablePath);

        if (IsCurrentUserIsAdmin())
        {
            td.Principal.RunLevel = TaskRunLevel.Highest;
        }

        td.Settings.StopIfGoingOnBatteries = false;
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.ExecutionTimeLimit = TimeSpan.Zero;

        try
        {
            TaskService.Instance.RootFolder.RegisterTaskDefinition(LogonTaskName, td);
            return true;
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Failed to schedule logon task: {e}");
            return false;
        }
    }

    private static bool UnscheduleLogonTask()
    {
        using var taskService = new TaskService();
        try
        {
            taskService.RootFolder.DeleteTask(LogonTaskName);
            return true;
        }
        catch (Exception e)
        {
            App.API.LogError(ClassName, $"Failed to unschedule logon task: {e}");
            return false;
        }
    }

    private static bool IsCurrentUserIsAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool UnscheduleRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
        key?.DeleteValue(Constant.FlowLauncher, false);
        return true;
    }

    private static bool ScheduleRegistry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
        key?.SetValue(Constant.FlowLauncher, $"\"{Constant.ExecutablePath}\"");
        return true;
    }
}
