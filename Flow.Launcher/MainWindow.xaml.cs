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
using Window = System.Windows.Window;
using System.Linq;
using System.Windows.Shapes;

namespace Flow.Launcher
{
    public partial class MainWindow
    {
        #region Private Fields

        private readonly Settings _settings;
        private NotifyIcon _notifyIcon;
        private readonly ContextMenu contextMenu = new();
        private readonly MainViewModel _viewModel;
        private bool _animating;
        private bool isArrowKeyPressed = false;

        private MediaPlayer animationSoundWMP;
        private SoundPlayer animationSoundWPF;

        // Window Animations
        private Storyboard clocksb;
        private Storyboard iconsb;
        private Storyboard windowsb;

        #endregion

        public MainWindow(Settings settings, MainViewModel mainVM)
        {
            DataContext = mainVM;
            _viewModel = mainVM;
            _settings = settings;

            InitializeComponent();
            
            InitSoundEffects();
            DataObject.AddPastingHandler(QueryTextBox, OnPaste);
            
            Loaded += (_, _) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                var win = HwndSource.FromHwnd(handle);
                win.AddHook(WndProc);
            };
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

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            WindowsInteropHelper.HideFromAltTab(this);
        }

        private void OnInitialized(object sender, EventArgs e)
        {
        }

