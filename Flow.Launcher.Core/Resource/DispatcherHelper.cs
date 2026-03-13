using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Flow.Launcher.Core.Resource;

#pragma warning disable VSTHRD001 // Avoid legacy thread switching APIs

public static class DispatcherHelper
{
    public static void Invoke(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        Invoke(Application.Current?.Dispatcher, action, priority);
    }

    public static T Invoke<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        return Invoke(Application.Current?.Dispatcher, func, priority);
    }

    public static void Invoke(Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher == null) return;
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action, priority);
        }
    }

    public static T Invoke<T>(Dispatcher dispatcher, Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher == null) return default;
        if (dispatcher.CheckAccess())
        {
            return func();
        }
        else
        {
            return dispatcher.Invoke(func, priority);
        }
    }

    public static async Task InvokeAsync(Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        await InvokeAsync(Application.Current?.Dispatcher, action, priority);
    }

    public static async Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        return await InvokeAsync(Application.Current?.Dispatcher, func, priority);
    }

    public static async Task InvokeAsync(Func<Task> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        await InvokeAsync(Application.Current?.Dispatcher, func, priority);
    }

    public static async Task InvokeAsync(Dispatcher dispatcher, Action action, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher == null) return;
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            await dispatcher.InvokeAsync(action, priority);
        }
    }

    public static async Task<T> InvokeAsync<T>(Dispatcher dispatcher, Func<T> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher == null) return default;
        if (dispatcher.CheckAccess())
        {
            return func();
        }
        else
        {
            return await dispatcher.InvokeAsync(func, priority);
        }
    }

    public static async Task InvokeAsync(Dispatcher dispatcher, Func<Task> func, DispatcherPriority priority = DispatcherPriority.Normal)
    {
        if (dispatcher == null) return;
        if (dispatcher.CheckAccess())
        {
            await func();
        }
        else
        {
            var task = await dispatcher.InvokeAsync(func, priority);
            await task;
        }
    }
}
