using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using InfrastructureGlobalHotkey = Flow.Launcher.Infrastructure.Hotkey.GlobalHotkey;

namespace Flow.Launcher.Avalonia.Views.Controls
{
    public partial class HotkeyRecorderDialog : ContentDialog
    {
        public enum EResultType
        {
            Cancel,
            Save,
            Delete
        }

        public EResultType ResultType { get; private set; } = EResultType.Cancel;
        public string ResultValue { get; private set; } = string.Empty;
        public ObservableCollection<string> KeysToDisplay { get; } = new();

        private bool _altDown;
        private bool _ctrlDown;
        private bool _shiftDown;
        private bool _winDown;

        public HotkeyRecorderDialog(string currentHotkey)
        {
            InitializeComponent();
            
            var model = new HotkeyModel(currentHotkey);
            UpdateKeysDisplay(model);

            Opened += HotkeyRecorderDialog_Opened;
            Closing += HotkeyRecorderDialog_Closing;
            
            PrimaryButtonClick += (s, e) => { ResultType = EResultType.Save; ResultValue = string.Join("+", KeysToDisplay); };
            SecondaryButtonClick += (s, e) => { ResultType = EResultType.Delete; };
        }

        protected override Type StyleKeyOverride => typeof(ContentDialog);

        private void HotkeyRecorderDialog_Opened(object? sender, EventArgs args)
        {
            this.Focus();
            
            // Initialize Global Hotkey Hook when dialog opens
            try 
            {
                // Sync initial modifier state
                var state = InfrastructureGlobalHotkey.CheckModifiers();
                _altDown = state.AltPressed;
                _ctrlDown = state.CtrlPressed;
                _shiftDown = state.ShiftPressed;
                _winDown = state.WinPressed;

                InfrastructureGlobalHotkey.hookedKeyboardCallback = GlobalKeyHook;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hook Error: {ex.Message}");
            }
        }

        private void HotkeyRecorderDialog_Closing(object? sender, EventArgs args)
        {
            // Clear the callback but DON'T dispose the static hook
            InfrastructureGlobalHotkey.hookedKeyboardCallback = null;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private bool GlobalKeyHook(KeyEvent keyEvent, int vkCode, SpecialKeyState state)
        {
            var wpfKey = InfrastructureGlobalHotkey.GetKeyFromVk(vkCode);
            bool isKeyDown = (keyEvent == KeyEvent.WM_KEYDOWN || keyEvent == KeyEvent.WM_SYSKEYDOWN);
            bool isKeyUp = (keyEvent == KeyEvent.WM_KEYUP || keyEvent == KeyEvent.WM_SYSKEYUP);

            // Track modifier state manually (since we swallow keys, OS state is stale)
            if (wpfKey == System.Windows.Input.Key.LeftAlt || wpfKey == System.Windows.Input.Key.RightAlt)
                _altDown = isKeyDown;
            else if (wpfKey == System.Windows.Input.Key.LeftCtrl || wpfKey == System.Windows.Input.Key.RightCtrl)
                _ctrlDown = isKeyDown;
            else if (wpfKey == System.Windows.Input.Key.LeftShift || wpfKey == System.Windows.Input.Key.RightShift)
                _shiftDown = isKeyDown;
            else if (wpfKey == System.Windows.Input.Key.LWin || wpfKey == System.Windows.Input.Key.RWin)
                _winDown = isKeyDown;

            // Only process key down events for UI updates
            if (isKeyDown)
            {
                // Capture current modifier state for the UI thread
                bool alt = _altDown;
                bool ctrl = _ctrlDown;
                bool shift = _shiftDown;
                bool win = _winDown;

                // Marshal to UI Thread
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    HandleGlobalKey(vkCode, alt, ctrl, shift, win);
                });
            }

            // Return false to SWALLOW the key (prevents other apps from receiving it)
            return false; 
        }

        private void HandleGlobalKey(int vkCode, bool altDown, bool ctrlDown, bool shiftDown, bool winDown)
        {
            var wpfKey = InfrastructureGlobalHotkey.GetKeyFromVk(vkCode);

            // Don't treat modifiers as main keys
            if (wpfKey == System.Windows.Input.Key.LeftAlt || wpfKey == System.Windows.Input.Key.RightAlt ||
                wpfKey == System.Windows.Input.Key.LeftCtrl || wpfKey == System.Windows.Input.Key.RightCtrl ||
                wpfKey == System.Windows.Input.Key.LeftShift || wpfKey == System.Windows.Input.Key.RightShift ||
                wpfKey == System.Windows.Input.Key.LWin || wpfKey == System.Windows.Input.Key.RWin)
            {
                wpfKey = System.Windows.Input.Key.None;
            }

            var model = new HotkeyModel(
                altDown,
                shiftDown,
                winDown,
                ctrlDown,
                wpfKey);

            UpdateKeysDisplay(model);
            
            // Update Save button enablement based on validity and availability
            var isValid = model.Validate();
            var isAvailable = isValid && HotKeyMapper.CheckAvailability(model);
            
            IsPrimaryButtonEnabled = isAvailable;

            var alert = this.FindControl<Border>("Alert");
            var tbMsg = this.FindControl<TextBlock>("tbMsg");
            if (alert != null && tbMsg != null)
            {
                if (isValid && !isAvailable)
                {
                    // TODO: Get actual translation
                    tbMsg.Text = "Hotkey already in use";
                    alert.IsVisible = true;
                }
                else
                {
                    alert.IsVisible = false;
                }
            }
        }

        private void UpdateKeysDisplay(HotkeyModel model)
        {
            KeysToDisplay.Clear();
            foreach (var key in model.EnumerateDisplayKeys())
            {
                KeysToDisplay.Add(key);
            }
        }
        
        public new async Task<EResultType> ShowAsync()
        {
            var result = await base.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return EResultType.Save;
            if (result == ContentDialogResult.Secondary)
                return EResultType.Delete;
            return EResultType.Cancel;
        }
    }
}
