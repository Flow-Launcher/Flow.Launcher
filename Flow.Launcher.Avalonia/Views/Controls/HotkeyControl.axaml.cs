using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Avalonia.Helper;
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

        public ObservableCollection<string> KeysToDisplay { get; } = new();

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
                KeysToDisplay.Add("None");
                return;
            }

            var model = new HotkeyModel(Hotkey);
            foreach (var key in model.EnumerateDisplayKeys())
            {
                KeysToDisplay.Add(key);
            }
        }

        private async Task OpenHotkeyRecorderDialog()
        {
            var originalHotkey = Hotkey;
            
            // Temporarily unregister so it doesn't conflict with availability check
            HotKeyMapper.RemoveToggleHotkey();

            var dialog = new HotkeyRecorderDialog(Hotkey);
            var result = await dialog.ShowAsync();

            if (result == HotkeyRecorderDialog.EResultType.Save)
            {
                Hotkey = dialog.ResultValue;
            }
            else if (result == HotkeyRecorderDialog.EResultType.Delete)
            {
                Hotkey = string.Empty;
            }
            else
            {
                // Restore original hotkey if cancelled
                HotKeyMapper.SetToggleHotkey(originalHotkey);
            }
        }
    }
}
