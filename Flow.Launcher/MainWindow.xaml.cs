using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Forms;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.ViewModel;
using Screen = System.Windows.Forms.Screen;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin.SharedCommands;
using System.Windows.Threading;
using System.Windows.Data;
using ModernWpf.Controls;
using Key = System.Windows.Input.Key;
using System.Media;
using DataObject = System.Windows.DataObject;
using System.Windows.Media;
using System.Windows.Interop;
using Windows.Win32;

namespace Flow.Launcher
{
    public partial class MainWindow
    {
        #region Private Fields

        private readonly Storyboard _progressBarStoryboard = new Storyboard();
        private bool isProgressBarStoryboardPaused;
        private Settings _settings;
        private NotifyIcon _notifyIcon;
        private ContextMenu contextMenu = new ContextMenu();
        private MainViewModel _viewModel;
        private bool _animating;
        private bool isArrowKeyPressed = false;

        private MediaPlayer animationSoundWMP;
        private SoundPlayer animationSoundWPF;

        #endregion

        public MainWindow(Settings settings, MainViewModel mainVM)
        {
            DataContext = mainVM;
            _viewModel = mainVM;
            _settings = settings;

            InitializeComponent();
            // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
            InitializePosition();
            InitializePosition();

            InitSoundEffects();

            DataObject.AddPastingHandler(QueryTextBox, OnPaste);

            this.Loaded += (_, _) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                var win = HwndSource.FromHwnd(handle);
                win.AddHook(WndProc);
            };
        }

        DispatcherTimer timer = new DispatcherTimer { Interval = new TimeSpan(0, 0, 0, 0, 500), IsEnabled = false };

        public MainWindow()
        {
            InitializeComponent();
        }

