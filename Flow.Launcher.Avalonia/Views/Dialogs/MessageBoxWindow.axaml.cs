using System.Windows;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Avalonia.Views.Dialogs;

/// <summary>
/// In-app message box mirroring WPF MessageBoxEx: Esc/close yields OK for OK, Cancel for OKCancel/YesNoCancel,
/// and is ignored for YesNo (an explicit choice is required, like System.Windows.MessageBox).
/// </summary>
public partial class MessageBoxWindow : Window
{
    private readonly MessageBoxButton _button;
    private MessageBoxResult _result = MessageBoxResult.None;
    private bool _answered;

    public MessageBoxWindow()
        : this(string.Empty, string.Empty, MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.OK)
    {
    }

    private MessageBoxWindow(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        _button = button;
        AvaloniaXamlLoader.Load(this);

        var title = string.IsNullOrEmpty(caption) ? Constant.FlowLauncherFullName : caption;
        Title = title;
        this.FindControl<TextBlock>("CaptionText")!.Text = title;
        this.FindControl<SelectableTextBlock>("MessageText")!.Text = messageBoxText ?? string.Empty;

        var iconText = this.FindControl<TextBlock>("IconText")!;
        iconText.Text = icon switch
        {
            MessageBoxImage.Error => "⛔",
            MessageBoxImage.Warning => "⚠",
            MessageBoxImage.Question => "?",
            MessageBoxImage.Information => "ℹ",
            _ => null
        };
        iconText.IsVisible = iconText.Text != null;

        var ok = SetupButton("OkButton", MessageBoxResult.OK, "commonOK", "OK");
        var cancel = SetupButton("CancelButton", MessageBoxResult.Cancel, "commonCancel", "Cancel");
        var yes = SetupButton("YesButton", MessageBoxResult.Yes, "commonYes", "Yes");
        var no = SetupButton("NoButton", MessageBoxResult.No, "commonNo", "No");

        ok.IsVisible = button is MessageBoxButton.OK or MessageBoxButton.OKCancel;
        cancel.IsVisible = button is MessageBoxButton.OKCancel or MessageBoxButton.YesNoCancel;
        yes.IsVisible = no.IsVisible = button is MessageBoxButton.YesNo or MessageBoxButton.YesNoCancel;

        var defaultButton = button switch
        {
            MessageBoxButton.OKCancel => defaultResult == MessageBoxResult.Cancel ? cancel : ok,
            MessageBoxButton.YesNo => defaultResult == MessageBoxResult.No ? no : yes,
            MessageBoxButton.YesNoCancel => defaultResult switch
            {
                MessageBoxResult.No => no,
                MessageBoxResult.Cancel => cancel,
                _ => yes
            },
            _ => ok
        };
        defaultButton.IsDefault = true;
        Opened += (_, _) => defaultButton.Focus();

        KeyDown += OnWindowKeyDown;
        Closing += OnWindowClosing;
    }

    /// <summary>
    /// Shows the dialog and blocks (pumping the UI thread) until it is answered. Must be called on the UI thread.
    /// </summary>
    public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, MessageBoxImage icon, MessageBoxResult defaultResult)
    {
        Dispatcher.UIThread.VerifyAccess();

        var window = new MessageBoxWindow(messageBoxText, caption, button, icon, defaultResult);
        var frame = new DispatcherFrame();
        window.Closed += (_, _) => frame.Continue = false;
        window.Show();
        window.Activate();
        Dispatcher.UIThread.PushFrame(frame);
        return window._result;
    }

    private Button SetupButton(string name, MessageBoxResult result, string key, string fallback)
    {
        var button = this.FindControl<Button>(name)!;
        button.Content = Translate(key, fallback);
        button.Tag = result;
        return button;
    }

    private static string Translate(string key, string fallback)
    {
        var value = App.API?.GetTranslation(key);
        return string.IsNullOrEmpty(value) || value.StartsWith('[') ? fallback : value;
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: MessageBoxResult result })
        {
            Answer(result);
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        e.Handled = true;
        if (TryGetCancelResult(out var result))
        {
            Answer(result);
        }
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_answered)
        {
            return;
        }

        // Title bar close behaves like Esc.
        if (TryGetCancelResult(out var result))
        {
            _result = result;
            _answered = true;
        }
        else
        {
            e.Cancel = true;
        }
    }

    private bool TryGetCancelResult(out MessageBoxResult result)
    {
        switch (_button)
        {
            case MessageBoxButton.YesNo:
                // Follow System.Windows.MessageBox behavior: no implicit answer.
                result = MessageBoxResult.None;
                return false;
            case MessageBoxButton.OK:
                result = MessageBoxResult.OK;
                return true;
            default:
                result = MessageBoxResult.Cancel;
                return true;
        }
    }

    private void Answer(MessageBoxResult result)
    {
        _result = result;
        _answered = true;
        Close();
    }
}
