using System;
using System.ComponentModel;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Shell;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.Image;
using Flow.Launcher.Infrastructure.QuickSwitch;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin.SharedCommands;
using Flow.Launcher.ViewModel;
using ModernWpf.Controls;
using DataObject = System.Windows.DataObject;
using Key = System.Windows.Input.Key;
using MouseButtons = System.Windows.Forms.MouseButtons;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using Screen = System.Windows.Forms.Screen;

namespace Flow.Launcher
{
    public partial class MainWindow : IDisposable
    {
        #region Public Property

        // Window Event: Close Event
        public bool CanClose { get; set; } = false;

        #endregion

        #region Private Fields

        // Dependency Injection
        private readonly Settings _settings;
        private readonly Theme _theme;

        // Window Notify Icon
        private NotifyIcon _notifyIcon;

        // Window Context Menu
        private readonly ContextMenu _contextMenu = new();
        private readonly MainViewModel _viewModel;

        // Window Event: Key Event
        private bool _isArrowKeyPressed = false;

        // Window Sound Effects
        private MediaPlayer animationSoundWMP;
        private SoundPlayer animationSoundWPF;

        // Window WndProc
        private HwndSource _hwndSource;
        private int _initialWidth;
        private int _initialHeight;

        // Window Animation
        private const double DefaultRightMargin = 66; //* this value from base.xaml
        private bool _isClockPanelAnimating = false;

        // IDisposable
        private bool _disposed = false;

        #endregion

        #region Constructor

        public MainWindow()
        {
            _settings = Ioc.Default.GetRequiredService<Settings>();
            _theme = Ioc.Default.GetRequiredService<Theme>();
            _viewModel = Ioc.Default.GetRequiredService<MainViewModel>();
            DataContext = _viewModel;

            InitializeComponent();
            UpdatePosition();

            InitSoundEffects();
            DataObject.AddPastingHandler(QueryTextBox, QueryTextBox_OnPaste);
        }

        #endregion

        #region Window Event

#pragma warning disable VSTHRD100 // Avoid async void methods

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            var handle = Win32Helper.GetWindowHandle(this, true);
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource.AddHook(WndProc);
            Win32Helper.HideFromAltTab(this);
            Win32Helper.DisableControlBox(this);
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Check first launch
            if (_settings.FirstLaunch)
            {
                // Set First Launch to false
                _settings.FirstLaunch = false;

                // Set Backdrop Type to Acrylic for Windows 11 when First Launch. Default is None
                if (Win32Helper.IsBackdropSupported()) _settings.BackdropType = BackdropTypes.Acrylic;

                // Save settings
                App.API.SaveAppAllSettings();

                // Show Welcome Window
                var welcomeWindow = new WelcomeWindow();
                welcomeWindow.Show();
            }

            // Initialize place holder
            SetupPlaceholderText();
            _viewModel.PlaceholderText = _settings.PlaceholderText;

            // Hide window if need
            UpdatePosition();
            if (_settings.HideOnStartup)
            {
                _viewModel.Hide();
                _viewModel.InitializeVisibilityStatus(false);
            }
            else
            {
                _viewModel.Show();
                _viewModel.InitializeVisibilityStatus(true);
            }

            // Show notify icon when flowlauncher is hidden
            InitializeNotifyIcon();

