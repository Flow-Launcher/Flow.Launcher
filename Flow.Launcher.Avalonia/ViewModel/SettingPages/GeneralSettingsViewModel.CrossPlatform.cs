namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class GeneralSettingsViewModel
{
    // The legacy Korean IME toggle is a Windows registry setting; false hides the option in the view.
    public bool KoreanIMERegistryKeyExists => false;

    // Unreachable from the UI while KoreanIMERegistryKeyExists is false; kept for the shared view bindings.
    public bool LegacyKoreanIMEEnabled
    {
        get => false;
        set { }
    }
}
