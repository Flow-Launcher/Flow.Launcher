using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Avalonia.Views.Dialogs;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Logger;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;

namespace Flow.Launcher.Avalonia;

public partial class MainWindow : Window
{
    private static readonly string ClassName = nameof(MainWindow);

    // Settings whose change re-parses the customizable in-window hotkeys.
    private static readonly HashSet<string> HotkeySettingNames =
    [
        nameof(Settings.PreviewHotkey),
        nameof(Settings.OpenHistoryHotkey),
        nameof(Settings.CycleHistoryUpHotkey),
        nameof(Settings.CycleHistoryDownHotkey),
        nameof(Settings.AutoCompleteHotkey),
        nameof(Settings.AutoCompleteHotkey2),
        nameof(Settings.SelectNextItemHotkey),
        nameof(Settings.SelectNextItemHotkey2),
        nameof(Settings.SelectPrevItemHotkey),
        nameof(Settings.SelectPrevItemHotkey2),
        nameof(Settings.SelectNextPageHotkey),
        nameof(Settings.SelectPrevPageHotkey),
        nameof(Settings.OpenContextMenuHotkey),
        nameof(Settings.SettingWindowHotkey),
        nameof(Settings.OpenResultModifiers),
    ];

    private static readonly Key[] OpenResultKeys =
        [Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.D0];

    // Minimum window width the quick width hotkeys shrink to (WPF DecreaseWidth).
    private const double MinQuickWidth = 400;
    private const double QuickWidthStep = 100;

    private MainViewModel? _viewModel;
    private TextBox? _queryTextBox;
    private Settings? _settings;
    private KeyGesture? _previewHotkeyGesture;
    private KeyGesture? _openHistoryHotkeyGesture;
    private KeyGesture? _cycleHistoryUpHotkeyGesture;
    private KeyGesture? _cycleHistoryDownHotkeyGesture;
    private KeyGesture? _autoCompleteHotkeyGesture;
    private KeyGesture? _autoCompleteHotkeyGesture2;
    private KeyGesture? _selectNextItemHotkeyGesture;
    private KeyGesture? _selectNextItemHotkeyGesture2;
    private KeyGesture? _selectPrevItemHotkeyGesture;
    private KeyGesture? _selectPrevItemHotkeyGesture2;
    private KeyGesture? _selectNextPageHotkeyGesture;
    private KeyGesture? _selectPrevPageHotkeyGesture;
    private KeyGesture? _openContextMenuHotkeyGesture;
    private KeyGesture? _settingWindowHotkeyGesture;
    private KeyModifiers _openResultModifiers;

