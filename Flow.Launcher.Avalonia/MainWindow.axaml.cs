using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Infrastructure.UserSettings;
using System;
using System.ComponentModel;
#if DEBUG
using Avalonia.Diagnostics;
#endif

namespace Flow.Launcher.Avalonia;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private TextBox? _queryTextBox;
    private Settings? _settings;
    private KeyGesture? _previewHotkeyGesture;
    private KeyGesture? _openHistoryHotkeyGesture;
    private KeyGesture? _cycleHistoryUpHotkeyGesture;
    private KeyGesture? _cycleHistoryDownHotkeyGesture;

    public MainWindow()
    {
        InitializeComponent();

        // Get the ViewModel and Settings from DI
        _viewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _viewModel.HideRequested += () => Hide();
        _viewModel.ShowRequested += HandleShowRequested;
        _viewModel.QueryTextFocusRequested += HandleQueryTextFocusRequest;
        DataContext = _viewModel;

        // Get settings for hotkey configuration
        _settings = Ioc.Default.GetRequiredService<Settings>();
        _settings.PropertyChanged += OnSettingsPropertyChanged;
        UpdateHotkeyGestures();

        // Get reference to the query text box
        _queryTextBox = this.FindControl<TextBox>("QueryTextBox");
        _queryTextBox?.AddHandler(KeyDownEvent, OnQueryTextBoxKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);

        // Subscribe to window events
        this.Deactivated += OnWindowDeactivated;

#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Settings.PreviewHotkey)
            || e.PropertyName == nameof(Settings.OpenHistoryHotkey)
            || e.PropertyName == nameof(Settings.CycleHistoryUpHotkey)
            || e.PropertyName == nameof(Settings.CycleHistoryDownHotkey))
        {
            UpdateHotkeyGestures();
        }
    }

    private void UpdateHotkeyGestures()
    {
        if (_settings == null) return;

        _previewHotkeyGesture = ParseKeyGesture(_settings.PreviewHotkey);
        _openHistoryHotkeyGesture = ParseKeyGesture(_settings.OpenHistoryHotkey);
        _cycleHistoryUpHotkeyGesture = ParseKeyGesture(_settings.CycleHistoryUpHotkey);
        _cycleHistoryDownHotkeyGesture = ParseKeyGesture(_settings.CycleHistoryDownHotkey);
    }

    private static KeyGesture? ParseKeyGesture(string hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey)) return null;
        
        try
        {
            // Try parsing as a standard key gesture
            return KeyGesture.Parse(hotkey);
        }
        catch
        {
            // Fallback: manual parsing for common formats
            var parts = hotkey.Split('+', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return null;

            var keyPart = parts[^1].Trim();
            if (!Enum.TryParse<Key>(keyPart, true, out var key))
                return null;

            var modifiers = KeyModifiers.None;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                var mod = parts[i].Trim();
                if (Enum.TryParse<KeyModifiers>(mod, true, out var parsedMod))
                    modifiers |= parsedMod;
            }

            return new KeyGesture(key, modifiers);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Focus the query text box when window loads
        _queryTextBox?.Focus();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        // Center the window on screen
        CenterOnScreen();

        // Focus and select all text
        ApplyQueryTextBoxFocus(QueryTextFocusMode.SelectAll);
    }

    private void CenterOnScreen()
    {
        var screen = Screens.Primary;
        if (screen != null)
        {
            var workingArea = screen.WorkingArea;
            var x = (workingArea.Width - Width) / 2 + workingArea.X;
            var y = workingArea.Height * 0.25 + workingArea.Y; // Position at 25% from top (like Flow Launcher)
            Position = new PixelPoint((int)x, (int)y);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (TryHandleDynamicHotkeys(e))
        {
            return;
        }

        // Handle Escape to hide window (handled by command, but keep as fallback)
        if (e.Key == Key.Escape)
        {
            _viewModel?.EscCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Handle Right Arrow to open context menu when cursor is at end of query
        if (e.Key == Key.Right && _viewModel != null)
        {
            // Only trigger context menu if:
            // 1. We're in results view
            // 2. There's a selected result
            // 3. Cursor is at the end of the query text
            if (_viewModel.IsResultsViewActive &&
                _viewModel.Results.SelectedItem != null &&
                _queryTextBox != null &&
                _queryTextBox.CaretIndex >= (_viewModel.QueryText?.Length ?? 0))
            {
                _viewModel.LoadContextMenuCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Handle Left Arrow to go back from context menu
        if (e.Key == Key.Left && _viewModel != null && _viewModel.IsContextMenuViewActive)
        {
            _viewModel.BackToResultsCommand.Execute(null);
            e.Handled = true;
            return;
        }
    }

    private void OnWindowBorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Allow dragging the window
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    // Note: In Avalonia, use the Deactivated event instead of override
    // Subscribe in constructor: this.Deactivated += OnWindowDeactivated;
    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // Hide window when it loses focus if the setting is enabled
        if (_settings?.HideWhenDeactivated == true)
        {
            Hide();
        }
    }

    /// <summary>
    /// Shows and activates the window. Focus/caret behavior is handled by QueryTextFocusRequested.
    /// </summary>
    private void HandleShowRequested()
    {
        Show();
        Activate();
    }

    private void OnQueryTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox)
        {
            return;
        }

        TryHandleDynamicHotkeys(e);
    }

    private bool TryHandleDynamicHotkeys(KeyEventArgs e)
    {
        if (_openHistoryHotkeyGesture != null
            && e.Key == _openHistoryHotkeyGesture.Key
            && e.KeyModifiers == _openHistoryHotkeyGesture.KeyModifiers)
        {
            _viewModel?.LoadHistoryCommand.Execute(null);
            e.Handled = true;
            return true;
        }

        if (e.Source is Visual source && source.FindAncestorOfType<TextBox>() == null && e.Source is not TextBox)
        {
            return false;
        }

        if (_previewHotkeyGesture != null
            && e.Key == _previewHotkeyGesture.Key
            && e.KeyModifiers == _previewHotkeyGesture.KeyModifiers)
        {
            _viewModel?.TogglePreviewCommand.Execute(null);
            e.Handled = true;
            return true;
        }

        if (_cycleHistoryUpHotkeyGesture != null
            && e.Key == _cycleHistoryUpHotkeyGesture.Key
            && e.KeyModifiers == _cycleHistoryUpHotkeyGesture.KeyModifiers)
        {
            _viewModel?.ReverseHistoryCommand.Execute(null);
            e.Handled = true;
            return true;
        }

        if (_cycleHistoryDownHotkeyGesture != null
            && e.Key == _cycleHistoryDownHotkeyGesture.Key
            && e.KeyModifiers == _cycleHistoryDownHotkeyGesture.KeyModifiers)
        {
            _viewModel?.ForwardHistoryCommand.Execute(null);
            e.Handled = true;
            return true;
        }

        return false;
    }

    private void HandleQueryTextFocusRequest(QueryTextFocusRequest request)
    {
        if (!request.ShowWindow && (!IsVisible || _queryTextBox?.IsVisible != true))
        {
            return;
        }

        if (request.ShowWindow)
        {
            Show();
        }

        if (request.ActivateWindow)
        {
            Activate();
        }

        ApplyQueryTextBoxFocus(request.Mode);
    }

    private void ApplyQueryTextBoxFocus(QueryTextFocusMode mode)
    {
        if (_queryTextBox == null)
        {
            return;
        }

        _queryTextBox.Focus();

        if (mode == QueryTextFocusMode.SelectAll)
        {
            _queryTextBox.SelectAll();
            return;
        }

        var textLength = _queryTextBox.Text?.Length ?? 0;
        _queryTextBox.SelectionStart = textLength;
        _queryTextBox.SelectionEnd = textLength;
        _queryTextBox.CaretIndex = textLength;
    }
}
