using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;

#nullable enable

namespace Flow.Launcher
{
    public partial class HotkeyControl
    {
        private HotkeyControlDialog hotkeyControlDialog;

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

            //hotkeyControl.SetKeysToDisplay(new HotkeyModel(hotkeyControl.Hotkey));
            //hotkeyControl.CurrentHotkey = new HotkeyModel(hotkeyControl.Hotkey);

            var hotkeyModel = new HotkeyModel(hotkeyControl.Hotkey);
            hotkeyControl.SetKeysToDisplay(hotkeyModel);
            hotkeyControl.CurrentHotkey = hotkeyModel;

            hotkeyControl.RefreshHotkeyInterface(hotkeyControl.Hotkey);
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

        public static readonly DependencyProperty TypeProperty = DependencyProperty.Register(
            nameof(Type),
            typeof(HotkeyType),
            typeof(HotkeyControl),
            new FrameworkPropertyMetadata(HotkeyType.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged)
        );

        public HotkeyType Type
        {
            get { return (HotkeyType)GetValue(TypeProperty); }
            set { SetValue(TypeProperty, value); }
        }

        public enum HotkeyType
        {
            None,
            // Custom query hotkeys
            CustomQueryHotkey,
            // Settings hotkeys
            Hotkey,
            PreviewHotkey,
            OpenContextMenuHotkey,
            SettingWindowHotkey,
            OpenHistoryHotkey,
            CycleHistoryUpHotkey,
            CycleHistoryDownHotkey,
            SelectPrevPageHotkey,
            SelectNextPageHotkey,
            AutoCompleteHotkey,
            AutoCompleteHotkey2,
            SelectPrevItemHotkey,
            SelectPrevItemHotkey2,
            SelectNextItemHotkey,
            SelectNextItemHotkey2,
            DialogJumpHotkey,
        }

        // We can initialize settings in static field because it has been constructed in App constuctor
        // and it will not construct settings instances twice
        private static readonly Settings _settings = Ioc.Default.GetRequiredService<Settings>();

        private string hotkey = string.Empty;
        public string Hotkey
        {
            get
            {
                return Type switch
                {
                    // Custom query hotkeys
                    HotkeyType.CustomQueryHotkey => hotkey,
                    // Settings hotkeys
                    HotkeyType.Hotkey => _settings.Hotkey,
                    HotkeyType.PreviewHotkey => _settings.PreviewHotkey,
                    HotkeyType.OpenContextMenuHotkey => _settings.OpenContextMenuHotkey,
                    HotkeyType.SettingWindowHotkey => _settings.SettingWindowHotkey,
                    HotkeyType.OpenHistoryHotkey => _settings.OpenHistoryHotkey,
                    HotkeyType.CycleHistoryUpHotkey => _settings.CycleHistoryUpHotkey,
                    HotkeyType.CycleHistoryDownHotkey => _settings.CycleHistoryDownHotkey,
                    HotkeyType.SelectPrevPageHotkey => _settings.SelectPrevPageHotkey,
                    HotkeyType.SelectNextPageHotkey => _settings.SelectNextPageHotkey,
                    HotkeyType.AutoCompleteHotkey => _settings.AutoCompleteHotkey,
                    HotkeyType.AutoCompleteHotkey2 => _settings.AutoCompleteHotkey2,
                    HotkeyType.SelectPrevItemHotkey => _settings.SelectPrevItemHotkey,
                    HotkeyType.SelectPrevItemHotkey2 => _settings.SelectPrevItemHotkey2,
                    HotkeyType.SelectNextItemHotkey => _settings.SelectNextItemHotkey,
                    HotkeyType.SelectNextItemHotkey2 => _settings.SelectNextItemHotkey2,
                    HotkeyType.DialogJumpHotkey => _settings.DialogJumpHotkey,
                    _ => throw new System.NotImplementedException("Hotkey type not set")
                };
            }
            set
            {
                switch (Type)
                {
                    // Custom query hotkeys
                    case HotkeyType.CustomQueryHotkey:
                        // We just need to store it in a local field
                        // because we will save to settings in other place
                        hotkey = value;
                        break;
                    // Settings hotkeys
                    case HotkeyType.Hotkey:
                        _settings.Hotkey = value;
                        break;
                    case HotkeyType.PreviewHotkey:
                        _settings.PreviewHotkey = value;
                        break;
                    case HotkeyType.OpenContextMenuHotkey:
                        _settings.OpenContextMenuHotkey = value;
                        break;
                    case HotkeyType.SettingWindowHotkey:
                        _settings.SettingWindowHotkey = value;
                        break;
                    case HotkeyType.OpenHistoryHotkey:
                        _settings.OpenHistoryHotkey = value;
                        break;                    
                    case HotkeyType.CycleHistoryUpHotkey:
                        _settings.CycleHistoryUpHotkey = value;
                        break;
                    case HotkeyType.CycleHistoryDownHotkey:
                        _settings.CycleHistoryDownHotkey = value;
                        break;
                    case HotkeyType.SelectPrevPageHotkey:
                        _settings.SelectPrevPageHotkey = value;
                        break;
                    case HotkeyType.SelectNextPageHotkey:
                        _settings.SelectNextPageHotkey = value;
                        break;
                    case HotkeyType.AutoCompleteHotkey:
                        _settings.AutoCompleteHotkey = value;
                        break;
                    case HotkeyType.AutoCompleteHotkey2:
                        _settings.AutoCompleteHotkey2 = value;
                        break;
                    case HotkeyType.SelectPrevItemHotkey:
                        _settings.SelectPrevItemHotkey = value;
                        break;
                    case HotkeyType.SelectNextItemHotkey:
                        _settings.SelectNextItemHotkey = value;
                        break;
                    case HotkeyType.SelectPrevItemHotkey2:
                        _settings.SelectPrevItemHotkey2 = value;
                        break;
                    case HotkeyType.SelectNextItemHotkey2:
                        _settings.SelectNextItemHotkey2 = value;
                        break;
                    case HotkeyType.DialogJumpHotkey:
                        _settings.DialogJumpHotkey = value;
                        break;
                    default:
                        throw new System.NotImplementedException("Hotkey type not set");
                }

                // After setting the hotkey, we need to refresh the interface
                RefreshHotkeyInterface(Hotkey);
            }
        }