    // A user drag-resize has no end event; the size is applied to the settings once resizing pauses.
    private readonly DispatcherTimer _resizeEndTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };
    private Size _sizeBeforeUserResize;
    private Size _lastSettledClientSize;

    public MainWindow()
    {
        InitializeComponent();
        InitializeWindowShape();

        // Get the ViewModel and Settings from DI
        _viewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _viewModel.HideRequested += () =>
        {
            SaveLastPosition();
            Hide();
            ReturnFocusAfterHide();
        };
        _viewModel.QueryTextFocusRequested += HandleQueryTextFocusRequest;
        DataContext = _viewModel;

        _settings.PropertyChanged += OnSettingsPropertyChanged;
        UpdateHotkeyGestures();

        // Get reference to the query text box
        _queryTextBox = this.FindControl<TextBox>("QueryTextBox");
        if (_queryTextBox != null)
        {
            _queryTextBox.PastingFromClipboard += OnQueryTextBoxPasting;
        }

        // Launcher hotkeys run before the focused control (query box, result list) sees the key.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        // Subscribe to window events
        Deactivated += OnWindowDeactivated;
        Resized += OnWindowResized;
        _resizeEndTimer.Tick += (_, _) => ApplyUserResize();
        Screens.Changed += OnScreensChanged;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != null && HotkeySettingNames.Contains(e.PropertyName))
        {
            Dispatcher.UIThread.Post(UpdateHotkeyGestures);
        }
    }

    private void UpdateHotkeyGestures()
    {
        if (_settings == null) return;

        _previewHotkeyGesture = ParseKeyGesture(_settings.PreviewHotkey);
        _openHistoryHotkeyGesture = ParseKeyGesture(_settings.OpenHistoryHotkey);
        _cycleHistoryUpHotkeyGesture = ParseKeyGesture(_settings.CycleHistoryUpHotkey);
        _cycleHistoryDownHotkeyGesture = ParseKeyGesture(_settings.CycleHistoryDownHotkey);
        _autoCompleteHotkeyGesture = ParseKeyGesture(_settings.AutoCompleteHotkey);
        _autoCompleteHotkeyGesture2 = ParseKeyGesture(_settings.AutoCompleteHotkey2);
        _selectNextItemHotkeyGesture = ParseKeyGesture(_settings.SelectNextItemHotkey);
        _selectNextItemHotkeyGesture2 = ParseKeyGesture(_settings.SelectNextItemHotkey2);
        _selectPrevItemHotkeyGesture = ParseKeyGesture(_settings.SelectPrevItemHotkey);
        _selectPrevItemHotkeyGesture2 = ParseKeyGesture(_settings.SelectPrevItemHotkey2);
        _selectNextPageHotkeyGesture = ParseKeyGesture(_settings.SelectNextPageHotkey);
        _selectPrevPageHotkeyGesture = ParseKeyGesture(_settings.SelectPrevPageHotkey);
        _openContextMenuHotkeyGesture = ParseKeyGesture(_settings.OpenContextMenuHotkey);
        _settingWindowHotkeyGesture = ParseKeyGesture(_settings.SettingWindowHotkey);
        _openResultModifiers = string.IsNullOrWhiteSpace(_settings.OpenResultModifiers)
            ? KeyModifiers.None
            : ToKeyModifiers(new HotkeyModel(_settings.OpenResultModifiers));
    }

    /// <summary>
    /// Parses a stored hotkey ("Ctrl + Tab", "Alt+Up", "F1") with the same <see cref="HotkeyModel"/> rules the
    /// hotkey recorder writes them with. "Win" maps to the Meta (Command) key.
    /// </summary>
    private static KeyGesture? ParseKeyGesture(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return null;

        var model = new HotkeyModel(hotkey);
        // Stored key names are WPF Key names, which Avalonia's Key enum mirrors.
        if (!Enum.TryParse<Key>(model.CharKey.ToString(), out var key) || key == Key.None)
        {
            return null;
        }

        return new KeyGesture(key, ToKeyModifiers(model));
    }

    private static KeyModifiers ToKeyModifiers(HotkeyModel model) =>
        (model.Ctrl ? KeyModifiers.Control : KeyModifiers.None)
        | (model.Alt ? KeyModifiers.Alt : KeyModifiers.None)
        | (model.Shift ? KeyModifiers.Shift : KeyModifiers.None)
        | (model.Win ? KeyModifiers.Meta : KeyModifiers.None);

    private static bool Matches(KeyGesture? gesture, KeyEventArgs e) =>
        gesture != null && e.Key == gesture.Key && e.KeyModifiers == gesture.KeyModifiers;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        if (_settings?.FirstLaunch == true)
        {
            _settings.FirstLaunch = false;
            _settings.ReleaseNotesVersion = Constant.Version;

            if (Win32Helper.IsBackdropSupported())
            {
                _settings.BackdropType = BackdropTypes.Acrylic;
            }

            _settings.Save();

            var welcomeWindow = new WelcomeWindow();
            welcomeWindow.Show(this);
        }

        // Focus the query text box when window loads
        _queryTextBox?.Focus();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        PlaceOnSelectedScreen();

        // QueryTextFocusRequested applies the final select/caret behavior. Avoid a transient SelectAll here.
        _queryTextBox?.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_viewModel == null || e.Handled)
        {
            return;
        }

        // Handle Escape to hide window (handled by command, but keep as fallback)
        if (e.Key == Key.Escape)
        {
            _viewModel.EscCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Right at the end of the query opens the selected result's context menu (results and history views).
        // Reaching here means the query box did not move the caret.
        if (e.Key == Key.Right
            && (_viewModel.IsResultsViewActive || _viewModel.IsHistoryViewActive)
            && _queryTextBox != null
            && _queryTextBox.CaretIndex >= (_queryTextBox.Text?.Length ?? 0))
        {
            _viewModel.ToggleContextMenuCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Left at the start of the query goes back from the context menu or history view.
        if (e.Key == Key.Left
            && !_viewModel.IsResultsViewActive
            && _queryTextBox != null
            && _queryTextBox.CaretIndex == 0)
        {
            _viewModel.EscCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_viewModel == null || e.Handled)
        {
            return;
        }

        // Scrollable preview content keeps its own navigation keys.
        if (IsNavigationKey(e.Key) && IsInPreviewPanel(e.Source))
        {
            return;
        }

        if (TryHandleFixedHotkeys(e) || TryHandleDynamicHotkeys(e))
        {
            e.Handled = true;
        }
    }

    private static bool IsNavigationKey(Key key) =>
        key is Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End;

    private bool IsInPreviewPanel(object? source) =>
        source is Visual visual
        && this.FindControl<Border>("PreviewPanelContainer") is { } preview
        && (visual == preview || preview.IsVisualAncestorOf(visual));

    // Port of the WPF window's fixed key bindings (Settings.FixedHotkeys) and its PreviewKeyDown handler.
    private bool TryHandleFixedHotkeys(KeyEventArgs e)
    {
        var vm = _viewModel!;
        var modifiers = e.KeyModifiers;

        switch (e.Key)
        {
            case Key.Enter:
                if (modifiers == KeyModifiers.Shift)
                {
                    vm.ToggleContextMenuCommand.Execute(null);
                }
                else
                {
                    // Enter, Ctrl/Alt/Cmd+Enter and Ctrl+Shift+Enter pass the modifiers on to the plugin.
                    _ = vm.OpenResultWithModifiersAsync(null, modifiers);
                }

                return true;
            case Key.PageDown when modifiers == KeyModifiers.None:
                vm.SelectNextPageCommand.Execute(null);
                return true;
            case Key.PageUp when modifiers == KeyModifiers.None:
                vm.SelectPrevPageCommand.Execute(null);
                return true;
            case Key.Home when modifiers == KeyModifiers.Alt:
                vm.SelectFirstResultCommand.Execute(null);
                return true;
            case Key.End when modifiers == KeyModifiers.Alt:
                vm.SelectLastResultCommand.Execute(null);
                return true;
            case Key.R when modifiers == KeyModifiers.Control:
                vm.ReQueryResults();
                return true;
            case Key.F5 when modifiers == KeyModifiers.None:
                vm.ReloadPluginDataCommand.Execute(null);
                return true;
            case Key.OemCloseBrackets when modifiers == KeyModifiers.Control:
                ChangeWindowWidth(QuickWidthStep);
                return true;
            case Key.OemOpenBrackets when modifiers == KeyModifiers.Control:
                ChangeWindowWidth(-QuickWidthStep);
                return true;
            case Key.OemPlus when modifiers == KeyModifiers.Control:
                vm.IncreaseMaxResultCommand.Execute(null);
                return true;
            case Key.OemMinus when modifiers == KeyModifiers.Control:
                vm.DecreaseMaxResultCommand.Execute(null);
                return true;
            case Key.C when modifiers == (KeyModifiers.Control | KeyModifiers.Shift):
                vm.CopyAlternativeCommand.Execute(null);
                return true;
            case Key.F12 when modifiers == KeyModifiers.Control:
                vm.ToggleGameModeCommand.Execute(null);
                return true;
            case Key.Back when modifiers == KeyModifiers.Control:
                // Ctrl+Backspace at the end of a path query goes up one directory; otherwise the query box deletes a word.
                return _queryTextBox != null
                    && vm.IsResultsViewActive
                    && !string.IsNullOrEmpty(_queryTextBox.Text)
                    && _queryTextBox.CaretIndex == _queryTextBox.Text.Length
                    && vm.TryBackspacePath();
        }

        // Copy with nothing selected in the query box copies the selected result instead.
        if (TextBox.CopyGesture?.Matches(e) == true
            && _queryTextBox != null
            && string.IsNullOrEmpty(_queryTextBox.SelectedText))
        {
            return vm.TryCopySelectedResult();
        }

        if (_openResultModifiers != KeyModifiers.None && modifiers == _openResultModifiers)
        {
            var index = Array.IndexOf(OpenResultKeys, e.Key);
            if (index >= 0)
            {
                _ = vm.OpenResultAtIndexAsync(index);
                return true;
            }
        }

        return false;
    }

    // Customizable hotkeys from the hotkey settings page.
    private bool TryHandleDynamicHotkeys(KeyEventArgs e)
    {
        var vm = _viewModel!;

        if (Matches(_openHistoryHotkeyGesture, e))
        {
            vm.LoadHistoryCommand.Execute(null);
            return true;
        }

        if (Matches(_autoCompleteHotkeyGesture, e) || Matches(_autoCompleteHotkeyGesture2, e))
        {
            vm.AutocompleteQuery(useSubTitle: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            return true;
        }

        if (Matches(_selectNextItemHotkeyGesture, e) || Matches(_selectNextItemHotkeyGesture2, e))
        {
            vm.SelectNextItemCommand.Execute(null);
            return true;
        }

        if (Matches(_selectPrevItemHotkeyGesture, e) || Matches(_selectPrevItemHotkeyGesture2, e))
        {
            vm.SelectPrevItemCommand.Execute(null);
            return true;
        }

        if (Matches(_selectNextPageHotkeyGesture, e))
        {
            vm.SelectNextPageCommand.Execute(null);
            return true;
        }

        if (Matches(_selectPrevPageHotkeyGesture, e))
        {
            vm.SelectPrevPageCommand.Execute(null);
            return true;
        }

        if (Matches(_openContextMenuHotkeyGesture, e))
        {
            vm.ToggleContextMenuCommand.Execute(null);
            return true;
        }

        if (Matches(_settingWindowHotkeyGesture, e))
        {
            vm.OpenSettingsCommand.Execute(null);
            return true;
        }

        // The preview and history cycling hotkeys only apply while typing in a text box.
        if (e.Source is Visual source && source.FindAncestorOfType<TextBox>() == null && e.Source is not TextBox)
        {
            return false;
        }

        if (Matches(_previewHotkeyGesture, e))
        {
            vm.TogglePreviewCommand.Execute(null);
            return true;
        }

        if (Matches(_cycleHistoryUpHotkeyGesture, e))
        {
            vm.ReverseHistoryCommand.Execute(null);
            return true;
        }

        if (Matches(_cycleHistoryDownHotkeyGesture, e))
        {
            vm.ForwardHistoryCommand.Execute(null);
            return true;
        }

        return false;
    }

    // Ctrl+] / Ctrl+[: widen or narrow the window around its center (WPF IncreaseWidth/DecreaseWidth).
    private void ChangeWindowWidth(double delta)
    {
        if (_settings == null) return;

        var currentWidth = _settings.WindowSize;
        var newWidth = Math.Max(MinQuickWidth, currentWidth + delta);
        if (newWidth == currentWidth) return;

        var shift = (int)Math.Round((newWidth - currentWidth) / 2 * DesktopScaling);
        _settings.WindowSize = newWidth;
        Position = new PixelPoint(Position.X - shift, Position.Y);
    }

    private void OnWindowResized(object? sender, WindowResizedEventArgs e)
    {
        if (e.Reason != WindowResizeReason.User)
        {
            if (!_resizeEndTimer.IsEnabled)
            {
                _lastSettledClientSize = e.ClientSize;
            }

            return;
        }

        if (!_resizeEndTimer.IsEnabled)
        {
            _sizeBeforeUserResize = _lastSettledClientSize;
        }

        _resizeEndTimer.Stop();
        _resizeEndTimer.Start();
    }

    // Port of the WPF WM_EXITSIZEMOVE handling: a drag-resize sets the number of results and the window width,
    // unless the window size is fixed, then the window sizes to its content again.
    private void ApplyUserResize()
    {
        _resizeEndTimer.Stop();
        if (_settings == null) return;

        var size = ClientSize;
        if (!_settings.KeepMaxResults)
        {
            if (Math.Abs(size.Height - _sizeBeforeUserResize.Height) >= 1
                && size.Height > _settings.WindowHeightSize + _settings.ItemHeightSize)
            {
                var itemCount = (size.Height - (_settings.WindowHeightSize + 14)) / _settings.ItemHeightSize;
                _settings.MaxResultsToShow = Math.Max(2, (int)Math.Truncate(itemCount));
            }

            if (Math.Abs(size.Width - _sizeBeforeUserResize.Width) >= 1)
            {
                _settings.WindowSize = size.Width;
            }
        }

        SizeToContent = SizeToContent.Height;
        _lastSettledClientSize = ClientSize;
    }

    private void OnScreensChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (IsVisible)
            {
                PlaceOnSelectedScreen();
            }
        });
    }

    private void OnWindowBorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Allow dragging the window
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private async void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (_settings?.HideWhenDeactivated != true || _viewModel?.MainWindowVisibility != true)
        {
            return;
        }

        // Give the show animation time to start; this also avoids hiding when focus returns right away
        // (e.g. after the settings window closes).
        if (_settings.UseAnimation)
        {
            await Task.Delay(100);
        }

        if (!IsActive && _viewModel.MainWindowVisibility)
        {
            // Through the view model so the view state, last query mode and visibility event are applied.
            _viewModel.Hide();
        }
    }

    // Pasted multi-line text becomes a single-line query: line breaks are replaced with spaces.
    private async void OnQueryTextBoxPasting(object? sender, RoutedEventArgs e)
    {
        if (_queryTextBox == null || Clipboard is not { } clipboard)
        {
            return;
        }

        e.Handled = true;
        try
        {
            var text = await clipboard.TryGetTextAsync();
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            _queryTextBox.SelectedText = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        }
        catch (Exception ex)
        {
            Log.Exception(ClassName, "Failed to paste text", ex);
        }
    }

    private void OnQueryCutClick(object? sender, RoutedEventArgs e) => _queryTextBox?.Cut();

    private void OnQueryCopyClick(object? sender, RoutedEventArgs e)
    {
        if (_queryTextBox == null) return;

        if (string.IsNullOrEmpty(_queryTextBox.SelectedText) && _viewModel?.TryCopySelectedResult() == true)
        {
            return;
        }

        _queryTextBox.Copy();
    }

    private void OnQueryPasteClick(object? sender, RoutedEventArgs e) => _queryTextBox?.Paste();

    private void HandleQueryTextFocusRequest(QueryTextFocusRequest request)
    {
        if (_queryTextBox == null)
        {
            return;
        }

        if (!request.ShowWindow && !request.ActivateWindow)
        {
            ApplyQueryTextBoxSelection(request.Mode);
            return;
        }

        if (!request.ShowWindow && (!IsVisible || _queryTextBox.IsVisible != true))
        {
            return;
        }

        var revealing = request.ShowWindow && !IsVisible;
        if (revealing)
        {
            // Before activating Flow, so the "Focus" screen option still sees the previously active window.
            PlaceOnSelectedScreen();

            if (_settings?.UseSound == true)
            {
                PlayOpenSound();
            }
        }

        ActivateApplication();

        if (request.ShowWindow)
        {
            // Text and selection are applied before Show() so the first rendered frame never shows stale state.
            SynchronizeQueryTextBoxText();
            ApplyQueryTextBoxSelection(request.Mode);
            Show();
        }

        if (request.ActivateWindow)
        {
            Activate();
        }

        _queryTextBox.Focus();
        if (!request.ShowWindow)
        {
            ApplyQueryTextBoxSelection(request.Mode);
        }

        if (revealing)
        {
            // The native window may grant key focus a tick after Show(); re-assert focus only. Re-applying the
            // selection here would select (and let the next keystroke overwrite) text the user already typed.
            Dispatcher.UIThread.Post(() => _queryTextBox?.Focus(), DispatcherPriority.Input);

            if (_settings?.UseAnimation == true)
            {
                PlayShowAnimation();
            }
        }
    }

    // Port of the WPF WindowAnimation: the search/plugin icon and the clock fade in while sliding into place.
    private void PlayShowAnimation()
    {
        if (_settings == null) return;

        var length = _settings.AnimationSpeed switch
        {
            AnimationSpeeds.Slow => 560,
            AnimationSpeeds.Medium => 360,
            AnimationSpeeds.Fast => 160,
            _ => _settings.CustomAnimationLength
        };
        if (length <= 0) return;

        var duration = TimeSpan.FromMilliseconds(length);
        foreach (var name in new[] { "SearchIconCanvas", "PluginActivationIcon", "ClockPanel" })
        {
            if (this.FindControl<Control>(name) is { IsVisible: true } target)
            {
                _ = AnimateInAsync(target, duration);
            }
        }
    }

    private static async Task AnimateInAsync(Control target, TimeSpan duration, double slideOffset = 12)
    {
        try
        {
            if (target.RenderTransform is not TranslateTransform translate)
            {
                translate = new TranslateTransform();
                target.RenderTransform = translate;
            }

            var easing = new CircularEaseInOut();
            var fade = new Animation
            {
                Duration = duration,
                Easing = easing,
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(OpacityProperty, 0d) } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(OpacityProperty, target.Opacity) } },
                }
            };
            // Slide via a transition on the TranslateTransform itself; keyframe animations of transform
            // properties go through TransformAnimator, which throws for this target in Avalonia 12.
            translate.Transitions = null;
            translate.X = slideOffset;
            translate.Transitions = new Transitions
            {
                new DoubleTransition { Property = TranslateTransform.XProperty, Duration = duration, Easing = easing }
            };
            translate.X = 0;

            await fade.RunAsync(target);
        }
        catch (Exception e)
        {
            Log.Exception(ClassName, "Show animation failed", e);
        }
    }

    // Platform hooks: bring the app forward before the window is shown/activated, and hand focus back after it hides.
    partial void ActivateApplication();

    partial void ReturnFocusAfterHide();

    // Platform hook: shape the native window to match the rounded WindowBorder.
    partial void InitializeWindowShape();

    // Platform hook: the "open" sound played when the window is summoned (Settings.UseSound).
    partial void PlayOpenSound();

    private void SynchronizeQueryTextBoxText()
    {
        if (_queryTextBox == null || _viewModel == null)
        {
            return;
        }

        if (_queryTextBox.Text != _viewModel.QueryText)
        {
            _queryTextBox.Text = _viewModel.QueryText;
        }
    }

    private void ApplyQueryTextBoxSelection(QueryTextFocusMode mode)
    {
        if (_queryTextBox == null)
        {
            return;
        }

        if (mode == QueryTextFocusMode.Focus)
        {
            return;
        }

        if (mode == QueryTextFocusMode.SelectAll)
        {
            _queryTextBox.SelectionStart = 0;
            _queryTextBox.SelectionEnd = _queryTextBox.Text?.Length ?? 0;
            return;
        }

        var textLength = _queryTextBox.Text?.Length ?? 0;
        _queryTextBox.SelectionStart = textLength;
        _queryTextBox.SelectionEnd = textLength;
        _queryTextBox.CaretIndex = textLength;
    }
}