            // Initialize color scheme
            if (_settings.ColorScheme == Constant.Light)
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Light;
            }
            else if (_settings.ColorScheme == Constant.Dark)
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;
            }

            // Initialize position
            InitProgressbarAnimation();

            // Force update position
            UpdatePosition();

            // Refresh frame
            await _theme.RefreshFrameAsync();

            // Initialize resize mode after refreshing frame
            SetupResizeMode();

            // Reset preview
            _viewModel.ResetPreview();

            // Since the default main window visibility is visible, so we need set focus during startup
            QueryTextBox.Focus();

            // Set the initial state of the QueryTextBoxCursorMovedToEnd property
            // Without this part, when shown for the first time, switching the context menu does not move the cursor to the end.
            _viewModel.QueryTextCursorMovedToEnd = false;

            // Register quick switch events
            InitializeQuickSwitch();
            
            // Initialize hotkey mapper after window is loaded
            HotKeyMapper.Initialize();

            // View model property changed event
            _viewModel.PropertyChanged += (o, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(MainViewModel.MainWindowVisibilityStatus):
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (_viewModel.MainWindowVisibilityStatus)
                                {
                                    // Play sound effect before activing the window
                                    if (_settings.UseSound && !_viewModel.IsQuickSwitchWindowUnderDialog())
                                    {
                                        SoundPlay();
                                    }

                                    // Update position & Activate
                                    UpdatePosition();
                                    Activate();

                                    // Reset preview
                                    _viewModel.ResetPreview();

                                    // Select last query if need
                                    if (!_viewModel.LastQuerySelected)
                                    {
                                        QueryTextBox.SelectAll();
                                        _viewModel.LastQuerySelected = true;
                                    }

                                    // Focus query box
                                    QueryTextBox.Focus();

                                    // Play window animation
                                    if (_settings.UseAnimation && !_viewModel.IsQuickSwitchWindowUnderDialog())
                                    {
                                        WindowAnimation();
                                    }

                                    // Update activate times
                                    _settings.ActivateTimes++;
                                }
                            });
                            break;
                        }
                    case nameof(MainViewModel.QueryTextCursorMovedToEnd):
                        if (_viewModel.QueryTextCursorMovedToEnd)
                        {
                            // QueryTextBox seems to be update with a DispatcherPriority as low as ContextIdle.
                            // To ensure QueryTextBox is up to date with QueryText from the View, we need to Dispatch with such a priority
                            Dispatcher.Invoke(() => QueryTextBox.CaretIndex = QueryTextBox.Text.Length);
                            _viewModel.QueryTextCursorMovedToEnd = false;
                        }
                        break;
                    case nameof(MainViewModel.GameModeStatus):
                        _notifyIcon.Icon = _viewModel.GameModeStatus
                            ? Properties.Resources.gamemode
                            : Properties.Resources.app;
                        break;
                }
            };

            // Settings property changed event
            _settings.PropertyChanged += (o, e) =>
            {
                switch (e.PropertyName)
                {
                    case nameof(Settings.HideNotifyIcon):
                        _notifyIcon.Visible = !_settings.HideNotifyIcon;
                        break;
                    case nameof(Settings.Language):
                        UpdateNotifyIconText();
                        break;
                    case nameof(Settings.Hotkey):
                        UpdateNotifyIconText();
                        break;
                    case nameof(Settings.WindowLeft):
                        Left = _settings.WindowLeft;
                        break;
                    case nameof(Settings.WindowTop):
                        Top = _settings.WindowTop;
                        break;
                    case nameof(Settings.ShowPlaceholder):
                        SetupPlaceholderText();
                        break;
                    case nameof(Settings.PlaceholderText):
                        _viewModel.PlaceholderText = _settings.PlaceholderText;
                        break;
                    case nameof(Settings.KeepMaxResults):
                        SetupResizeMode();
                        break;
                }
            };

            // QueryTextBox.Text change detection (modified to only work when character count is 1 or higher)
            QueryTextBox.TextChanged += (s, e) => UpdateClockPanelVisibility();

            // Detecting ContextMenu.Visibility changes
            DependencyPropertyDescriptor
                .FromProperty(VisibilityProperty, typeof(ContextMenu))
                .AddValueChanged(ContextMenu, (s, e) => UpdateClockPanelVisibility());

            // Detect History.Visibility changes
            DependencyPropertyDescriptor
                .FromProperty(VisibilityProperty, typeof(StackPanel))
                .AddValueChanged(History, (s, e) => UpdateClockPanelVisibility());
        }

        private async void OnClosing(object sender, CancelEventArgs e)
        {
            if (!CanClose)
            {
                CanClose = true;
                _notifyIcon.Visible = false;
                App.API.SaveAppAllSettings();
                e.Cancel = true;
                await ImageLoader.WaitSaveAsync();
                await PluginManager.DisposePluginsAsync();
                Notification.Uninstall();
                // After plugins are all disposed, we shutdown application to close app
                // We use this instead of Close() to avoid InvalidOperationException when calling Close() in OnClosing event
                Application.Current.Shutdown();
            }
        }

        private void OnClosed(object sender, EventArgs e)
        {
            try
            {
                _hwndSource.RemoveHook(WndProc);
            }
            catch (Exception)
            {
                // Ignored
            }

            _hwndSource = null;
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (_viewModel.IsQuickSwitchWindowUnderDialog())
            {
                return;
            }

            if (_settings.SearchWindowScreen == SearchWindowScreens.RememberLastLaunchLocation)
            {
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }
        }

        private async void OnDeactivated(object sender, EventArgs e)
        {
            if (_viewModel.IsQuickSwitchWindowUnderDialog())
            {
                return;
            }

            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;

            _viewModel.ClockPanelOpacity = 0.0;
            _viewModel.SearchIconOpacity = 0.0;

            // This condition stops extra hide call when animator is on,
            // which causes the toggling to occasional hide instead of show.
            if (_viewModel.MainWindowVisibilityStatus)
            {
                // Need time to initialize the main query window animation.
                // This also stops the mainwindow from flickering occasionally after Settings window is opened
                // and always after Settings window is closed.
                if (_settings.UseAnimation)
                {
                    await Task.Delay(100);
                }

                if (_settings.HideWhenDeactivated && !_viewModel.ExternalPreviewVisible)
                {
                    _viewModel.Hide();
                }
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            var specialKeyState = GlobalHotkey.CheckModifiers();
            switch (e.Key)
            {
                case Key.Down:
                    _isArrowKeyPressed = true;
                    _viewModel.SelectNextItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Up:
                    _isArrowKeyPressed = true;
                    _viewModel.SelectPrevItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.PageDown:
                    _viewModel.SelectNextPageCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.PageUp:
                    _viewModel.SelectPrevPageCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Right:
                    if (_viewModel.QueryResultsSelected()
                        && QueryTextBox.CaretIndex == QueryTextBox.Text.Length
                        && !string.IsNullOrEmpty(QueryTextBox.Text))
                    {
                        _viewModel.LoadContextMenuCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Left:
                    if (!_viewModel.QueryResultsSelected() && QueryTextBox.CaretIndex == 0)
                    {
                        _viewModel.EscCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;
                case Key.Back:
                    if (specialKeyState.CtrlPressed)
                    {
                        if (_viewModel.QueryResultsSelected()
                            && QueryTextBox.Text.Length > 0
                            && QueryTextBox.CaretIndex == QueryTextBox.Text.Length)
                        {
                            var queryWithoutActionKeyword =
                                QueryBuilder.Build(QueryTextBox.Text.Trim(), PluginManager.NonGlobalPlugins)?.Search;

                            if (FilesFolders.IsLocationPathString(queryWithoutActionKeyword))
                            {
                                _viewModel.BackspaceCommand.Execute(null);
                                e.Handled = true;
                            }
                        }
                    }
                    break;
                default:
                    break;
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Down)
            {
                _isArrowKeyPressed = false;
            }
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_isArrowKeyPressed)
            {
                e.Handled = true; // Ignore Mouse Hover when press Arrowkeys
            }
        }

#pragma warning restore VSTHRD100 // Avoid async void methods

        #endregion

        #region Window Boarder Event

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        #endregion

        #region Window Context Menu Event

#pragma warning disable VSTHRD100 // Avoid async void methods

        private async void OnContextMenusForSettingsClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Hide();

            if (_settings.UseAnimation)
                await Task.Delay(100);

            App.API.OpenSettingDialog();
        }

#pragma warning restore VSTHRD100 // Avoid async void methods

        #endregion

        #region Window WndProc

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_viewModel.IsQuickSwitchWindowUnderDialog())
            {
                return IntPtr.Zero;
            }

            if (msg == Win32Helper.WM_ENTERSIZEMOVE)
            {
                _initialWidth = (int)Width;
                _initialHeight = (int)Height;

                handled = true;
            }
            else if (msg == Win32Helper.WM_EXITSIZEMOVE)
            {
                if (_initialHeight != (int)Height)
                {
                    if (!_settings.KeepMaxResults)
                    {
                        // Get shadow margin
                        var shadowMargin = 0;
                        var (_, useDropShadowEffect) = _theme.GetActualValue();
                        if (useDropShadowEffect)
                        {
                            shadowMargin = 32;
                        }

                        // Calculate max results to show
                        var itemCount = (Height - (_settings.WindowHeightSize + 14) - shadowMargin) / _settings.ItemHeightSize;
                        if (itemCount < 2)
                        {
                            _settings.MaxResultsToShow = 2;
                        }
                        else
                        {
                            _settings.MaxResultsToShow = Convert.ToInt32(Math.Truncate(itemCount));
                        }
                    }

                    SizeToContent = SizeToContent.Height;
                }

                if (_initialWidth != (int)Width)
                {
                    if (!_settings.KeepMaxResults)
                    {
                        // Update width
                        _viewModel.MainWindowWidth = Width;
                    }

                    SizeToContent = SizeToContent.Height;
                }

                handled = true;
            }

            return IntPtr.Zero;
        }

        #endregion

        #region Window Sound Effects

        private void InitSoundEffects()
        {
            if (_settings.WMPInstalled)
            {
                animationSoundWMP = new MediaPlayer();
                animationSoundWMP.Open(new Uri(AppContext.BaseDirectory + "Resources\\open.wav"));
            }
            else
            {
                animationSoundWPF = new SoundPlayer(AppContext.BaseDirectory + "Resources\\open.wav");
            }
        }

        private void SoundPlay()
        {
            if (_settings.WMPInstalled)
            {
                animationSoundWMP.Position = TimeSpan.Zero;
                animationSoundWMP.Volume = _settings.SoundVolume / 100.0;
                animationSoundWMP.Play();
            }
            else
            {
                animationSoundWPF.Play();
            }
        }

        #endregion

        #region Window Notify Icon

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = Constant.FlowLauncherFullName,
                Icon = Constant.Version == "1.0.0" ? Properties.Resources.dev : Properties.Resources.app,
                Visible = !_settings.HideNotifyIcon
            };
            var openIcon = new FontIcon { Glyph = "\ue71e" };
            var open = new MenuItem
            {
                Header = App.API.GetTranslation("iconTrayOpen") + " (" + _settings.Hotkey + ")",
                Icon = openIcon
            };
            var gamemodeIcon = new FontIcon { Glyph = "\ue7fc" };
            var gamemode = new MenuItem
            {
                Header = App.API.GetTranslation("GameMode"),
                Icon = gamemodeIcon
            };
            var positionresetIcon = new FontIcon { Glyph = "\ue73f" };
            var positionreset = new MenuItem
            {
                Header = App.API.GetTranslation("PositionReset"),
                Icon = positionresetIcon
            };
            var settingsIcon = new FontIcon { Glyph = "\ue713" };
            var settings = new MenuItem
            {
                Header = App.API.GetTranslation("iconTraySettings"),
                Icon = settingsIcon
            };
            var exitIcon = new FontIcon { Glyph = "\ue7e8" };
            var exit = new MenuItem
            {
                Header = App.API.GetTranslation("iconTrayExit"),
                Icon = exitIcon
            };

            open.Click += (o, e) => _viewModel.ToggleFlowLauncher();
            gamemode.Click += (o, e) => _viewModel.ToggleGameMode();
            positionreset.Click += (o, e) => _ = PositionResetAsync();
            settings.Click += (o, e) => App.API.OpenSettingDialog();
            exit.Click += (o, e) => Close();

            gamemode.ToolTip = App.API.GetTranslation("GameModeToolTip");
            positionreset.ToolTip = App.API.GetTranslation("PositionResetToolTip");

            _contextMenu.Items.Add(open);
            _contextMenu.Items.Add(gamemode);
            _contextMenu.Items.Add(positionreset);
            _contextMenu.Items.Add(settings);
            _contextMenu.Items.Add(exit);

            _notifyIcon.MouseClick += (o, e) =>
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        _viewModel.ToggleFlowLauncher();
                        break;
                    case MouseButtons.Right:

                        _contextMenu.IsOpen = true;
                        // Get context menu handle and bring it to the foreground
                        if (PresentationSource.FromVisual(_contextMenu) is HwndSource hwndSource)
                        {
                            Win32Helper.SetForegroundWindow(hwndSource.Handle);
                        }

                        _contextMenu.Focus();
                        break;
                }
            };
        }

        private void UpdateNotifyIconText()
        {
            var menu = _contextMenu;
            ((MenuItem)menu.Items[0]).Header = App.API.GetTranslation("iconTrayOpen") +
                                               " (" + _settings.Hotkey + ")";
            ((MenuItem)menu.Items[1]).Header = App.API.GetTranslation("GameMode");
            ((MenuItem)menu.Items[2]).Header = App.API.GetTranslation("PositionReset");
            ((MenuItem)menu.Items[3]).Header = App.API.GetTranslation("iconTraySettings");
            ((MenuItem)menu.Items[4]).Header = App.API.GetTranslation("iconTrayExit");
        }

        #endregion

        #region Window Position

        public void UpdatePosition()
        {
            if (_viewModel.IsQuickSwitchWindowUnderDialog())
            {
                UpdateQuickSwitchPosition();
            }
            else
            {
                // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
                InitializePosition();
                InitializePosition();
            }
        }

        private async Task PositionResetAsync()
        {
            _viewModel.Show();
            await Task.Delay(300); // If don't give a time, Positioning will be weird.
            var screen = SelectedScreen();
            Left = HorizonCenter(screen);
            Top = VerticalCenter(screen);
        }

        private void InitializePosition()
        {
            // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
            InitializePositionInner();
            InitializePositionInner();
            return;

            void InitializePositionInner()
            {
                if (_settings.SearchWindowScreen == SearchWindowScreens.RememberLastLaunchLocation)
                {
                    Top = _settings.WindowTop;
                    Left = _settings.WindowLeft;
                }
                else
                {
                    var screen = SelectedScreen();
                    switch (_settings.SearchWindowAlign)
                    {
                        case SearchWindowAligns.Center:
                            Left = HorizonCenter(screen);
                            Top = VerticalCenter(screen);
                            break;
                        case SearchWindowAligns.CenterTop:
                            Left = HorizonCenter(screen);
                            Top = 10;
                            break;
                        case SearchWindowAligns.LeftTop:
                            Left = HorizonLeft(screen);
                            Top = 10;
                            break;
                        case SearchWindowAligns.RightTop:
                            Left = HorizonRight(screen);
                            Top = 10;
                            break;
                        case SearchWindowAligns.Custom:
                            Left = Win32Helper.TransformPixelsToDIP(this,
                                screen.WorkingArea.X + _settings.CustomWindowLeft, 0).X;
                            Top = Win32Helper.TransformPixelsToDIP(this, 0,
                                screen.WorkingArea.Y + _settings.CustomWindowTop).Y;
                            break;
                    }
                }
            }
        }

        private Screen SelectedScreen()
        {
            Screen screen;
            switch (_settings.SearchWindowScreen)
            {
                case SearchWindowScreens.Cursor:
                    screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
                    break;
                case SearchWindowScreens.Primary:
                    screen = Screen.PrimaryScreen;
                    break;
                case SearchWindowScreens.Focus:
                    var foregroundWindowHandle = Win32Helper.GetForegroundWindow();
                    screen = Screen.FromHandle(foregroundWindowHandle);
                    break;
                case SearchWindowScreens.Custom:
                    if (_settings.CustomScreenNumber <= Screen.AllScreens.Length)
                        screen = Screen.AllScreens[_settings.CustomScreenNumber - 1];
                    else
                        screen = Screen.AllScreens[0];
                    break;
                default:
                    screen = Screen.AllScreens[0];
                    break;
            }

            return screen ?? Screen.AllScreens[0];
        }

        private double HorizonCenter(Screen screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        private double VerticalCenter(Screen screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - QueryTextBox.ActualHeight) / 4 + dip1.Y;
            return top;
        }

        private double HorizonRight(Screen screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip1.X + dip2.X - ActualWidth) - 10;
            return left;
        }

        private double HorizonLeft(Screen screen)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var left = dip1.X + 10;
            return left;
        }

        #endregion

        #region Window Animation

        private void InitProgressbarAnimation()
        {
            var progressBarStoryBoard = new Storyboard();

            var da = new DoubleAnimation(ProgressBar.X2, ActualWidth + 100,
                new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            var da1 = new DoubleAnimation(ProgressBar.X1, ActualWidth + 0,
                new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            Storyboard.SetTargetProperty(da, new PropertyPath("(Line.X2)"));
            Storyboard.SetTargetProperty(da1, new PropertyPath("(Line.X1)"));
            progressBarStoryBoard.Children.Add(da);
            progressBarStoryBoard.Children.Add(da1);
            progressBarStoryBoard.RepeatBehavior = RepeatBehavior.Forever;

            da.Freeze();
            da1.Freeze();

            const string progressBarAnimationName = "ProgressBarAnimation";
            var beginStoryboard = new BeginStoryboard
            {
                Name = progressBarAnimationName, Storyboard = progressBarStoryBoard
            };
            
            var stopStoryboard = new StopStoryboard()
            {
                BeginStoryboardName = progressBarAnimationName
            };

            var trigger = new Trigger
            {
                Property = VisibilityProperty, Value = Visibility.Visible
            };
            trigger.EnterActions.Add(beginStoryboard);
            trigger.ExitActions.Add(stopStoryboard);

            var progressStyle = new Style(typeof(Line))
            {
                BasedOn = FindResource("PendingLineStyle") as Style
            };
            progressStyle.RegisterName(progressBarAnimationName, beginStoryboard);
            progressStyle.Triggers.Add(trigger);

            ProgressBar.Style = progressStyle;
          
            _viewModel.ProgressBarVisibility = Visibility.Hidden;
        }

        private void WindowAnimation()
        {
            _isArrowKeyPressed = true;

            var clocksb = new Storyboard();
            var iconsb = new Storyboard();
            var easing = new CircleEase { EasingMode = EasingMode.EaseInOut };

            var animationLength = _settings.AnimationSpeed switch
            {
                AnimationSpeeds.Slow => 560,
                AnimationSpeeds.Medium => 360,
                AnimationSpeeds.Fast => 160,
                _ => _settings.CustomAnimationLength
            };

            var IconMotion = new DoubleAnimation
            {
                From = 12,
                To = 0,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.HoldEnd
            };

            var ClockOpacity = new DoubleAnimation
            {
                From = 0,
                To = 1,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.HoldEnd
            };

            var TargetIconOpacity = GetOpacityFromStyle(SearchIcon.Style, 1.0);

            var IconOpacity = new DoubleAnimation
            {
                From = 0,
                To = TargetIconOpacity,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.HoldEnd
            };
            
            var rightMargin = GetThicknessFromStyle(ClockPanel.Style, new Thickness(0, 0, DefaultRightMargin, 0)).Right;

            var thicknessAnimation = new ThicknessAnimation
            {
                From = new Thickness(0, 12, rightMargin, 0),
                To = new Thickness(0, 0, rightMargin, 0),
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.HoldEnd
            };

            Storyboard.SetTargetProperty(ClockOpacity, new PropertyPath(OpacityProperty));
            Storyboard.SetTarget(ClockOpacity, ClockPanel);

            Storyboard.SetTargetName(thicknessAnimation, "ClockPanel");
            Storyboard.SetTargetProperty(thicknessAnimation, new PropertyPath(MarginProperty));

            Storyboard.SetTarget(IconMotion, SearchIcon);
            Storyboard.SetTargetProperty(IconMotion, new PropertyPath(TopProperty));

            Storyboard.SetTarget(IconOpacity, SearchIcon);
            Storyboard.SetTargetProperty(IconOpacity, new PropertyPath(OpacityProperty));

            clocksb.Children.Add(thicknessAnimation);
            clocksb.Children.Add(ClockOpacity);
            iconsb.Children.Add(IconMotion);
            iconsb.Children.Add(IconOpacity);
            
            _settings.WindowLeft = Left;
            _isArrowKeyPressed = false;
            
            clocksb.Begin(ClockPanel);
            iconsb.Begin(SearchIcon);
        }

        private void UpdateClockPanelVisibility()
        {
            if (QueryTextBox == null || ContextMenu == null || History == null || ClockPanel == null)
            {
                return;
            }

            // ✅ Initialize animation length & duration
            var animationLength = _settings.AnimationSpeed switch
            {
                AnimationSpeeds.Slow => 560,
                AnimationSpeeds.Medium => 360,
                AnimationSpeeds.Fast => 160,
                _ => _settings.CustomAnimationLength
            };
            var animationDuration = TimeSpan.FromMilliseconds(animationLength * 2 / 3);

            // ✅ Conditions for showing ClockPanel (No query input & ContextMenu, History are closed)
            var shouldShowClock = QueryTextBox.Text.Length == 0 &&
                ContextMenu.Visibility != Visibility.Visible &&
                History.Visibility != Visibility.Visible;

            // ✅ 1. When ContextMenu opens, immediately set Visibility.Hidden (force hide without animation)
            if (ContextMenu.Visibility == Visibility.Visible)
            {
                _viewModel.ClockPanelVisibility = Visibility.Hidden;
                _viewModel.ClockPanelOpacity = 0.0;  // Set to 0 in case Opacity animation affects it
                return;
            }

            // ✅ 2. When ContextMenu is closed, keep it Hidden if there's text in the query (remember previous state)
            else if (QueryTextBox.Text.Length > 0)
            {
                _viewModel.ClockPanelVisibility = Visibility.Hidden;
                _viewModel.ClockPanelOpacity = 0.0;
                return;
            }

            // ✅ Prevent multiple animations
            if (_isClockPanelAnimating)
            {
                return;
            }

            // ✅ 3. When hiding ClockPanel (apply fade-out animation)
            if ((!shouldShowClock) && _viewModel.ClockPanelVisibility == Visibility.Visible)
            {
                _isClockPanelAnimating = true;

                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = animationDuration,
                    FillBehavior = FillBehavior.HoldEnd
                };

                fadeOut.Completed += (s, e) =>
                {
                    _viewModel.ClockPanelVisibility = Visibility.Hidden; // ✅ Completely hide after animation
                    _isClockPanelAnimating = false;
                };

                ClockPanel.BeginAnimation(OpacityProperty, fadeOut);
            }

            // ✅ 4. When showing ClockPanel (apply fade-in animation)
            else if (shouldShowClock && _viewModel.ClockPanelVisibility != Visibility.Visible)
            {
                _isClockPanelAnimating = true;

                _viewModel.ClockPanelVisibility = Visibility.Visible;  // ✅ Set Visibility to Visible first

                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = animationDuration,
                    FillBehavior = FillBehavior.HoldEnd
                };

                fadeIn.Completed += (s, e) => _isClockPanelAnimating = false;

                ClockPanel.BeginAnimation(OpacityProperty, fadeIn);
            }
        }

        private static double GetOpacityFromStyle(Style style, double defaultOpacity = 1.0)
        {
            if (style == null)
            {
                return defaultOpacity;
            }

            foreach (Setter setter in style.Setters.Cast<Setter>())
            {
                if (setter.Property == OpacityProperty)
                {
                    return setter.Value is double opacity ? opacity : defaultOpacity;
                }
            }

            return defaultOpacity;
        }

        private static Thickness GetThicknessFromStyle(Style style, Thickness defaultThickness)
        {
            if (style == null)
            {
                return defaultThickness;
            }

            foreach (Setter setter in style.Setters.Cast<Setter>())
            {
                if (setter.Property == MarginProperty)
                {
                    return setter.Value is Thickness thickness ? thickness : defaultThickness;
                }
            }

            return defaultThickness;
        }

        #endregion

        #region QueryTextBox Event

        private void QueryTextBox_OnCopy(object sender, ExecutedRoutedEventArgs e)
        {
            var result = _viewModel.Results.SelectedItem?.Result;
            if (QueryTextBox.SelectionLength == 0 && result != null)
            {
                string copyText = result.CopyText;
                App.API.CopyToClipboard(copyText, directCopy: true);
            }
            else if (!string.IsNullOrEmpty(QueryTextBox.Text))
            {
                App.API.CopyToClipboard(QueryTextBox.SelectedText, showDefaultNotification: false);
            }
        }

        private void QueryTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            var isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (isText)
            {
                var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
                text = text.Replace(Environment.NewLine, " ");
                DataObject data = new DataObject();
                data.SetData(DataFormats.UnicodeText, text);
                e.DataObject = data;
            }
        }

        private void QueryTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (_viewModel.QueryText != QueryTextBox.Text)
            {
                BindingExpression be = QueryTextBox.GetBindingExpression(TextBox.TextProperty);
                be.UpdateSource();
            }
        }

        private void QueryTextBox_OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }
        
        #endregion

        #region Placeholder

        private void SetupPlaceholderText()
        {
            if (_settings.ShowPlaceholder)
            {
                QueryTextBox.TextChanged += QueryTextBox_TextChanged;
                QueryTextSuggestionBox.TextChanged += QueryTextSuggestionBox_TextChanged;
                SetPlaceholderText();
            }
            else
            {
                QueryTextBox.TextChanged -= QueryTextBox_TextChanged;
                QueryTextSuggestionBox.TextChanged -= QueryTextSuggestionBox_TextChanged;
                QueryTextPlaceholderBox.Visibility = Visibility.Collapsed;
            }
        }

        private void QueryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetPlaceholderText();
        }

        private void QueryTextSuggestionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetPlaceholderText();
        }

        private void SetPlaceholderText()
        {
            var queryText = QueryTextBox.Text;
            var suggestionText = QueryTextSuggestionBox.Text;
            QueryTextPlaceholderBox.Visibility = string.IsNullOrEmpty(queryText) && string.IsNullOrEmpty(suggestionText) ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Resize Mode

        private void SetupResizeMode()
        {
            ResizeMode = _settings.KeepMaxResults ? ResizeMode.NoResize : ResizeMode.CanResize;
            if (WindowChrome.GetWindowChrome(this) is WindowChrome windowChrome)
            {
                _theme.SetResizeBorderThickness(windowChrome, _settings.KeepMaxResults);
            }
        }

        #endregion
        
        #region Search Delay

        private void QueryTextBox_TextChanged1(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            _viewModel.QueryText = textBox.Text;
            _viewModel.Query(_settings.SearchQueryResultsWithDelay);
        }

        #endregion

        #region Quick Switch

        private void InitializeQuickSwitch()
        {
            QuickSwitch.ShowQuickSwitchWindow = _viewModel.SetupQuickSwitch;
            QuickSwitch.UpdateQuickSwitchWindow = UpdateQuickSwitchPosition;
            QuickSwitch.ResetQuickSwitchWindow = _viewModel.ResetQuickSwitch;
            QuickSwitch.HideQuickSwitchWindow = _viewModel.HideQuickSwitch;
        }

        private void UpdateQuickSwitchPosition()
        {
            if (_viewModel.DialogWindowHandle == nint.Zero || !_viewModel.MainWindowVisibilityStatus) return;
            if (!_viewModel.IsQuickSwitchWindowUnderDialog()) return;

            // Get dialog window rect
            var result = Win32Helper.GetWindowRect(_viewModel.DialogWindowHandle, out var window);
            if (!result) return;

            // Move window below the bottom of the dialog and keep it center
            Top = VerticalBottom(window);
            Left = HorizonCenter(window);
        }

        private double HorizonCenter(Rect window)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, window.X, 0);
            var dip2 = Win32Helper.TransformPixelsToDIP(this, window.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        private double VerticalBottom(Rect window)
        {
            var dip1 = Win32Helper.TransformPixelsToDIP(this, 0, window.Bottom);
            return dip1.Y;
        }

        #endregion

        #region IDisposable

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _hwndSource?.Dispose();
                    _notifyIcon?.Dispose();
                }

                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
