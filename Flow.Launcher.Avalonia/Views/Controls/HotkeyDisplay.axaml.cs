using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Infrastructure.Hotkey;
using System.Collections.ObjectModel;

namespace Flow.Launcher.Avalonia.Views.Controls;

/// <summary>
/// A read-only control that displays a hotkey as a series of key badges.
/// Unlike HotkeyControl, this is not editable.
/// </summary>
public partial class HotkeyDisplay : UserControl
{
    public static readonly DirectProperty<HotkeyDisplay, string> KeysProperty =
        AvaloniaProperty.RegisterDirect<HotkeyDisplay, string>(
            nameof(Keys),
            o => o.Keys,
            (o, v) => o.Keys = v);

    private string _keys = string.Empty;
    public string Keys
    {
        get => _keys;
        set
        {
            if (SetAndRaise(KeysProperty, ref _keys, value))
            {
                UpdateKeysDisplay();
            }
        }
    }

    public ObservableCollection<string> KeysToDisplay { get; } = new();

    public HotkeyDisplay()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateKeysDisplay()
    {
        KeysToDisplay.Clear();

        if (string.IsNullOrEmpty(Keys))
        {
            return;
        }

        // Handle multiple hotkeys separated by space (e.g., "Ctrl+[ Ctrl+]")
        var hotkeys = Keys.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
        foreach (var hotkey in hotkeys)
        {
            try
            {
                var model = new HotkeyModel(hotkey);
                foreach (var key in model.EnumerateDisplayKeys())
                {
                    KeysToDisplay.Add(key);
                }
            }
            catch
            {
                // If parsing fails, just display the raw string
                KeysToDisplay.Add(hotkey);
            }
        }
    }
}