        public static readonly DependencyProperty IsWPFHotkeyControlProperty = DependencyProperty.Register(
            nameof(IsWPFHotkeyControl),
            typeof(bool),
            typeof(HotkeyControl),
            new PropertyMetadata(true)
        );

        public bool IsWPFHotkeyControl
        {
            get { return (bool)GetValue(IsWPFHotkeyControlProperty); }
            set { SetValue(IsWPFHotkeyControlProperty, value); }
        }

        public HotkeyControl()
        {
            InitializeComponent();

            HotkeyList.ItemsSource = KeysToDisplay;

            // We should not call RefreshHotkeyInterface here because DependencyProperty is not set yet
            // And it will be called in OnHotkeyChanged event or Hotkey setter later
        }

        private void RefreshHotkeyInterface(string hotkey)
        {
            SetKeysToDisplay(new HotkeyModel(hotkey));
            CurrentHotkey = new HotkeyModel(hotkey);
        }

        private static bool CheckHotkeyValid(string hotkey)
            => HotKeyMapper.CheckHotkeyValid(hotkey);

        private static bool CheckWPFHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture)
            => hotkey.ValidateForWpf(validateKeyGesture);

        public string EmptyHotkey => App.API.GetTranslation("none");

        public ObservableCollection<string> KeysToDisplay { get; set; } = new();

        public HotkeyModel CurrentHotkey { get; private set; } = new();

        public void GetNewHotkey(object sender, RoutedEventArgs e)
        {
            _ = OpenHotkeyDialogAsync();
        }

        private async Task OpenHotkeyDialogAsync()
        {
            hotkeyControlDialog = new HotkeyControlDialog(Hotkey, DefaultHotkey, HotkeySettings, IsWPFHotkeyControl, WindowTitle);
            await hotkeyControlDialog.ShowAsync();
            switch (hotkeyControlDialog.ResultType)
            {
                case HotkeyControlDialog.EResultType.Cancel:
                    //SetHotkey(Hotkey);
                    return;
                case HotkeyControlDialog.EResultType.Save:
                    SetHotkey(hotkeyControlDialog.ResultValue);
                    break;
                case HotkeyControlDialog.EResultType.Delete:
                    Delete();
                    break;
            }
        }

        private void SetHotkey(HotkeyModel keyModel, bool triggerValidate = true)
        {
            if (string.IsNullOrEmpty(keyModel.HotkeyRaw))
                return;

            if (triggerValidate)
            {
                var hotkeyAvailable = IsWPFHotkeyControl
                    ? CheckWPFHotkeyAvailability(keyModel, ValidateKeyGesture) 
                    : CheckHotkeyValid(keyModel.HotkeyRaw);

                if (!hotkeyAvailable)
                    return;

                Hotkey = keyModel.ToWPFHotkeyString();
                SetKeysToDisplay(CurrentHotkey);

                // If exists then will be unregistered, if doesn't no errors will be thrown.
                if (IsWPFHotkeyControl)
                    HotKeyMapper.UnregisterHotkey(keyModel.HotkeyRaw);
                
                ChangeHotkey?.Execute(keyModel);
            }
            else
            {
                Hotkey = keyModel.ToWPFHotkeyString();
                ChangeHotkey?.Execute(keyModel);
            }
        }

        public void Delete()
        {
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.UnregisterHotkey(Hotkey);
            Hotkey = "";
            SetKeysToDisplay(new HotkeyModel(Hotkey));
        }

        private void SetKeysToDisplay(HotkeyModel? hotkey)
        {
            KeysToDisplay.Clear();

            if (hotkey == null || string.IsNullOrEmpty(hotkey.Value.HotkeyRaw))
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
            if (string.IsNullOrEmpty(keyStr))
                return;

            // index 0 - new hotkey to be added, index 1 - old hotkey to be removed
            var hotkeyNewOld = keyStr.Split(":");
            var hotkey = new HotkeyModel(hotkeyNewOld[0])
            {
                PreviousHotkey = hotkeyNewOld.Length == 2 ? hotkeyNewOld[1] : hotkeyNewOld[0]
            };
            SetHotkey(hotkey, triggerValidate);
        }
    }
}