        private int _initialWidth;
        private int _initialHeight;

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == PInvoke.WM_ENTERSIZEMOVE)
            {
                _initialWidth = (int)Width;
                _initialHeight = (int)Height;
                handled = true;
            }

            if (msg == PInvoke.WM_EXITSIZEMOVE)
            {
                if (_initialHeight != (int)Height)
                {
                    OnResizeEnd();
                }

                if (_initialWidth != (int)Width)
                {
                    FlowMainWindow.SizeToContent = SizeToContent.Height;
                }

                handled = true;
            }

            return IntPtr.Zero;
        }

        private void OnResizeEnd()
        {
            int shadowMargin = 0;
            if (_settings.UseDropShadowEffect)
            {
                shadowMargin = 32;
            }

            if (!_settings.KeepMaxResults)
            {
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

            FlowMainWindow.SizeToContent = SizeToContent.Height;
            _viewModel.MainWindowWidth = Width;
        }

        private void OnCopy(object sender, ExecutedRoutedEventArgs e)
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

        private void OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            var isText = e.SourceDataObject.GetDataPresent(System.Windows.DataFormats.UnicodeText, true);
            if (isText)
            {
                var text = e.SourceDataObject.GetData(System.Windows.DataFormats.UnicodeText) as string;
                text = text.Replace(Environment.NewLine, " ");
                DataObject data = new DataObject();
                data.SetData(System.Windows.DataFormats.UnicodeText, text);
                e.DataObject = data;
            }
        }

        private async void OnClosing(object sender, CancelEventArgs e)
        {
            _notifyIcon.Visible = false;
            App.API.SaveAppAllSettings();
            e.Cancel = true;
            await PluginManager.DisposePluginsAsync();
            Notification.Uninstall();
            Environment.Exit(0);
        }

        private void OnInitialized(object sender, EventArgs e)
        {
        }

        private void OnLoaded(object sender, RoutedEventArgs _)
        {
            // MouseEventHandler
            PreviewMouseMove += MainPreviewMouseMove;
            CheckFirstLaunch();
            HideStartup();
            // show notify icon when flowlauncher is hidden
            InitializeNotifyIcon();
            InitializeColorScheme();
            WindowsInteropHelper.DisableControlBox(this);
            InitProgressbarAnimation();
            // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
            InitializePosition();
            InitializePosition();
            PreviewReset();
            // since the default main window visibility is visible
            // so we need set focus during startup
            QueryTextBox.Focus();

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
                                if (_settings.UseSound)
                                {
                                    SoundPlay();
                                }

                                UpdatePosition();
                                PreviewReset();
                                Activate();
                                QueryTextBox.Focus();
                                _settings.ActivateTimes++;
                                if (!_viewModel.LastQuerySelected)
                                {
                                    QueryTextBox.SelectAll();
                                    _viewModel.LastQuerySelected = true;
                                }

                                if (_viewModel.ProgressBarVisibility == Visibility.Visible &&
                                    isProgressBarStoryboardPaused)
                                {
                                    _progressBarStoryboard.Begin(ProgressBar, true);
                                    isProgressBarStoryboardPaused = false;
                                }

                                if (_settings.UseAnimation)
                                    WindowAnimator();
                            }
                            else if (!isProgressBarStoryboardPaused)
                            {
                                _progressBarStoryboard.Stop(ProgressBar);
                                isProgressBarStoryboardPaused = true;
                            }
                        });
                        break;
                    }
                    case nameof(MainViewModel.ProgressBarVisibility):
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (_viewModel.ProgressBarVisibility == Visibility.Hidden && !isProgressBarStoryboardPaused)
                            {
                                _progressBarStoryboard.Stop(ProgressBar);
                                isProgressBarStoryboardPaused = true;
                            }
                            else if (_viewModel.MainWindowVisibilityStatus &&
                                     isProgressBarStoryboardPaused)
                            {
                                _progressBarStoryboard.Begin(ProgressBar, true);
                                isProgressBarStoryboardPaused = false;
                            }
                        });
                        break;
                    }
                    case nameof(MainViewModel.QueryTextCursorMovedToEnd):
                        if (_viewModel.QueryTextCursorMovedToEnd)
                        {
                            MoveQueryTextToEnd();
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
                }
            };
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
                            Left = WindowsInteropHelper.TransformPixelsToDIP(this,
                                screen.WorkingArea.X + _settings.CustomWindowLeft, 0).X;
                            Top = WindowsInteropHelper.TransformPixelsToDIP(this, 0,
                                screen.WorkingArea.Y + _settings.CustomWindowTop).Y;
                            break;
                    }
                }
            }
        }

        private void UpdateNotifyIconText()
        {
            var menu = contextMenu;
            ((MenuItem)menu.Items[0]).Header = InternationalizationManager.Instance.GetTranslation("iconTrayOpen") +
                                               " (" + _settings.Hotkey + ")";
            ((MenuItem)menu.Items[1]).Header = InternationalizationManager.Instance.GetTranslation("GameMode");
            ((MenuItem)menu.Items[2]).Header = InternationalizationManager.Instance.GetTranslation("PositionReset");
            ((MenuItem)menu.Items[3]).Header = InternationalizationManager.Instance.GetTranslation("iconTraySettings");
            ((MenuItem)menu.Items[4]).Header = InternationalizationManager.Instance.GetTranslation("iconTrayExit");
        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = Infrastructure.Constant.FlowLauncherFullName,
                Icon = Constant.Version == "1.0.0" ? Properties.Resources.dev : Properties.Resources.app,
                Visible = !_settings.HideNotifyIcon
            };

            var openIcon = new FontIcon { Glyph = "\ue71e" };
            var open = new MenuItem
            {
                Header = InternationalizationManager.Instance.GetTranslation("iconTrayOpen") + " (" +
                         _settings.Hotkey + ")",
                Icon = openIcon
            };
            var gamemodeIcon = new FontIcon { Glyph = "\ue7fc" };
            var gamemode = new MenuItem
            {
                Header = InternationalizationManager.Instance.GetTranslation("GameMode"), Icon = gamemodeIcon
            };
            var positionresetIcon = new FontIcon { Glyph = "\ue73f" };
            var positionreset = new MenuItem
            {
                Header = InternationalizationManager.Instance.GetTranslation("PositionReset"),
                Icon = positionresetIcon
            };
            var settingsIcon = new FontIcon { Glyph = "\ue713" };
            var settings = new MenuItem
            {
                Header = InternationalizationManager.Instance.GetTranslation("iconTraySettings"),
                Icon = settingsIcon
            };
            var exitIcon = new FontIcon { Glyph = "\ue7e8" };
            var exit = new MenuItem
            {
                Header = InternationalizationManager.Instance.GetTranslation("iconTrayExit"), Icon = exitIcon
            };

            open.Click += (o, e) => _viewModel.ToggleFlowLauncher();
            gamemode.Click += (o, e) => _viewModel.ToggleGameMode();
            positionreset.Click += (o, e) => PositionReset();
            settings.Click += (o, e) => App.API.OpenSettingDialog();
            exit.Click += (o, e) => Close();

            gamemode.ToolTip = InternationalizationManager.Instance.GetTranslation("GameModeToolTip");
            positionreset.ToolTip = InternationalizationManager.Instance.GetTranslation("PositionResetToolTip");

            contextMenu.Items.Add(open);
            contextMenu.Items.Add(gamemode);
            contextMenu.Items.Add(positionreset);
            contextMenu.Items.Add(settings);
            contextMenu.Items.Add(exit);

            _notifyIcon.MouseClick += (o, e) =>
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        _viewModel.ToggleFlowLauncher();
                        break;
                    case MouseButtons.Right:

                        contextMenu.IsOpen = true;
                        // Get context menu handle and bring it to the foreground
                        if (PresentationSource.FromVisual(contextMenu) is HwndSource hwndSource)
                        {
                            PInvoke.SetForegroundWindow(new(hwndSource.Handle));
                        }

                        contextMenu.Focus();
                        break;
                }
            };
        }

        private void CheckFirstLaunch()
        {
            if (_settings.FirstLaunch)
            {
                _settings.FirstLaunch = false;
                PluginManager.API.SaveAppAllSettings();
                OpenWelcomeWindow();
            }
        }

        private void OpenWelcomeWindow()
        {
            var WelcomeWindow = new WelcomeWindow(_settings);
            WelcomeWindow.Show();
        }

        private async void PositionReset()
        {
            _viewModel.Show();
            await Task.Delay(300); // If don't give a time, Positioning will be weird.
            var screen = SelectedScreen();
            Left = HorizonCenter(screen);
            Top = VerticalCenter(screen);
        }

        private void InitProgressbarAnimation()
        {
            var da = new DoubleAnimation(ProgressBar.X2, ActualWidth + 100,
                new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            var da1 = new DoubleAnimation(ProgressBar.X1, ActualWidth + 0,
                new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            Storyboard.SetTargetProperty(da, new PropertyPath("(Line.X2)"));
            Storyboard.SetTargetProperty(da1, new PropertyPath("(Line.X1)"));
            _progressBarStoryboard.Children.Add(da);
            _progressBarStoryboard.Children.Add(da1);
            _progressBarStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            _viewModel.ProgressBarVisibility = Visibility.Hidden;
            isProgressBarStoryboardPaused = true;
        }

        public void WindowAnimator()
        {
            if (_animating)
                return;

            isArrowKeyPressed = true;
            _animating = true;
            UpdatePosition();

            Storyboard windowsb = new Storyboard();
            Storyboard clocksb = new Storyboard();
            Storyboard iconsb = new Storyboard();
            CircleEase easing = new CircleEase();
            easing.EasingMode = EasingMode.EaseInOut;

            var animationLength = _settings.AnimationSpeed switch
            {
                AnimationSpeeds.Slow => 560,
                AnimationSpeeds.Medium => 360,
                AnimationSpeeds.Fast => 160,
                _ => _settings.CustomAnimationLength
            };

            var WindowOpacity = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(animationLength * 2 / 3),
                FillBehavior = FillBehavior.Stop
            };

            var WindowMotion = new DoubleAnimation
            {
                From = Top + 10,
                To = Top,
                Duration = TimeSpan.FromMilliseconds(animationLength * 2 / 3),
                FillBehavior = FillBehavior.Stop
            };
            var IconMotion = new DoubleAnimation
            {
                From = 12,
                To = 0,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.Stop
            };

            var ClockOpacity = new DoubleAnimation
            {
                From = 0,
                To = 1,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.Stop
            };
            double TargetIconOpacity = SearchIcon.Opacity; // Animation Target Opacity from Style
            var IconOpacity = new DoubleAnimation
            {
                From = 0,
                To = TargetIconOpacity,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.Stop
            };

            double right = ClockPanel.Margin.Right;
            var thicknessAnimation = new ThicknessAnimation
            {
                From = new Thickness(0, 12, right, 0),
                To = new Thickness(0, 0, right, 0),
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.Stop
            };

            Storyboard.SetTargetProperty(ClockOpacity, new PropertyPath(OpacityProperty));
            Storyboard.SetTargetName(thicknessAnimation, "ClockPanel");
            Storyboard.SetTargetProperty(thicknessAnimation, new PropertyPath(MarginProperty));
            Storyboard.SetTarget(WindowOpacity, this);
            Storyboard.SetTargetProperty(WindowOpacity, new PropertyPath(Window.OpacityProperty));
            Storyboard.SetTargetProperty(WindowMotion, new PropertyPath(Window.TopProperty));
            Storyboard.SetTargetProperty(IconMotion, new PropertyPath(TopProperty));
            Storyboard.SetTargetProperty(IconOpacity, new PropertyPath(OpacityProperty));

            clocksb.Children.Add(thicknessAnimation);
            clocksb.Children.Add(ClockOpacity);
            windowsb.Children.Add(WindowOpacity);
            windowsb.Children.Add(WindowMotion);
            iconsb.Children.Add(IconMotion);
            iconsb.Children.Add(IconOpacity);

            windowsb.Completed += (_, _) => _animating = false;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            isArrowKeyPressed = false;

            if (QueryTextBox.Text.Length == 0)
            {
                clocksb.Begin(ClockPanel);
            }

            iconsb.Begin(SearchIcon);
            windowsb.Begin(FlowMainWindow);
        }

        private void InitSoundEffects()
        {
            if (_settings.WMPInstalled)
            {
                animationSoundWMP = new MediaPlayer();
                animationSoundWMP.Open(new Uri(AppDomain.CurrentDomain.BaseDirectory + "Resources\\open.wav"));
            }
            else
            {
                animationSoundWPF = new SoundPlayer(AppDomain.CurrentDomain.BaseDirectory + "Resources\\open.wav");
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

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        private async void OnContextMenusForSettingsClick(object sender, RoutedEventArgs e)
        {
            _viewModel.Hide();

            if (_settings.UseAnimation)
                await Task.Delay(100);

            App.API.OpenSettingDialog();
        }

        private async void OnDeactivated(object sender, EventArgs e)
        {
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            //This condition stops extra hide call when animator is on,
            // which causes the toggling to occasional hide instead of show.
            if (_viewModel.MainWindowVisibilityStatus)
            {
                // Need time to initialize the main query window animation.
                // This also stops the mainwindow from flickering occasionally after Settings window is opened
                // and always after Settings window is closed.
                if (_settings.UseAnimation)
                    await Task.Delay(100);

                if (_settings.HideWhenDeactivated && !_viewModel.ExternalPreviewVisible)
                {
                    _viewModel.Hide();
                }
            }
        }

        private void UpdatePosition()
        {
            if (_animating)
                return;

            // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
            InitializePosition();
            InitializePosition();
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (_animating)
                return;
            if (_settings.SearchWindowScreen == SearchWindowScreens.RememberLastLaunchLocation)
            {
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }
        }

        public void HideStartup()
        {
            UpdatePosition();
            if (_settings.HideOnStartup)
            {
                _viewModel.Hide();
            }
            else
            {
                _viewModel.Show();
            }
        }

        public Screen SelectedScreen()
        {
            Screen screen = null;
            switch (_settings.SearchWindowScreen)
            {
                case SearchWindowScreens.Cursor:
                    screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
                    break;
                case SearchWindowScreens.Primary:
                    screen = Screen.PrimaryScreen;
                    break;
                case SearchWindowScreens.Focus:
                    var foregroundWindowHandle = PInvoke.GetForegroundWindow().Value;
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

        public double HorizonCenter(Screen screen)
        {
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        public double VerticalCenter(Screen screen)
        {
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - QueryTextBox.ActualHeight) / 4 + dip1.Y;
            return top;
        }

        public double HorizonRight(Screen screen)
        {
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip1.X + dip2.X - ActualWidth) - 10;
            return left;
        }

        public double HorizonLeft(Screen screen)
        {
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var left = dip1.X + 10;
            return left;
        }

        /// <summary>
        /// Register up and down key
        /// todo: any way to put this in xaml ?
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            var specialKeyState = GlobalHotkey.CheckModifiers();
            switch (e.Key)
            {
                case Key.Down:
                    isArrowKeyPressed = true;
                    _viewModel.SelectNextItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Up:
                    isArrowKeyPressed = true;
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
                    if (_viewModel.SelectedIsFromQueryResults()
                        && QueryTextBox.CaretIndex == QueryTextBox.Text.Length
                        && !string.IsNullOrEmpty(QueryTextBox.Text))
                    {
                        _viewModel.LoadContextMenuCommand.Execute(null);
                        e.Handled = true;
                    }

                    break;
                case Key.Left:
                    if (!_viewModel.SelectedIsFromQueryResults() && QueryTextBox.CaretIndex == 0)
                    {
                        _viewModel.EscCommand.Execute(null);
                        e.Handled = true;
                    }

                    break;
                case Key.Back:
                    if (specialKeyState.CtrlPressed)
                    {
                        if (_viewModel.SelectedIsFromQueryResults()
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
                isArrowKeyPressed = false;
            }
        }

        private void MainPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (isArrowKeyPressed)
            {
                e.Handled = true; // Ignore Mouse Hover when press Arrowkeys
            }
        }

        public void PreviewReset()
        {
            _viewModel.ResetPreview();
        }

        private void MoveQueryTextToEnd()
        {
            // QueryTextBox seems to be update with a DispatcherPriority as low as ContextIdle.
            // To ensure QueryTextBox is up to date with QueryText from the View, we need to Dispatch with such a priority
            Dispatcher.Invoke(() => QueryTextBox.CaretIndex = QueryTextBox.Text.Length);
        }

        public void InitializeColorScheme()
        {
            if (_settings.ColorScheme == Constant.Light)
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Light;
            }
            else if (_settings.ColorScheme == Constant.Dark)
            {
                ModernWpf.ThemeManager.Current.ApplicationTheme = ModernWpf.ApplicationTheme.Dark;
            }
        }

        private void QueryTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (_viewModel.QueryText != QueryTextBox.Text)
            {
                BindingExpression be = QueryTextBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                be.UpdateSource();
            }
        }
    }
}
