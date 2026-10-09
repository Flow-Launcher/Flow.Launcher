using Flow.Launcher.Plugin;
using InfrastructureGlobalHotkey = Flow.Launcher.Infrastructure.Hotkey.GlobalHotkey;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Flow.Launcher.Avalonia.Views.Controls
{
    public partial class HotkeyRecorderDialog : FAContentDialog
    {
        public enum EResultType
        {
            Cancel,
            Save,
            Delete
        }

        private readonly IHotkeySettings _hotkeySettings;
        private readonly HotkeyModel _originalHotkey;
        private Action? _overwriteOtherHotkey;

        public string DefaultHotkey { get; }
        public bool HasDefaultHotkey => !string.IsNullOrEmpty(DefaultHotkey);

        public EResultType ResultType { get; private set; } = EResultType.Cancel;
        public string ResultValue { get; private set; } = string.Empty;
        /// <summary>True when the result was saved by taking the hotkey over from another Flow hotkey.</summary>
        public bool Overwrote { get; private set; }
        public ObservableCollection<string> KeysToDisplay { get; } = new();

        private static string EmptyHotkey => App.API.GetTranslation("none");

        public HotkeyRecorderDialog(string currentHotkey, string defaultHotkey = "", string windowTitle = "")
        {
            _hotkeySettings = Ioc.Default.GetRequiredService<Settings>();
            DefaultHotkey = defaultHotkey ?? string.Empty;

            InitializeComponent();

            if (!string.IsNullOrEmpty(windowTitle))
                Title = windowTitle;

            try
            {
                _originalHotkey = new HotkeyModel(currentHotkey);
                // The hotkey being edited is always "available" to itself; don't flag it on open.
                UpdateKeysDisplay(_originalHotkey);
            }
            catch (Exception e)
            {
                Log.Exception(nameof(HotkeyRecorderDialog), $"Failed to parse current hotkey '{currentHotkey}'", e);
            }

            Opened += HotkeyRecorderDialog_Opened;
            Closing += HotkeyRecorderDialog_Closing;

            PrimaryButtonClick += (s, e) => SetSaveResult();
            SecondaryButtonClick += (s, e) => { ResultType = EResultType.Delete; };
        }

        protected override Type StyleKeyOverride => typeof(FAContentDialog);

        private void HotkeyRecorderDialog_Opened(object? sender, EventArgs args)
        {
            this.Focus();
            AttachKeyboardHook();
        }

        private void HotkeyRecorderDialog_Closing(object? sender, EventArgs args)
        {
            DetachKeyboardHook();
        }

        private bool _altDown;
        private bool _ctrlDown;
        private bool _shiftDown;
        private bool _winDown;
        private Func<KeyEvent, int, SpecialKeyState, bool>? _previousKeyboardCallback;

        private void AttachKeyboardHook()
        {
            // Initialize Global Hotkey Hook when dialog opens
            try
            {
                // Sync initial modifier state
                var state = InfrastructureGlobalHotkey.CheckModifiers();
                _altDown = state.AltPressed;
                _ctrlDown = state.CtrlPressed;
                _shiftDown = state.ShiftPressed;
                _winDown = state.WinPressed;

                _previousKeyboardCallback = InfrastructureGlobalHotkey.hookedKeyboardCallback;
                InfrastructureGlobalHotkey.hookedKeyboardCallback = GlobalKeyHook;
            }
            catch (Exception ex)
            {
                Log.Exception(nameof(HotkeyRecorderDialog), "Failed to attach global keyboard hook", ex);
            }
        }

        private void DetachKeyboardHook()
        {
            if (InfrastructureGlobalHotkey.hookedKeyboardCallback == GlobalKeyHook)
            {
                InfrastructureGlobalHotkey.hookedKeyboardCallback = _previousKeyboardCallback;
            }
        }

        private bool GlobalKeyHook(KeyEvent keyEvent, int vkCode, SpecialKeyState state)
        {
            var wpfKey = InfrastructureGlobalHotkey.GetKeyFromVk(vkCode);
            bool isKeyDown = (keyEvent == KeyEvent.WM_KEYDOWN || keyEvent == KeyEvent.WM_SYSKEYDOWN);

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

            ApplyRecordedHotkey(model);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void SetSaveResult()
        {
            if (KeysToDisplay.Count == 0 || (KeysToDisplay.Count == 1 && KeysToDisplay[0] == EmptyHotkey))
            {
                ResultType = EResultType.Delete;
                return;
            }

            ResultType = EResultType.Save;
            ResultValue = string.Join("+", KeysToDisplay);
        }

        private void UpdateKeysDisplay(HotkeyModel model)
        {
            KeysToDisplay.Clear();
            if (model == default)
            {
                KeysToDisplay.Add(EmptyHotkey);
                return;
            }

            foreach (var key in model.EnumerateDisplayKeys())
            {
                KeysToDisplay.Add(key);
            }
        }

        private void ApplyRecordedHotkey(HotkeyModel model)
        {
            UpdateKeysDisplay(model);
            _overwriteOtherHotkey = null;

            var alert = this.FindControl<Border>("Alert");
            var tbMsg = this.FindControl<TextBlock>("tbMsg");
            var overwriteBtn = this.FindControl<Button>("OverwriteBtn");

            void ShowState(string? message, bool canSave, bool canOverwrite)
            {
                IsPrimaryButtonEnabled = canSave;
                if (overwriteBtn != null)
                    overwriteBtn.IsVisible = canOverwrite;
                if (alert != null && tbMsg != null)
                {
                    alert.IsVisible = message != null;
                    tbMsg.Text = message ?? string.Empty;
                }
            }

            if (model == default || model == _originalHotkey)
            {
                ShowState(null, true, false);
                return;
            }

            if (_hotkeySettings.RegisteredHotkeys.FirstOrDefault(v => v.Hotkey == model) is { } registered)
            {
                var description = string.Format(
                    App.API.GetTranslation(registered.DescriptionResourceKey),
                    registered.DescriptionFormatVariables);

                if (registered.RemoveHotkey is not null)
                {
                    _overwriteOtherHotkey = registered.RemoveHotkey;
                    ShowState(string.Format(App.API.GetTranslation("hotkeyUnavailableEditable"), description), false, true);
                }
                else
                {
                    ShowState(string.Format(App.API.GetTranslation("hotkeyUnavailableUneditable"), description), false, false);
                }
                return;
            }

            var isAvailable = model.Validate(true) && HotKeyMapper.CheckAvailability(model);
            ShowState(isAvailable ? null : App.API.GetTranslation("hotkeyUnavailable"), isAvailable, false);
        }

        private void Reset_Click(object? sender, RoutedEventArgs e)
        {
            ApplyRecordedHotkey(new HotkeyModel(DefaultHotkey));
        }

        private void Overwrite_Click(object? sender, RoutedEventArgs e)
        {
            _overwriteOtherHotkey?.Invoke();
            Overwrote = true;
            SetSaveResult();
            Hide(FAContentDialogResult.Primary);
        }

        public new async Task<EResultType> ShowAsync()
        {
            var result = await base.ShowAsync();
            if (result == FAContentDialogResult.Primary)
                return ResultType == EResultType.Delete ? EResultType.Delete : EResultType.Save;
            if (result == FAContentDialogResult.Secondary)
                return EResultType.Delete;
            return EResultType.Cancel;
        }
    }
}
