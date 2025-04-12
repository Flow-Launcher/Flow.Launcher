using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Logger;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;

namespace Flow.Launcher.Helper;

public class AutoStartup
{
    private const string StartupPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string LogonTaskName = $"{Constant.FlowLauncher} Startup";
    private const string LogonTaskDesc = $"{Constant.FlowLauncher} Auto Startup";

    public static bool IsEnabled
    {
        get
        {
            // Check if logon task is enabled
            if (CheckLogonTask())
            {
                return true;
            }

            // Check if registry is enabled
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
                var path = key?.GetValue(Constant.FlowLauncher) as string;
                return path == Constant.ExecutablePath;
            }
            catch (Exception e)
            {
                Log.Error("AutoStartup", $"Ignoring non-critical registry error (querying if enabled): {e}");
            }

            return false;
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
                var action = task.Definition.Actions.FirstOrDefault()!.ToString().Trim();
                if (!Constant.ExecutablePath.Equals(action, StringComparison.OrdinalIgnoreCase) && !File.Exists(action))
                {
                    UnscheduleLogonTask();
                    ScheduleLogonTask();
                }

                return true;
            }
            catch (Exception e)
            {
                Log.Error("AutoStartup", $"Failed to check logon task: {e}");
            }
        }

        return false;
    }

    public static void DisableViaLogonTaskAndRegistry()
    {
        Disable(true);
        Disable(false);
    }

    public static void EnableViaLogonTask()
    {
        Enable(true);
    }

    public static void EnableViaRegistry()
    {
        Enable(false);
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
                using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
                key?.DeleteValue(Constant.FlowLauncher, false);
            }
        }
        catch (Exception e)
        {
            Log.Error("AutoStartup", $"Failed to disable auto-startup: {e}");
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
                using var key = Registry.CurrentUser.OpenSubKey(StartupPath, true);
                key?.SetValue(Constant.FlowLauncher, $"\"{Constant.ExecutablePath}\"");
            }
        }
        catch (Exception e)
        {
            Log.Error("AutoStartup", $"Failed to enable auto-startup: {e}");
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
            Log.Error("AutoStartup", $"Failed to schedule logon task: {e}");
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
            Log.Error("AutoStartup", $"Failed to unschedule logon task: {e}");
            return false;
        }
    }

    private static bool IsCurrentUserIsAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
