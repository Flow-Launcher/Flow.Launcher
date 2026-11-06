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
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher.Avalonia.Views.SettingPages;

public partial class CustomQueryHotkeyWindow : Window, INotifyPropertyChanged
{
    private readonly Func<string, bool> _doesHotkeyExist;
    private readonly string _originalHotkey = string.Empty;
    private readonly string _originalActionKeyword = string.Empty;
    private readonly Internationalization _i18n;
    private readonly MainViewModel _mainViewModel;
    private readonly bool _update;
    private string _hotkey = string.Empty;
    private string _actionKeyword = string.Empty;

    public string Hotkey
    {
        get => _hotkey;
        set => SetProperty(ref _hotkey, value);
    }

    public string ActionKeyword
    {
        get => _actionKeyword;
        set => SetProperty(ref _actionKeyword, value);
    }

    public string ConfirmButtonText => Translate(_update ? "update" : "done", _update ? "Update" : "Done");

    public CustomQueryHotkeyWindow() : this(_ => false)
    {
    }

    public CustomQueryHotkeyWindow(Func<string, bool> doesHotkeyExist)
    {
        _doesHotkeyExist = doesHotkeyExist;
        _i18n = Ioc.Default.GetRequiredService<Internationalization>();
        _mainViewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        InitializeComponent();
        DataContext = this;
    }

    public CustomQueryHotkeyWindow(CustomPluginHotkey hotkey, Func<string, bool> doesHotkeyExist) : this(doesHotkeyExist)
    {
        _update = true;
        _originalHotkey = hotkey.Hotkey;
        _originalActionKeyword = hotkey.ActionKeyword;
        Hotkey = hotkey.Hotkey;
        ActionKeyword = hotkey.ActionKeyword;
        OnPropertyChanged(nameof(ConfirmButtonText));
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        this.FindControl<TextBox>("ActionKeywordTextBox")?.Focus();
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
        if (string.IsNullOrWhiteSpace(Hotkey) || string.IsNullOrWhiteSpace(ActionKeyword))
        {
            await ShowMessageAsync("Custom Query Hotkey", "Both the hotkey and query text are required.");
            return;
        }

        if (((_update && _originalHotkey != Hotkey) || !_update) && _doesHotkeyExist(Hotkey))
        {
            await ShowMessageAsync("Custom Query Hotkey", "That hotkey is already assigned to another custom query.");
            return;
        }

        if (_update && _originalHotkey == Hotkey && _originalActionKeyword == ActionKeyword)
        {
            Close(false);
            return;
        }

        Close(true);
    }

    private void OnPreviewClick(object? sender, RoutedEventArgs e)
    {
        _mainViewModel.ShowWithInjectedQuery(ActionKeyword);
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
