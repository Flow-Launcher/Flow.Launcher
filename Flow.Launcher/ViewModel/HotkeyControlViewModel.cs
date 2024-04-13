using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

        private void SetMessage(string messageKey, bool error)
        {
            Message = InternationalizationManager.Instance.GetTranslation(messageKey);
            MessageColor = error ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
            MessageVisibility = true;
        }

        public HotkeyControlViewModel(string hotkey = "",
            string defaultHotkey = "",
            bool validateKeyGesture = true,
            Action<HotkeyModel>? hotkeyDelegate = null)
        {
            Hotkey = hotkey;
            CurrentHotkey = new HotkeyModel(hotkey);
            DefaultHotkey = defaultHotkey;
            ValidateKeyGesture = validateKeyGesture;
            HotkeyDelegate = hotkeyDelegate;

            keysToDisplay = new(() =>
            {
                var collection = new ObservableCollection<string>();

                if (string.IsNullOrEmpty(Hotkey))
                {
                    Hotkey = DefaultHotkey;
                }

                SetKeysToDisplay(collection, Hotkey?.Split(KeySeparator));
                return collection;
            });
        }


        private string EmptyHotkeyKey = "none";
        public string EmptyHotkey => InternationalizationManager.Instance.GetTranslation(EmptyHotkeyKey);
        private const string KeySeparator = " + ";

        private Lazy<ObservableCollection<string>> keysToDisplay;

        public ObservableCollection<string> KeysToDisplay => keysToDisplay.Value;


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

            try
            {
                var converter = new KeyGestureConverter();
                var key = (KeyGesture)converter.ConvertFromString(CurrentHotkey.ToString())!;
            }
            catch (Exception e) when (e is NotSupportedException or InvalidEnumArgumentException)
            {
                SetMessage("Hotkey Invalid", true);
                CurrentHotkey = new HotkeyModel(Hotkey);
                SetKeysToDisplay(KeysToDisplay, CurrentHotkey);
                return;
            }
            
            Hotkey = CurrentHotkey.ToString();

            await SetHotkeyAsync(CurrentHotkey, true);
        }


        [RelayCommand]
        private async Task ResetToDefaultAsync()
        {
            Recording = false;
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.RemoveHotkey(Hotkey);
            Hotkey = DefaultHotkey;
            CurrentHotkey = new HotkeyModel(Hotkey);

            SetKeysToDisplay(KeysToDisplay, CurrentHotkey);

            await SetHotkeyAsync(Hotkey);
        }

        public bool ValidateKeyGesture { get; set; }
        public Action<HotkeyModel>? HotkeyDelegate { get; set; }

        private async Task SetHotkeyAsync(HotkeyModel keyModel, bool triggerValidate = true)
        {
            // tbHotkey.Text = keyModel.ToString();
            // tbHotkey.Select(tbHotkey.Text.Length, 0);

            if (triggerValidate)
            {
                bool hotkeyAvailable = CheckHotkeyAvailability(keyModel, ValidateKeyGesture);
                SetMessage(hotkeyAvailable ? "success" : "hotkeyUnavailable", !hotkeyAvailable);
                HotkeyDelegate?.Invoke(keyModel);

                if (CurrentHotkeyAvailable)
                {
                    Hotkey = keyModel.ToString();
                    SetKeysToDisplay(KeysToDisplay, CurrentHotkey);
                }
            }
            else
            {
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
            SetKeysToDisplay(KeysToDisplay, new List<string>());
        }

        private void SetKeysToDisplay(ICollection<string> container, HotkeyModel hotkey)
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

        private void SetKeysToDisplay(ICollection<string> container, ICollection<string>? keys)
        {
            container.Clear();

            if (keys == null)
            {
                return;
            }

            foreach (var key in keys)
            {
                container.Add(key);
            }

            if (!keys.Any())
            {
                container.Add(EmptyHotkey);
            }
        }
        
        public Task RegisterHotkey()
        {
            return SetHotkeyAsync(Hotkey, true);
        }

        public Task SetHotkeyAsync(string? keyStr, bool triggerValidate = true)
        {
            return SetHotkeyAsync(new HotkeyModel(keyStr), triggerValidate);
        }

        public void KeyDown(HotkeyModel hotkeyModel)
        {
            CurrentHotkey = hotkeyModel;
            SetKeysToDisplay(KeysToDisplay, CurrentHotkey);
        }
    }
}