        private async void OnLoaded(object sender, RoutedEventArgs _)
        {
            // MouseEventHandler
            PreviewMouseMove += MainPreviewMouseMove;
            CheckFirstLaunch();
            HideStartup();
            // Show notify icon when flowlauncher is hidden
            InitializeNotifyIcon();
            InitializeColorScheme();
            WindowsInteropHelper.DisableControlBox(this);
            InitProgressbarAnimation();
            // Move the window out of screen because setting backdrop will cause flicker with a rectangle
            Left = Top = -10000;
            await ThemeManager.Instance.RefreshFrameAsync();
            // Initialize call twice to work around multi-display alignment issue- https://github.com/Flow-Launcher/Flow.Launcher/issues/2910
            InitializePosition();
            InitializePosition();
            PreviewReset();
            // Since the default main window visibility is visible, so we need set focus during startup
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

                                if (_settings.UseAnimation)
                                    WindowAnimator();
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
            // ✅ QueryTextBox.Text 변경 감지 (글자 수 1 이상일 때만 동작하도록 수정)
            QueryTextBox.TextChanged += (sender, e) => UpdateClockPanelVisibility();

            // ✅ ContextMenu.Visibility 변경 감지
            DependencyPropertyDescriptor
                .FromProperty(UIElement.VisibilityProperty, typeof(ContextMenu))
                .AddValueChanged(ContextMenu, (s, e) => UpdateClockPanelVisibility());

            // ✅ History.Visibility 변경 감지
            DependencyPropertyDescriptor
                .FromProperty(UIElement.VisibilityProperty, typeof(StackPanel)) // History는 StackPanel이라고 가정
                .AddValueChanged(History, (s, e) => UpdateClockPanelVisibility());
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
                Text = Constant.FlowLauncherFullName,
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
                Header = InternationalizationManager.Instance.GetTranslation("GameMode"),
                Icon = gamemodeIcon
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
                Header = InternationalizationManager.Instance.GetTranslation("iconTrayExit"),
                Icon = exitIcon
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
                App.API.SaveAppAllSettings();
                OpenWelcomeWindow();
            }
        }

        private void OpenWelcomeWindow()
        {
            var WelcomeWindow = new WelcomeWindow();
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

        public void ResetAnimation()
        {
            // 애니메이션 중지
            clocksb?.Stop(ClockPanel);
            iconsb?.Stop(SearchIcon);
            windowsb?.Stop(FlowMainWindow);

            // UI 요소 상태 초기화
            //ClockPanel.Margin = new Thickness(0, 0, ClockPanel.Margin.Right, 0);
            ClockPanel.Opacity = 0;
            SearchIcon.Opacity = 0;
        }

        public void WindowAnimator()
        {
            if (_animating)
                return;

            isArrowKeyPressed = true;
            _animating = true;
            UpdatePosition();

            windowsb = new Storyboard();
            clocksb = new Storyboard();
            iconsb = new Storyboard();
            CircleEase easing = new CircleEase { EasingMode = EasingMode.EaseInOut };

            var animationLength = _settings.AnimationSpeed switch
            {
                AnimationSpeeds.Slow => 560,
                AnimationSpeeds.Medium => 360,
                AnimationSpeeds.Fast => 160,
                _ => _settings.CustomAnimationLength
            };

            var WindowOpacity = new DoubleAnimation
            {
                From = 1,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(animationLength * 2 / 3),
                FillBehavior = FillBehavior.Stop
            };

            var WindowMotion = new DoubleAnimation
            {
                From = Top,
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
                FillBehavior = FillBehavior.HoldEnd
            };

            double TargetIconOpacity = GetOpacityFromStyle(SearchIcon.Style, 1.0);

            var IconOpacity = new DoubleAnimation
            {
                From = 0,
                To = TargetIconOpacity,
                EasingFunction = easing,
                Duration = TimeSpan.FromMilliseconds(animationLength),
                FillBehavior = FillBehavior.HoldEnd
            };

            const double DefaultRightMargin = 66; //* this value from base.xaml
            double rightMargin = GetThicknessFromStyle(ClockPanel.Style, new Thickness(0, 0, DefaultRightMargin, 0)).Right;

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

            Storyboard.SetTarget(WindowOpacity, this);
            Storyboard.SetTargetProperty(WindowOpacity, new PropertyPath(Window.OpacityProperty));

            Storyboard.SetTarget(WindowMotion, this);
            Storyboard.SetTargetProperty(WindowMotion, new PropertyPath(Window.TopProperty));

            Storyboard.SetTarget(IconMotion, SearchIcon);
            Storyboard.SetTargetProperty(IconMotion, new PropertyPath(TopProperty));

            Storyboard.SetTarget(IconOpacity, SearchIcon);
            Storyboard.SetTargetProperty(IconOpacity, new PropertyPath(OpacityProperty));

            clocksb.Children.Add(thicknessAnimation);
            clocksb.Children.Add(ClockOpacity);
            windowsb.Children.Add(WindowOpacity);
            windowsb.Children.Add(WindowMotion);
            iconsb.Children.Add(IconMotion);
            iconsb.Children.Add(IconOpacity);

            windowsb.Completed += (_, _) => _animating = false;
            _settings.WindowLeft = Left;
            isArrowKeyPressed = false;

            if (QueryTextBox.Text.Length == 0)
            {
                clocksb.Begin(ClockPanel);
            }

            iconsb.Begin(SearchIcon);
            windowsb.Begin(FlowMainWindow);
        }

        private static double GetOpacityFromStyle(Style style, double defaultOpacity = 1.0)
        {
            if (style == null)
                return defaultOpacity;

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
                return defaultThickness;

            foreach (Setter setter in style.Setters.Cast<Setter>())
            {
                if (setter.Property == MarginProperty)
                {
                    return setter.Value is Thickness thickness ? thickness : defaultThickness;
                }
            }

            return defaultThickness;
        }

        private bool _isClockPanelAnimating = false; // 애니메이션 실행 중인지 여부

        private void UpdateClockPanelVisibility()
        {
            if (QueryTextBox == null || ContextMenu == null || History == null || ClockPanel == null)
                return;

            var animationLength = _settings.AnimationSpeed switch
            {
                AnimationSpeeds.Slow => 560,
                AnimationSpeeds.Medium => 360,
                AnimationSpeeds.Fast => 160,
                _ => _settings.CustomAnimationLength
            };

            var animationDuration = TimeSpan.FromMilliseconds(animationLength * 2 / 3);

            // ✅ ClockPanel이 표시될 조건 (쿼리 입력 없음 & ContextMenu, History가 닫혀 있음)
            bool shouldShowClock = QueryTextBox.Text.Length == 0 &&
                                   ContextMenu.Visibility == Visibility.Collapsed &&
                                   History.Visibility == Visibility.Collapsed;

            // ✅ ClockPanel이 숨겨질 조건 (쿼리에 글자가 있거나, ContextMenu 또는 History가 열려 있음)
            bool shouldHideClock = QueryTextBox.Text.Length > 0 ||
                                   ContextMenu.Visibility == Visibility.Visible ||
                                   History.Visibility != Visibility.Collapsed;

            // ✅ 1. ContextMenu가 열리면 즉시 Visibility.Hidden으로 설정 (애니메이션 없이 강제 숨김)
            if (ContextMenu.Visibility == Visibility.Visible)
            {
                ClockPanel.Visibility = Visibility.Hidden;
                ClockPanel.Opacity = 0.0;  // 혹시라도 Opacity 애니메이션이 영향을 줄 경우 0으로 설정
                return;
            }

            // ✅ 2. ContextMenu가 닫혔을 때, 쿼리에 글자가 남아 있다면 Hidden 상태 유지 (이전 상태 기억)
            if (ContextMenu.Visibility == Visibility.Collapsed && QueryTextBox.Text.Length > 0)
            {
                ClockPanel.Visibility = Visibility.Hidden;
                ClockPanel.Opacity = 0.0;
                return;
            }

            // ✅ 3. ClockPanel을 숨기는 경우 (페이드아웃 애니메이션 적용)
            if (shouldHideClock && ClockPanel.Visibility == Visibility.Visible && !_isClockPanelAnimating)
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
                    ClockPanel.Visibility = Visibility.Hidden; // ✅ 애니메이션 후 완전히 숨김
                    _isClockPanelAnimating = false;
                };

                ClockPanel.BeginAnimation(OpacityProperty, fadeOut);
            }
            // ✅ 4. ClockPanel을 표시하는 경우 (페이드인 애니메이션 적용)
            else if (shouldShowClock && ClockPanel.Visibility != Visibility.Visible && !_isClockPanelAnimating)
            {
                _isClockPanelAnimating = true;

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ClockPanel.Visibility = Visibility.Visible;  // ✅ Visibility를 먼저 Visible로 설정

                    var fadeIn = new DoubleAnimation
                    {
                        From = 0.0,
                        To = 1.0,
                        Duration = animationDuration,
                        FillBehavior = FillBehavior.HoldEnd
                    };

                    fadeIn.Completed += (s, e) => _isClockPanelAnimating = false;
                    ClockPanel.BeginAnimation(OpacityProperty, fadeIn);
                }, DispatcherPriority.Render);
            }
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
            //_viewModel.MainWindowOpacity = 0.2; /*Fix Render Blinking */
            if (_settings.HideOnStartup)
            {
                // 📌 최초 실행 시 창이 깜빡이는 문제 방지 (완전히 숨긴 상태로 시작)
                //System.Windows.Application.Current.MainWindow.Visibility = Visibility.Hidden;

                //Dispatcher.BeginInvoke((Action)(() =>
                //{
                //    _viewModel.Hide();
                //    System.Windows.Application.Current.MainWindow.Visibility = Visibility.Collapsed;
                //}), DispatcherPriority.Background);
                _viewModel.Hide();
            }
            else
            {
                // 📌 최초 실행 시 그림자 효과를 미리 적용하여 Show() 할 때 렌더링이 느려지지 않도록 함
                //ThemeManager.Instance.SetBlurForWindow();
                //ThemeManager.Instance.AutoDropShadow();
                _viewModel.Show();
                //_viewModel.MainWindowOpacity = 1;
            }
        }

        public Screen SelectedScreen()
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
