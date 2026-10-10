using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Avalonia.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public static readonly DirectProperty<HotkeyControl, IReadOnlyList<string>> KeysToDisplayProperty =
            AvaloniaProperty.RegisterDirect<HotkeyControl, IReadOnlyList<string>>(
                nameof(KeysToDisplay),
                o => o.KeysToDisplay);

        private IReadOnlyList<string> _keysToDisplay = Array.Empty<string>();
        // Replaced wholesale on each change so the ItemsControl regenerates once instead of per Add.
        public IReadOnlyList<string> KeysToDisplay
        {
            get => _keysToDisplay;
            private set => SetAndRaise(KeysToDisplayProperty, ref _keysToDisplay, value);
        }

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
            if (string.IsNullOrEmpty(Hotkey))
            {
                KeysToDisplay = [Translator.GetString("none")];
                return;
            }

            var model = new HotkeyModel(Hotkey);
            KeysToDisplay = model.EnumerateDisplayKeys().ToList();
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
                var dialog = new HotkeyRecorderDialog(Hotkey);
                var result = await dialog.ShowAsync();

                if (result == HotkeyRecorderDialog.EResultType.Save)
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
