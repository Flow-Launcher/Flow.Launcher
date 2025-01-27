using System;
using System.Linq;
using System.Windows;

namespace Flow.Launcher.Helper;

public static class SingletonWindowOpener
{
    public static T Open<T>(params object[] args) where T : Window
    {
        var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.GetType() == typeof(T))
                     ?? (T)Activator.CreateInstance(typeof(T), args);

        // Fix UI bug
        // Add `window.WindowState = WindowState.Normal`
        // If only use `window.Show()`, Settings-window doesn't show when minimized in taskbar 
        // Not sure why this works tho
        // Probably because, when `.Show()` fails, `window.WindowState == Minimized` (not `Normal`) 
        // https://stackoverflow.com/a/59719760/4230390
        // Ensure the window is not minimized before showing it
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        // Ensure the window is visible
        if (!window.IsVisible)
        {
            window.Show();
        }
        else
        {
            window.Activate(); // Bring the window to the foreground if already open
        }

        window.Focus();

        return (T)window;
    }
}
