using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Infrastructure.UserSettings;
using System;

namespace Flow.Launcher.Avalonia;

public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private TextBox? _queryTextBox;

    public MainWindow()
    {
        InitializeComponent();

        // Get the ViewModel from DI (same instance that App uses)
        _viewModel = Ioc.Default.GetRequiredService<MainViewModel>();
        _viewModel.HideRequested += () => Hide();
        _viewModel.ShowRequested += () => ShowAndFocus();
        DataContext = _viewModel;

        // Get reference to the query text box
        _queryTextBox = this.FindControl<TextBox>("QueryTextBox");

        // Subscribe to window events
        this.Deactivated += OnWindowDeactivated;

#if DEBUG
        this.AttachDevTools();
#endif
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
        if (_queryTextBox != null)
        {
            _queryTextBox.Focus();
            _queryTextBox.SelectAll();
        }
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
        // Optionally hide window when it loses focus (like original Flow Launcher)
        // Uncomment if desired:
        // Hide();
    }

    /// <summary>
    /// Shows the window and focuses the query text box
    /// </summary>
    public void ShowAndFocus()
    {
        Show();
        Activate();
        _queryTextBox?.Focus();
        _queryTextBox?.SelectAll();
    }
}
