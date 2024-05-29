#nullable enable

using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;

namespace Flow.Launcher
{
    public partial class HotkeyControl
    {
        public IHotkeySettings HotkeySettings {
            get { return (IHotkeySettings)GetValue(HotkeySettingsProperty); }
            set { SetValue(HotkeySettingsProperty, value); }
        }

        public static readonly DependencyProperty HotkeySettingsProperty = DependencyProperty.Register(
            nameof(HotkeySettings),
            typeof(IHotkeySettings),
            typeof(HotkeyControl),
            new PropertyMetadata()
        );
        public string WindowTitle {
            get { return (string)GetValue(WindowTitleProperty); }
            set { SetValue(WindowTitleProperty, value); }
        }

        public static readonly DependencyProperty WindowTitleProperty = DependencyProperty.Register(
            nameof(WindowTitle),
            typeof(string),
            typeof(HotkeyControl),
            new PropertyMetadata(string.Empty)
        );

        /// <summary>
        /// Designed for Preview Hotkey and KeyGesture.
        /// </summary>
        public static readonly DependencyProperty ValidateKeyGestureProperty = DependencyProperty.Register(
            nameof(ValidateKeyGesture),
            typeof(bool),
            typeof(HotkeyControl),
            new PropertyMetadata(default(bool))
        );

        public bool ValidateKeyGesture
        {
            get { return (bool)GetValue(ValidateKeyGestureProperty); }
            set { SetValue(ValidateKeyGestureProperty, value); }
        }

        public static readonly DependencyProperty DefaultHotkeyProperty = DependencyProperty.Register(
            nameof(DefaultHotkey),
            typeof(string),
            typeof(HotkeyControl),
            new PropertyMetadata(default(string))
        );

        public string DefaultHotkey
        {
            get { return (string)GetValue(DefaultHotkeyProperty); }
            set { SetValue(DefaultHotkeyProperty, value); }
        }

        private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HotkeyControl hotkeyControl)
            {
                return;
            }

            hotkeyControl.SetKeysToDisplay(new HotkeyModel(hotkeyControl.Hotkey));
            hotkeyControl.CurrentHotkey = new HotkeyModel(hotkeyControl.Hotkey);
        }


        public static readonly DependencyProperty ChangeHotkeyProperty = DependencyProperty.Register(
            nameof(ChangeHotkey),
            typeof(ICommand),
            typeof(HotkeyControl),
            new PropertyMetadata(default(ICommand))
        );

        public ICommand? ChangeHotkey
        {
            get { return (ICommand)GetValue(ChangeHotkeyProperty); }
            set { SetValue(ChangeHotkeyProperty, value); }
        }


        public static readonly DependencyProperty HotkeyProperty = DependencyProperty.Register(
            nameof(Hotkey),
            typeof(string),
            typeof(HotkeyControl),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged)
        );

        public string Hotkey
        {
            get { return (string)GetValue(HotkeyProperty); }
            set { SetValue(HotkeyProperty, value); }
        }

        public HotkeyControl()
        {
            InitializeComponent();

            HotkeyList.ItemsSource = KeysToDisplay;
            SetKeysToDisplay(CurrentHotkey);
        }

        private static bool CheckHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture) =>
            hotkey.Validate(validateKeyGesture) && HotKeyMapper.CheckAvailability(hotkey);

        public string EmptyHotkey => InternationalizationManager.Instance.GetTranslation("none");

        public ObservableCollection<string> KeysToDisplay { get; set; } = new();

        public HotkeyModel CurrentHotkey { get; private set; } = new(false, false, false, false, Key.None);


        public void GetNewHotkey(object sender, RoutedEventArgs e)
        {
            OpenHotkeyDialog();
        }

        private async Task OpenHotkeyDialog()
        {
            if (!string.IsNullOrEmpty(Hotkey))
            {
                HotKeyMapper.RemoveHotkey(Hotkey);
            }

            var dialog = new HotkeyControlDialog(Hotkey, DefaultHotkey, HotkeySettings, WindowTitle);
            await dialog.ShowAsync();
            switch (dialog.ResultType)
            {
                case HotkeyControlDialog.EResultType.Cancel:
                    SetHotkey(Hotkey);
                    return;
                case HotkeyControlDialog.EResultType.Save:
                    SetHotkey(dialog.ResultValue);
                    break;
                case HotkeyControlDialog.EResultType.Delete:
                    Delete();
                    break;
            }
        }


        private void SetHotkey(HotkeyModel keyModel, bool triggerValidate = true)
        {
            if (triggerValidate)
            {
                bool hotkeyAvailable = CheckHotkeyAvailability(keyModel, ValidateKeyGesture);

                if (!hotkeyAvailable)
                {
                    return;
                }

                Hotkey = keyModel.ToString();
                SetKeysToDisplay(CurrentHotkey);
                ChangeHotkey?.Execute(keyModel);
            }
            else
            {
                Hotkey = keyModel.ToString();
                ChangeHotkey?.Execute(keyModel);
            }
        }

        public void Delete()
        {
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.RemoveHotkey(Hotkey);
            Hotkey = "";
            SetKeysToDisplay(new HotkeyModel(false, false, false, false, Key.None));
        }

        private void SetKeysToDisplay(HotkeyModel? hotkey)
        {
            KeysToDisplay.Clear();

            if (hotkey == null || hotkey == default(HotkeyModel))
            {
                KeysToDisplay.Add(EmptyHotkey);
                return;
            }

            foreach (var key in hotkey.Value.EnumerateDisplayKeys()!)
            {
                KeysToDisplay.Add(key);
            }
        }

        public void SetHotkey(string? keyStr, bool triggerValidate = true)
        {
            SetHotkey(new HotkeyModel(keyStr), triggerValidate);
        }
    }
}
