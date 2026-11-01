using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Plugin.Calculator.ViewModels;

public class SettingsViewModel : BaseModel
{
    private readonly Action _cancelPendingHistory;

    public SettingsViewModel(Settings settings, Action cancelPendingHistory = null)
    {
        Settings = settings;
        _cancelPendingHistory = cancelPendingHistory ?? (() => { });
    }

    public Settings Settings { get; }

    public static IEnumerable<int> MaxDecimalPlacesRange => Enumerable.Range(1, 20);

    public List<DecimalSeparatorLocalized> AllDecimalSeparator { get; } = DecimalSeparatorLocalized.GetValues();

    public DecimalSeparator SelectedDecimalSeparator
    {
        get => Settings.DecimalSeparator;
        set
        {
            if (Settings.DecimalSeparator != value)
            {
                Settings.DecimalSeparator = value;
                OnPropertyChanged();
            }
        }
    }

    public bool EnableHistory
    {
        get => Settings.EnableHistory;
        set
        {
            if (Settings.EnableHistory != value)
            {
                Settings.EnableHistory = value;
                if (!value)
                {
                    // Disabling history invalidates any pending debounced entry.
                    _cancelPendingHistory();
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowHistoryQueryWarning));
            }
        }
    }

    public List<HistoryCreationModeLocalized> AllHistoryCreationMode { get; } = HistoryCreationModeLocalized.GetValues();

    public HistoryCreationMode SelectedHistoryCreationMode
    {
        get => Settings.HistoryCreationMode;
        set
        {
            if (Settings.HistoryCreationMode != value)
            {
                Settings.HistoryCreationMode = value;
                if (value == HistoryCreationMode.OnEnter)
                {
                    // Pending entries are only created in OnQuery mode, so drop any leftover one.
                    _cancelPendingHistory();
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowHistoryQueryWarning));
            }
        }
    }

    public bool ShowHistoryQueryWarning => EnableHistory && SelectedHistoryCreationMode == HistoryCreationMode.OnQuery;
}
