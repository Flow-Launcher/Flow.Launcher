#nullable enable
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Flow.Launcher.Plugin.Explorer.ViewModels;

namespace Flow.Launcher.Plugin.Explorer.Views.Avalonia;

public partial class ActionKeywordSetting : Window, INotifyPropertyChanged
{
    private ActionKeywordModel CurrentActionKeyword { get; }

    private string _actionKeyword = string.Empty;
    private bool _keywordEnabled;

    public string ActionKeyword
    {
        get => _actionKeyword;
        set
        {
            // Set Enable to be true if user changes ActionKeyword
            KeywordEnabled = true;
            SetProperty(ref _actionKeyword, value);
        }
    }

    public bool KeywordEnabled
    {
        get => _keywordEnabled;
        set => SetProperty(ref _keywordEnabled, value);
    }

    public ActionKeywordSetting()
    {
        CurrentActionKeyword = new ActionKeywordModel(Settings.ActionKeyword.SearchActionKeyword, "");
        InitializeComponent();
        DataContext = this;
    }

    public ActionKeywordSetting(ActionKeywordModel selectedActionKeyword)
    {
        CurrentActionKeyword = selectedActionKeyword;
        _actionKeyword = selectedActionKeyword.Keyword;
        _keywordEnabled = selectedActionKeyword.Enabled;

        InitializeComponent();
        DataContext = this;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        this.FindControl<TextBox>("TxtCurrentActionKeyword")?.Focus();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDoneButtonClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ActionKeyword))
            ActionKeyword = Query.GlobalPluginWildcardSign;

        if (CurrentActionKeyword.Keyword == ActionKeyword && CurrentActionKeyword.Enabled == KeywordEnabled)
        {
            Close(false);
            return;
        }

        if (ActionKeyword == Query.GlobalPluginWildcardSign)
            switch (CurrentActionKeyword.KeywordProperty, KeywordEnabled)
            {
                case (Settings.ActionKeyword.FileContentSearchActionKeyword, true):
                    Main.Context.API.ShowMsgBox(Localize.plugin_explorer_globalActionKeywordInvalid());
                    return;
                case (Settings.ActionKeyword.QuickAccessActionKeyword, true):
                    Main.Context.API.ShowMsgBox(Localize.plugin_explorer_quickaccess_globalActionKeywordInvalid());
                    return;
            }

        if (!KeywordEnabled || !Main.Context.API.ActionKeywordAssigned(ActionKeyword))
        {
            Close(true);
            return;
        }

        // The keyword is not valid, so show message
        Main.Context.API.ShowMsgBox(Localize.plugin_explorer_new_action_keyword_assigned());
    }

    private void BtnCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    private void TxtCurrentActionKeyword_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OnDoneButtonClick(sender, e);
            e.Handled = true;
        }
        if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    #region INotifyPropertyChanged

    public new event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    #endregion
}
