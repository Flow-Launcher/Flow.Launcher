using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Resource;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;

namespace Flow.Launcher.Avalonia.Views.Controls
{
    public partial class HotkeyControl : UserControl
    {
        public static readonly DirectProperty<HotkeyControl, string> HotkeyProperty =
            AvaloniaProperty.RegisterDirect<HotkeyControl, string>(
                nameof(Hotkey),
                o => o.Hotkey,
                (o, v) => o.Hotkey = v);

        private string _hotkey = string.Empty;
        public string Hotkey
        {
            get => _hotkey;
            set
            {
                if (SetAndRaise(HotkeyProperty, ref _hotkey, value))
                {
                    UpdateKeysDisplay();
                }
            }
        }

        public static readonly StyledProperty<string> DefaultHotkeyProperty =
            AvaloniaProperty.Register<HotkeyControl, string>(nameof(DefaultHotkey), string.Empty);

        /// <summary>Hotkey restored by the recorder dialog's Reset button.</summary>
        public string DefaultHotkey
        {
            get => GetValue(DefaultHotkeyProperty);
            set => SetValue(DefaultHotkeyProperty, value);
        }

        public static readonly StyledProperty<string> WindowTitleProperty =
            AvaloniaProperty.Register<HotkeyControl, string>(nameof(WindowTitle), string.Empty);

        /// <summary>Recorder dialog title; empty uses the default "hotkeyRegTitle".</summary>
        public string WindowTitle
        {
            get => GetValue(WindowTitleProperty);
            set => SetValue(WindowTitleProperty, value);
        }

        public ObservableCollection<string> KeysToDisplay { get; } = new();

        public static readonly DirectProperty<HotkeyControl, bool> UnregisterToggleHotkeyWhileRecordingProperty =
            AvaloniaProperty.RegisterDirect<HotkeyControl, bool>(
                nameof(UnregisterToggleHotkeyWhileRecording),
                o => o.UnregisterToggleHotkeyWhileRecording,
                (o, v) => o.UnregisterToggleHotkeyWhileRecording = v);

        private bool _unregisterToggleHotkeyWhileRecording;
        public bool UnregisterToggleHotkeyWhileRecording
        {
            get => _unregisterToggleHotkeyWhileRecording;
            set => SetAndRaise(UnregisterToggleHotkeyWhileRecordingProperty, ref _unregisterToggleHotkeyWhileRecording, value);
        }

        public IAsyncRelayCommand RecordHotkeyCommand { get; }

        public HotkeyControl()
        {
            RecordHotkeyCommand = new AsyncRelayCommand(OpenHotkeyRecorderDialog);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void UpdateKeysDisplay()
        {
            KeysToDisplay.Clear();
            if (string.IsNullOrEmpty(Hotkey))
            {
                KeysToDisplay.Add(Translator.GetString("none"));
                return;
            }

            var model = new HotkeyModel(Hotkey);
            foreach (var key in model.EnumerateDisplayKeys())
            {
                KeysToDisplay.Add(key);
            }
        }

        // Mirrors WPF HotkeyControl.SetHotkey: only accept a hotkey that is valid and free (or explicitly taken over).
        private bool IsAcceptable(string hotkey, bool overwrote)
        {
            var model = new HotkeyModel(hotkey);
            if (!model.Validate(true))
                return false;
            return overwrote || hotkey == Hotkey || HotKeyMapper.CheckAvailability(model);
        }

        private async Task OpenHotkeyRecorderDialog()
        {
            var originalHotkey = Hotkey;
            var shouldUnregisterToggle = UnregisterToggleHotkeyWhileRecording;

            if (shouldUnregisterToggle)
            {
                HotKeyMapper.RemoveToggleHotkey();
            }

            try
            {
                var dialog = new HotkeyRecorderDialog(Hotkey, DefaultHotkey, WindowTitle);
                var result = await dialog.ShowAsync();

                if (result == HotkeyRecorderDialog.EResultType.Save && IsAcceptable(dialog.ResultValue, dialog.Overwrote))
                {
                    Hotkey = dialog.ResultValue;

                    if (shouldUnregisterToggle)
                    {
                        HotKeyMapper.SetToggleHotkey(dialog.ResultValue);
                    }
                }
                else if (result == HotkeyRecorderDialog.EResultType.Delete)
                {
                    Hotkey = string.Empty;
                }
                else if (shouldUnregisterToggle)
                {
                    HotKeyMapper.SetToggleHotkey(originalHotkey);
                }
            }
            catch
            {
                if (shouldUnregisterToggle)
                {
                    HotKeyMapper.SetToggleHotkey(originalHotkey);
                }

                throw;
            }
        }
    }
}
