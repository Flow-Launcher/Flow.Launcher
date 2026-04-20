using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using Flow.Launcher.Infrastructure;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace Flow.Launcher.Avalonia.Helper;

internal static class AutoStartup
{
    private static readonly string ClassName = nameof(AutoStartup);

    private const string StartupPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string LogonTaskName = $"{Constant.FlowLauncher} Startup";
    private const string LogonTaskDesc = $"{Constant.FlowLauncher} Auto Startup";

    internal static void CheckIsEnabled(bool useLogonTaskForStartup)
    {
        var logonTaskEnabled = CheckLogonTask();
        var registryEnabled = CheckRegistry();

        if (useLogonTaskForStartup)
        {
            if (!logonTaskEnabled)
            {
                Enable(true);
            }

            if (registryEnabled)
            {
                Disable(false);
            }
        }
        else
        {
            if (!registryEnabled)
            {
                Enable(false);
            }

            if (logonTaskEnabled)
            {
                Disable(true);
            }
        }
    }

    internal static void DisableViaLogonTaskAndRegistry()
    {
        Disable(true);
        Disable(false);
    }

    internal static void ChangeToViaLogonTask()
    {
        Disable(false);
        Enable(true);
    }

    internal static void ChangeToViaRegistry()
    {
        Disable(true);
        Enable(false);
    }

    private static bool CheckLogonTask()
    {
        using var taskService = new TaskService();
        var task = taskService.RootFolder.AllTasks.FirstOrDefault(t => t.Name == LogonTaskName);
        if (task == null)
        {
            return false;
        }

        try
        {
            if (task.Definition.Actions.FirstOrDefault() is Microsoft.Win32.TaskScheduler.Action taskAction)
            {
                var action = taskAction.ToString().Trim();
                var needsRecreation = !action.Equals(Constant.ExecutablePath, StringComparison.OrdinalIgnoreCase)
                    || task.Definition.Settings.Priority != ProcessPriorityClass.Normal;

                if (needsRecreation)
                {
                    UnscheduleLogonTask();
                    ScheduleLogonTask();
                }
            }

            return true;
        }
        catch (Exception e)
        {
            App.API?.LogError(ClassName, $"Failed to check logon task: {e}");
            throw;
        }
    }

    private static bool CheckRegistry()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
            if (key == null)
            {
                return false;
            }

            var action = (key.GetValue(Constant.FlowLauncher) as string) ?? string.Empty;
            if (!action.Equals(Constant.ExecutablePath, StringComparison.OrdinalIgnoreCase)
                && !action.Equals($"\"{Constant.ExecutablePath}\"", StringComparison.OrdinalIgnoreCase))
            {
                UnscheduleRegistry();
                ScheduleRegistry();
            }

            return true;
        }
        catch (Exception e)
        {
            App.API?.LogError(ClassName, $"Failed to check registry: {e}");
            throw;
        }
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
            App.API?.LogError(ClassName, $"Failed to disable auto-startup: {e}");
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
            App.API?.LogError(ClassName, $"Failed to enable auto-startup: {e}");
            throw;
        }
    }

    private static bool ScheduleLogonTask()
    {
        using var td = TaskService.Instance.NewTask();
        td.RegistrationInfo.Description = LogonTaskDesc;
        td.Triggers.Add(new LogonTrigger { UserId = WindowsIdentity.GetCurrent().Name, Delay = TimeSpan.FromSeconds(2) });
        td.Actions.Add(Constant.ExecutablePath);

        if (IsCurrentUserAdmin())
        {
            td.Principal.RunLevel = TaskRunLevel.Highest;
        }

        td.Settings.StopIfGoingOnBatteries = false;
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        td.Settings.Priority = ProcessPriorityClass.Normal;

        try
        {
            TaskService.Instance.RootFolder.RegisterTaskDefinition(LogonTaskName, td);
            return true;
        }
        catch (Exception e)
        {
            App.API?.LogError(ClassName, $"Failed to schedule logon task: {e}");
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
            App.API?.LogError(ClassName, $"Failed to unschedule logon task: {e}");
            return false;
        }
    }

    private static bool IsCurrentUserAdmin()
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
