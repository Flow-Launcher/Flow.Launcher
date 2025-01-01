#nullable enable

using System.Collections.ObjectModel;
using System.Globalization;
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
        private HotkeyControlDialog hotkeyControlDialog;

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

            //hotkeyControl.SetKeysToDisplay(new HotkeyModel(hotkeyControl.Hotkey));
            //hotkeyControl.CurrentHotkey = new HotkeyModel(hotkeyControl.Hotkey);

            var hotkeyModel = new HotkeyModel(hotkeyControl.Hotkey);
            hotkeyControl.SetKeysToDisplay(hotkeyModel);
            hotkeyControl.CurrentHotkey = hotkeyModel;
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
            SetKeysToDisplay(CurrentHotkey);
        }

        private static bool CheckHotkeyValid(string hotkey)
            => HotKeyMapper.CheckHotkeyValid(hotkey);

        private static bool CheckWPFHotkeyAvailability(HotkeyModel hotkey, bool validateKeyGesture)
            => hotkey.ValidateForWpf(validateKeyGesture);

        public string EmptyHotkey => InternationalizationManager.Instance.GetTranslation("none");

        public ObservableCollection<string> KeysToDisplay { get; set; } = new();

        public HotkeyModel CurrentHotkey { get; private set; } = new();


        public void GetNewHotkey(object sender, RoutedEventArgs e)
        {
            OpenHotkeyDialog();
        }

        private async Task OpenHotkeyDialog()
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
            // WPF hotkey control uses CharKey
            if (string.IsNullOrEmpty(keyModel.HotkeyRaw) || string.IsNullOrEmpty(keyModel.CharKey.ToString()))
                return;

            if (triggerValidate)
            {
                var hotkeyAvailable = IsWPFHotkeyControl
                    ? CheckWPFHotkeyAvailability(keyModel, ValidateKeyGesture) 
                    : CheckHotkeyValid(keyModel.HotkeyRaw);

                if (!hotkeyAvailable)
                    return;

                Hotkey = keyModel.HotkeyRaw;
                SetKeysToDisplay(CurrentHotkey);

                // If exists then will be unregistered, if doesn't no errors will be thrown.
                if (IsWPFHotkeyControl)
                    HotKeyMapper.UnregisterHotkey(keyModel.HotkeyRaw);
                
                ChangeHotkey?.Execute(keyModel);
            }
            else
            {
                Hotkey = keyModel.HotkeyRaw;
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
