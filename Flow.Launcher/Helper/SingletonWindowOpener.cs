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
        window.WindowState = WindowState.Normal; 
        window.Show();
            
        window.Focus();

        return (T)window;
    }
}
