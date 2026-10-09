using System;
using System.Linq;
using System.Windows.Forms;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.DialogJump;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class GeneralSettingsViewModel
{
    partial void SetupDialogJump(bool enabled) => DialogJump.SetupDialogJump(enabled);

    public bool KoreanIMERegistryKeyExists
    {
        get
        {
            var registryKeyExists = Win32Helper.IsKoreanIMEExist();
            var koreanLanguageInstalled = InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().Any(lang => lang.Culture.Name.StartsWith("ko", StringComparison.OrdinalIgnoreCase));
            var isWindows11 = Win32Helper.IsWindows11();
            return (isWindows11 && koreanLanguageInstalled) || registryKeyExists;
        }
    }

    public bool LegacyKoreanIMEEnabled
    {
        get => Win32Helper.IsLegacyKoreanIMEEnabled();
        set
        {
            if (Win32Helper.SetLegacyKoreanIMEEnabled(value))
            {
                OnPropertyChanged();
            }
            else
            {
                App.API?.ShowMsgError(_i18n.GetTranslation("KoreanImeSettingChangeFailTitle"), _i18n.GetTranslation("KoreanImeSettingChangeFailSubTitle"));
            }
        }
    }
}
