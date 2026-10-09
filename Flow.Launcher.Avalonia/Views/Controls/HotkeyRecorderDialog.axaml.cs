using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Avalonia.Helper;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.Hotkey;
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

        public EResultType ResultType { get; private set; } = EResultType.Cancel;
        public string ResultValue { get; private set; } = string.Empty;
        public ObservableCollection<string> KeysToDisplay { get; } = new();

        public HotkeyRecorderDialog(string currentHotkey)
        {
            InitializeComponent();
            
            try
            {
                var model = new HotkeyModel(currentHotkey);
                UpdateKeysDisplay(model);
            }
            catch (Exception e)
            {
                Log.Exception(nameof(HotkeyRecorderDialog), $"Failed to parse current hotkey '{currentHotkey}'", e);
            }

            Opened += HotkeyRecorderDialog_Opened;
            Closing += HotkeyRecorderDialog_Closing;
            
            PrimaryButtonClick += (s, e) => { ResultType = EResultType.Save; ResultValue = string.Join("+", KeysToDisplay); };
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

        // Key capture: Win32 low-level keyboard hook on Windows (.Windows.cs), window key events elsewhere (.CrossPlatform.cs).
        partial void AttachKeyboardHook();

        partial void DetachKeyboardHook();

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void UpdateKeysDisplay(HotkeyModel model)
        {
            KeysToDisplay.Clear();
            foreach (var key in model.EnumerateDisplayKeys())
            {
                KeysToDisplay.Add(key);
            }
        }

        private void ApplyRecordedHotkey(HotkeyModel model)
        {
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
        
        public new async Task<EResultType> ShowAsync()
        {
            var result = await base.ShowAsync();
            if (result == FAContentDialogResult.Primary)
                return EResultType.Save;
            if (result == FAContentDialogResult.Secondary)
                return EResultType.Delete;
            return EResultType.Cancel;
        }
    }
}
