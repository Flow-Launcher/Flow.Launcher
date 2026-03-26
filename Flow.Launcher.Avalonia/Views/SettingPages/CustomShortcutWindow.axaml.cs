using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using FluentAvalonia.UI.Controls;
using Flow.Launcher.Avalonia.Resource;
using Flow.Launcher.Avalonia.ViewModel;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class CustomShortcutWindow : Window, INotifyPropertyChanged
{
    private readonly Func<string, bool> _doesShortcutExist;
    private readonly string _originalShortcutKey = string.Empty;
    private readonly string _originalShortcutValue = string.Empty;
    private readonly Internationalization _i18n;
    private readonly MainViewModel _mainViewModel;
    private readonly bool _update;
    private string _shortcutKey = string.Empty;
    private string _shortcutValue = string.Empty;

    public string ShortcutKey
    {
        get => _shortcutKey;
        set => SetProperty(ref _shortcutKey, value);
    }

    public string ShortcutValue
    {
        get => _shortcutValue;
        set => SetProperty(ref _shortcutValue, value);
    }

    public string ConfirmButtonText => Translate(_update ? "update" : "done", _update ? "Update" : "Done");

    public CustomShortcutWindow() : this(_ => false)
    {
    }

    public CustomShortcutWindow(Func<string, bool> doesShortcutExist)
    {
        _doesShortcutExist = doesShortcutExist;
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        InitializeComponent();
        DataContext = this;
    }

    public CustomShortcutWindow(string key, string value, Func<string, bool> doesShortcutExist) : this(doesShortcutExist)
    {
        _update = true;
        _originalShortcutKey = key;
        _originalShortcutValue = value;
        ShortcutKey = key;
        ShortcutValue = value;
        OnPropertyChanged(nameof(ConfirmButtonText));
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        this.FindControl<TextBox>("ShortcutKeyTextBox")?.Focus();
    }

    public new event PropertyChangedEventHandler? PropertyChanged;

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private async void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ShortcutKey) || string.IsNullOrWhiteSpace(ShortcutValue))
        {
            await ShowMessageAsync("Custom Shortcut", "Both the shortcut and expansion text are required.");
            return;
        }

        if (((_update && _originalShortcutKey != ShortcutKey) || !_update) && _doesShortcutExist(ShortcutKey))
        {
            await ShowMessageAsync("Custom Shortcut", "That shortcut key already exists.");
            return;
        }

        if (_update && _originalShortcutKey == ShortcutKey && _originalShortcutValue == ShortcutValue)
        {
            Close(false);
            return;
        }

        Close(true);
    }

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        _mainViewModel.ShowWithInjectedQuery(ShortcutValue);
    }

    private async System.Threading.Tasks.Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = Translate("commonOK", "OK")
        };

        await dialog.ShowAsync(this);
    }

    private string Translate(string key, string fallback)
    {
        var value = _i18n.GetTranslation(key);
        return value.StartsWith('[') ? fallback : value;
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
