#nullable enable

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;

namespace Flow.Launcher
{
    public partial class HotkeyControl2
    {
        public string WindowTitle { get; set; } = string.Empty;

        public static readonly DependencyProperty WindowTitleProperty = DependencyProperty.Register(
            nameof(WindowTitle),
            typeof(string),
            typeof(HotkeyControl2),
            new PropertyMetadata(default(string))
        );

        /// <summary>
        /// Designed for Preview Hotkey and KeyGesture.
        /// </summary>
        public static readonly DependencyProperty ValidateKeyGestureProperty = DependencyProperty.Register(
            nameof(ValidateKeyGesture),
            typeof(bool),
            typeof(HotkeyControl2),
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
            typeof(HotkeyControl2),
            new PropertyMetadata(default(string))
        );

        public string DefaultHotkey
        {
            get { return (string)GetValue(DefaultHotkeyProperty); }
            set { SetValue(DefaultHotkeyProperty, value); }
        }

        private static void OnHotkeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not HotkeyControl2 hotkeyControl)
            {
                return;
            }

            hotkeyControl.SetKeysToDisplay(new HotkeyModel(hotkeyControl.Hotkey));
            hotkeyControl.CurrentHotkey = new HotkeyModel(hotkeyControl.Hotkey);
        }


        public static readonly DependencyProperty ChangeHotkeyProperty = DependencyProperty.Register(
            nameof(ChangeHotkey),
            typeof(ICommand),
            typeof(HotkeyControl2),
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
            typeof(HotkeyControl2),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnHotkeyChanged)
        );

        public string Hotkey
        {
            get { return (string)GetValue(HotkeyProperty); }
            set { SetValue(HotkeyProperty, value); }
        }

        public HotkeyControl2()
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
            if (!string.IsNullOrEmpty(Hotkey))
            {
                HotKeyMapper.RemoveHotkey(Hotkey);
            }

            var owner = Window.GetWindow(this);
            var width = owner?.ActualWidth ?? 0;
            var height = owner?.ActualHeight ?? 0;
            var w = new HotkeyControl2Dialog
            {
                Title = WindowTitle,
                Owner = owner,
                Width = width,
                Height = height
            };
            w.ShowDialog();
            switch (w.ResultType)
            {
                case HotkeyControl2Dialog.EResultType.Cancel:
                    return;
                case HotkeyControl2Dialog.EResultType.Save:
                    SetHotkey(w.ResultValue);
                    break;
                case HotkeyControl2Dialog.EResultType.Reset:
                    ResetToDefault();
                    break;
                case HotkeyControl2Dialog.EResultType.Delete:
                    Delete();
                    break;
            }
        }

        private void ResetToDefault()
        {
            if (!string.IsNullOrEmpty(Hotkey))
                HotKeyMapper.RemoveHotkey(Hotkey);
            Hotkey = DefaultHotkey;
            CurrentHotkey = new HotkeyModel(Hotkey);

            SetKeysToDisplay(CurrentHotkey);

            SetHotkey(CurrentHotkey);
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
