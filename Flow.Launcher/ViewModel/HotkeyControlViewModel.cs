using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using JetBrains.Annotations;

#nullable enable

namespace Flow.Launcher.ViewModel
{
    public partial class HotkeyControlViewModel : BaseModel
    {
        private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture) =>
            hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);

        public string? Message { get; set; }
        public bool MessageVisibility { get; set; }
        public SolidColorBrush? MessageColor { get; set; }


        public bool CurrentHotkeyAvailable { get; private set; }

        public string DefaultHotkey { get; init; }

        private void SetMessage(bool hotkeyAvailable)
        {
            Message = hotkeyAvailable
                ? InternationalizationManager.Instance.GetTranslation("success")
                : InternationalizationManager.Instance.GetTranslation("hotkeyUnavailable");
            MessageColor = hotkeyAvailable ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
            MessageVisibility = true;
        }

        public HotkeyControlViewModel(string hotkey = "",
            string defaultHotkey = "",
            bool validateKeyGesture = true,
            Action<HotkeyModel>? hotkeyDelegate = null)
        {
            Hotkey = hotkey;
            DefaultHotkey = defaultHotkey;
            ValidateKeyGesture = validateKeyGesture;
            HotkeyDelegate = hotkeyDelegate;
            EmptyKeyValue = InternationalizationManager.Instance.GetTranslation("empty");
            KeysToDisplay = new ObservableCollection<string> { EmptyKeyValue };

            if (string.IsNullOrEmpty(Hotkey))
            {
                Hotkey = DefaultHotkey;
            }

            SetKeysToDisplay(Hotkey?.Split(KeySeparator));
        }


        private string EmptyKeyValue = "<empty>";
        private const string KeySeparator = " + ";

        public ObservableCollection<string> KeysToDisplay { get; }


        public HotkeyModel CurrentHotkey { get; private set; }

        public string Hotkey { get; set; }


        public bool Recording { get; set; }

        [RelayCommand]
        public async Task StartRecordingAsync()
        {
            if (!Recording)
            {
                return;
            }

            if (!string.IsNullOrEmpty(Hotkey))
            {
                HotKeyMapper.RemoveHotkey(Hotkey);
            }
            /* 1. Key Recording Start */
            /* 2. Key Display area clear
             * 3. Key Display when typing*/
        }

        [RelayCommand]
        private async Task StopRecordingAsync()
        {
            Recording = false;

            if (KeysToDisplay.Count == 0 || (KeysToDisplay.Count == 1 && KeysToDisplay[0] == EmptyKeyValue))
            {
                return;
            }

            await SetHotkeyAsync(CurrentHotkey, true);
        }


        [RelayCommand]
        private async Task ResetToDefaultAsync()
        {
            Recording = false;
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.RemoveHotkey(Hotkey);
            Hotkey = DefaultHotkey;

            KeysToDisplay.Clear();

            foreach (var key in Hotkey.Split(KeySeparator))
            {
                KeysToDisplay.Add(key);
            }

            await SetHotkeyAsync(Hotkey);
        }

        public bool ValidateKeyGesture { get; set; }
        public Action<HotkeyModel> HotkeyDelegate { get; set; }

        private async Task SetHotkeyAsync(HotkeyModel keyModel, bool triggerValidate = true)
        {
            // tbHotkey.Text = keyModel.ToString();
            // tbHotkey.Select(tbHotkey.Text.Length, 0);

            if (triggerValidate)
            {
                bool hotkeyAvailable = CheckHotkeyAvailability(keyModel, ValidateKeyGesture);
                CurrentHotkeyAvailable = hotkeyAvailable;
                SetMessage(hotkeyAvailable);
                HotkeyDelegate?.Invoke(keyModel);

                if (CurrentHotkeyAvailable)
                {
                    CurrentHotkey = keyModel;
                    Hotkey = keyModel.ToString();
                    SetKeysToDisplay(Hotkey.Split(KeySeparator));
                }
            }
            else
            {
                CurrentHotkey = keyModel;
                Hotkey = keyModel.ToString();
            }
        }

        [RelayCommand]
        public async Task DeleteAsync()
        {
            Recording = false;
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.RemoveHotkey(Hotkey);
            Hotkey = "";
            SetKeysToDisplay(new List<string>());
        }

        private void SetKeysToDisplay(HotkeyModel hotkey)
        {
            KeysToDisplay.Clear();
            if (hotkey.Alt)
            {
                KeysToDisplay.Add("Alt");
            }

            if (hotkey.Ctrl)
            {
                KeysToDisplay.Add("Ctrl");
            }

            if (hotkey.Shift)
            {
                KeysToDisplay.Add("Shift");
            }

            if (hotkey.Win)
            {
                KeysToDisplay.Add("Win");
            }

            if (hotkey.CharKey != Key.None)
            {
                KeysToDisplay.Add(hotkey.CharKey.ToString());
            }
        }

        private void SetKeysToDisplay(ICollection<string>? keys)
        {
            KeysToDisplay.Clear();

            if (keys == null)
            {
                return;
            }

            foreach (var key in keys)
            {
                KeysToDisplay.Add(key);
            }

            if (!keys.Any())
            {
                KeysToDisplay.Add(EmptyKeyValue);
            }
        }

        public Task SetHotkeyAsync(string? keyStr, bool triggerValidate = true)
        {
            return SetHotkeyAsync(new HotkeyModel(keyStr), triggerValidate);
        }

        public void KeyDown(HotkeyModel hotkeyModel)
        {
            CurrentHotkey = hotkeyModel;
            SetKeysToDisplay(hotkeyModel.ToString().Split(KeySeparator));
        }
    }
}
