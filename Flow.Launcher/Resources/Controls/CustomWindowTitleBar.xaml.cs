using System;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Resources.Controls
{
    public partial class CustomWindowTitleBar : UserControl
    {
        private static readonly ImageSource DefaultWindowIcon = CreateDefaultWindowIcon();

        public sealed class WindowStateChangedEventArgs : EventArgs
        {
            public WindowStateChangedEventArgs(WindowState previousState, WindowState currentState)
            {
                PreviousState = previousState;
                CurrentState = currentState;
            }

            public WindowState PreviousState { get; }

            public WindowState CurrentState { get; }
        }

        public static readonly DependencyProperty IconSourceProperty =
            DependencyProperty.Register(
                name: nameof(IconSource),
                propertyType: typeof(ImageSource),
                ownerType: typeof(CustomWindowTitleBar),
                typeMetadata: new PropertyMetadata(defaultValue: null)
            );

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                name: nameof(Title),
                propertyType: typeof(string),
                ownerType: typeof(CustomWindowTitleBar),
                typeMetadata: new PropertyMetadata(defaultValue: null)
            );

        public static readonly DependencyProperty ShowIconProperty =
            DependencyProperty.Register(
                name: nameof(ShowIcon),
                propertyType: typeof(bool),
                ownerType: typeof(CustomWindowTitleBar),
                typeMetadata: new PropertyMetadata(defaultValue: true, propertyChangedCallback: OnButtonVisibilityOptionChanged)
            );

        public static readonly DependencyProperty ShowTitleProperty =
            DependencyProperty.Register(
                name: nameof(ShowTitle),
                propertyType: typeof(bool),
                ownerType: typeof(CustomWindowTitleBar),
                typeMetadata: new PropertyMetadata(defaultValue: true, propertyChangedCallback: OnButtonVisibilityOptionChanged)
            );

        public static readonly DependencyProperty ShowMinimizeButtonProperty =
            DependencyProperty.Register(
                name: nameof(ShowMinimizeButton),
                propertyType: typeof(bool),
                ownerType: typeof(CustomWindowTitleBar),
                typeMetadata: new PropertyMetadata(defaultValue: true, propertyChangedCallback: OnButtonVisibilityOptionChanged)
            );

        public static readonly DependencyProperty ShowMaximizeRestoreButtonProperty =
            DependencyProperty.Register(
                name: nameof(ShowMaximizeRestoreButton),
                propertyType: typeof(bool),
                ownerType: typeof(CustomWindowTitleBar),
                typeMetadata: new PropertyMetadata(defaultValue: true, propertyChangedCallback: OnButtonVisibilityOptionChanged)
            );

        public static readonly DependencyProperty ShowCloseButtonProperty =
            DependencyProperty.Register(
                name: nameof(ShowCloseButton),
                propertyType: typeof(bool),
                ownerType: typeof(CustomWindowTitleBar),
                typeMetadata: new PropertyMetadata(defaultValue: true, propertyChangedCallback: OnButtonVisibilityOptionChanged)
            );

        /// <summary>
        /// Occurs when the minimize button is clicked.
        /// </summary>
        /// <remarks>
        /// Set <see cref="RoutedEventArgs.Handled"/> to <see langword="true"/> in a subscriber to suppress
        /// the control's default minimize behavior.
        /// </remarks>
        public event RoutedEventHandler MinimizeButtonClick;

        /// <summary>
        /// Occurs when the maximize or restore button is clicked.
        /// </summary>
        /// <remarks>
        /// Set <see cref="RoutedEventArgs.Handled"/> to <see langword="true"/> in a subscriber to suppress
        /// the control's default maximize/restore toggle behavior.
        /// </remarks>
        public event RoutedEventHandler MaximizeRestoreButtonClick;

        /// <summary>
        /// Occurs when the close button is clicked.
        /// </summary>
        /// <remarks>
        /// Set <see cref="RoutedEventArgs.Handled"/> to <see langword="true"/> in a subscriber to suppress
        /// the control's default host-window close behavior.
        /// </remarks>
        public event RoutedEventHandler CloseButtonClick;

        /// <summary>
        /// Occurs when <see cref="LastNonMinimizedWindowState"/> changes.
        /// </summary>
        public event EventHandler<WindowStateChangedEventArgs> LastNonMinimizedWindowStateChanged;

        private Window _hostWindow;
        private WindowState _lastNonMinimizedWindowState = WindowState.Normal;

        private Button MinimizeButtonElement => FindName("MinimizeButton") as Button;
        private Button MaximizeButtonElement => FindName("MaximizeButton") as Button;
        private Button RestoreButtonElement => FindName("RestoreButton") as Button;
        private Button CloseButtonElement => FindName("CloseButton") as Button;
        private Image IconImageElement => FindName("IconImage") as Image;
        private TextBlock TitleTextBlockElement => FindName("TitleTextBlock") as TextBlock;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomWindowTitleBar"/> class.
        /// </summary>
        public CustomWindowTitleBar()
        {
            InitializeComponent();
            Loaded += CustomWindowTitleBar_Loaded;
            Unloaded += CustomWindowTitleBar_Unloaded;
        }
        
        /// <summary>
        /// Gets or sets the icon source shown in the title bar.
        /// </summary>
        /// <remarks>
        /// If unset (<see langword="null"/>), the control falls back to the host window icon, then to the default app icon.
        /// </remarks>
        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        /// <summary>
        /// Gets or sets the title text shown in the title bar.
        /// </summary>
        /// <remarks>
        /// If unset (<see langword="null"/>), the control uses the host window title when available.
        /// </remarks>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the window icon is visible.
        /// </summary>
        public bool ShowIcon
        {
            get => (bool)GetValue(ShowIconProperty);
            set => SetValue(ShowIconProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the title text is visible.
        /// </summary>
        public bool ShowTitle
        {
            get => (bool)GetValue(ShowTitleProperty);
            set => SetValue(ShowTitleProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the minimize button is visible.
        /// </summary>
        public bool ShowMinimizeButton
        {
            get => (bool)GetValue(ShowMinimizeButtonProperty);
            set => SetValue(ShowMinimizeButtonProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the maximize/restore button is visible.
        /// </summary>
        public bool ShowMaximizeRestoreButton
        {
            get => (bool)GetValue(ShowMaximizeRestoreButtonProperty);
            set => SetValue(ShowMaximizeRestoreButtonProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the close button is visible.
        /// </summary>
        public bool ShowCloseButton
        {
            get => (bool)GetValue(ShowCloseButtonProperty);
            set => SetValue(ShowCloseButtonProperty, value);
        }

        /// <summary>
        /// Gets the last observed window state that was not minimized.
        /// </summary>
        public WindowState LastNonMinimizedWindowState {
            get => _lastNonMinimizedWindowState;
        }

        private void CustomWindowTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            AttachToHostWindow();
            UpdateElementsVisibility();
        }

        private static void OnButtonVisibilityOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CustomWindowTitleBar control)
            {
                control.UpdateElementsVisibility();
            }
        }

        private void CustomWindowTitleBar_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachFromHostWindow();
        }

        private void AttachToHostWindow()
        {
            var window = Window.GetWindow(this);
            if (_hostWindow == window)
            {
                return;
            }

            DetachFromHostWindow();

            _hostWindow = window;
            if (_hostWindow == null)
            {
                return;
            }

            if (IconSource is null)
            {
                IconSource = _hostWindow.Icon ?? DefaultWindowIcon;
            }

            if (Title is null && !string.IsNullOrEmpty(_hostWindow.Title))
            {
                Title = _hostWindow.Title;
            }

            if (_hostWindow.WindowState != WindowState.Minimized)
            {
                UpdateLastNonMinimizedWindowState(_hostWindow.WindowState);
            }

            _hostWindow.StateChanged += HostWindow_StateChanged;
            _hostWindow.Activated += HostWindow_Activated;
            _hostWindow.Closed += HostWindow_Closed;
        }

        private void DetachFromHostWindow()
        {
            if (_hostWindow == null)
            {
                return;
            }

            _hostWindow.StateChanged -= HostWindow_StateChanged;
            _hostWindow.Activated -= HostWindow_Activated;
            _hostWindow.Closed -= HostWindow_Closed;
            _hostWindow = null;
        }

        private void HostWindow_StateChanged(object sender, EventArgs e)
        {
            if (_hostWindow == null)
            {
                return;
            }

            if (_hostWindow.WindowState != WindowState.Minimized)
            {
                UpdateLastNonMinimizedWindowState(_hostWindow.WindowState);
            }

            UpdateElementsVisibility();
        }

        private void HostWindow_Activated(object sender, EventArgs e)
        {
            if (_hostWindow == null)
            {
                return;
            }

            // Band-aid fix: Rare edge case where Alt+Tab activates the window but doesn't trigger StateChanged.
            if (_hostWindow.WindowState == WindowState.Minimized)
            {
                _hostWindow.WindowState = _lastNonMinimizedWindowState;
            }
        }

        private void HostWindow_Closed(object sender, EventArgs e)
        {
            DetachFromHostWindow();
        }

        private void UpdateLastNonMinimizedWindowState(WindowState state)
        {
            if (state == WindowState.Minimized || _lastNonMinimizedWindowState == state)
            {
                return;
            }

            var previousState = _lastNonMinimizedWindowState;
            _lastNonMinimizedWindowState = state;
            LastNonMinimizedWindowStateChanged?.Invoke(this,
                new WindowStateChangedEventArgs(previousState, _lastNonMinimizedWindowState)
            );
        }

        private void UpdateElementsVisibility()
        {
            var iconImage = IconImageElement;
            if (iconImage != null)
            {
                iconImage.Visibility = ShowIcon ? Visibility.Visible : Visibility.Collapsed;
            }

            var titleTextBlock = TitleTextBlockElement;
            if (titleTextBlock != null)
            {
                titleTextBlock.Visibility = ShowTitle ? Visibility.Visible : Visibility.Collapsed;
            }

            var minimizeButton = MinimizeButtonElement;
            if (minimizeButton != null)
            {
                minimizeButton.Visibility = ShowMinimizeButton ? Visibility.Visible : Visibility.Collapsed;
            }

            var closeButton = CloseButtonElement;
            if (closeButton != null)
            {
                closeButton.Visibility = ShowCloseButton ? Visibility.Visible : Visibility.Collapsed;
            }

            var maximizeButton = MaximizeButtonElement;
            var restoreButton = RestoreButtonElement;
            if (maximizeButton == null || restoreButton == null)
            {
                return;
            }

            if (!ShowMaximizeRestoreButton)
            {
                maximizeButton.Visibility = Visibility.Collapsed;
                restoreButton.Visibility = Visibility.Collapsed;
                return;
            }

            if (_hostWindow?.WindowState == WindowState.Maximized)
            {
                maximizeButton.Visibility = Visibility.Collapsed;
                restoreButton.Visibility = Visibility.Visible;
            }
            else
            {
                maximizeButton.Visibility = Visibility.Visible;
                restoreButton.Visibility = Visibility.Collapsed;
            }
        }

        private void MinimizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ShowMinimizeButton)
            {
                return;
            }

            MinimizeButtonClick?.Invoke(this, e);

            // External handlers can override the built-in behavior by marking the routed event as handled.
            if (e.Handled)
            {
                return;
            }

            if (_hostWindow == null)
            {
                return;
            }

            _hostWindow.WindowState = WindowState.Minimized;
        }

        private void MaximizeRestoreButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ShowMaximizeRestoreButton)
            {
                return;
            }

            MaximizeRestoreButtonClick?.Invoke(this, e);

            // External handlers can override the built-in behavior by marking the routed event as handled.
            if (e.Handled)
            {
                return;
            }

            if (_hostWindow == null)
            {
                return;
            }

            _hostWindow.WindowState = _hostWindow.WindowState switch
            {
                WindowState.Maximized => WindowState.Normal,
                _ => WindowState.Maximized
            };
        }

        private void CloseButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ShowCloseButton)
            {
                return;
            }

            CloseButtonClick?.Invoke(this, e);

            // External handlers can override the built-in behavior by marking the routed event as handled.
            if (e.Handled)
            {
                return;
            }

            if (_hostWindow == null)
            {
                return;
            }

            _hostWindow.Close();
        }

        private static ImageSource CreateDefaultWindowIcon()
        {
            var icon = new BitmapImage();
            icon.BeginInit();
            icon.UriSource = new Uri("Images/app.png", UriKind.Relative);
            icon.EndInit();
            icon.Freeze();
            return icon;
        }
    }
}
