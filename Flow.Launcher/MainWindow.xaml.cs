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
using ContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using DragEventArgs = System.Windows.DragEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using NotifyIcon = System.Windows.Forms.NotifyIcon;
using Flow.Launcher.Infrastructure;
using System.Windows.Media;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Plugin.SharedCommands;
using System.Text;
using DataObject = System.Windows.DataObject;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using System.IO;
using System.Windows.Threading;
using System.Windows.Data;
using ModernWpf.Controls;
using System.Drawing;
using System.Windows.Forms.Design.Behavior;
using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using Microsoft.VisualBasic.Devices;
using Microsoft.FSharp.Data.UnitSystems.SI.UnitNames;

namespace Flow.Launcher
{
    public partial class MainWindow
    {
        #region Private Fields

        private readonly Storyboard _progressBarStoryboard = new Storyboard();
        private bool isProgressBarStoryboardPaused;
        private Settings _settings;
        private NotifyIcon _notifyIcon;
        private ContextMenu contextMenu;
        private MainViewModel _viewModel;
        private readonly MediaPlayer animationSound = new();
        private bool _animating;

        #endregion

        public MainWindow(Settings settings, MainViewModel mainVM)
        {
            DataContext = mainVM;
            _viewModel = mainVM;
            _settings = settings;
            
            InitializeComponent();
            InitializePosition();            
            animationSound.Open(new Uri(AppDomain.CurrentDomain.BaseDirectory + "Resources\\open.wav"));
        }

        public MainWindow()
        {
            InitializeComponent();
        }
        
        private void OnCopy(object sender, ExecutedRoutedEventArgs e)
        {
            if (QueryTextBox.SelectionLength == 0)
            {
                _viewModel.ResultCopy(string.Empty);

            }
            else if (!string.IsNullOrEmpty(QueryTextBox.Text))
            {
                _viewModel.ResultCopy(QueryTextBox.SelectedText);
            }
        }
        private async void OnClosing(object sender, CancelEventArgs e)
        {
            _settings.WindowTop = Top;
            _settings.WindowLeft = Left;
            _notifyIcon.Visible = false;
            _viewModel.Save();
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
            CheckFirstLaunch();
            HideStartup();
            // show notify icon when flowlauncher is hidden
            InitializeNotifyIcon();
            InitializeColorScheme();
            WindowsInteropHelper.DisableControlBox(this);
            InitProgressbarAnimation();
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
                            if (_viewModel.MainWindowVisibilityStatus)
                            {
                                if (_settings.UseSound)
                                {
                                    animationSound.Position = TimeSpan.Zero;
                                    animationSound.Play();
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

                                if (_viewModel.ProgressBarVisibility == Visibility.Visible && isProgressBarStoryboardPaused)
                                {
                                    _progressBarStoryboard.Begin(ProgressBar, true);
                                    isProgressBarStoryboardPaused = false;
                                }

                                if(_settings.UseAnimation)
                                    WindowAnimator();
                            }
                            else if (!isProgressBarStoryboardPaused)
                            {
                                _progressBarStoryboard.Stop(ProgressBar);
                                isProgressBarStoryboardPaused = true;
                            }

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
            switch (_settings.SearchWindowPosition)
            {
                case SearchWindowPositions.RememberLastLaunchLocation:
                    Top = _settings.WindowTop;
                    Left = _settings.WindowLeft;
                    break;
                case SearchWindowPositions.MouseScreenCenter:
                    Left = HorizonCenter();
                    Top = VerticalCenter();
                    break;
                case SearchWindowPositions.MouseScreenCenterTop:
                    Left = HorizonCenter();
                    Top = 10;
                    break;
                case SearchWindowPositions.MouseScreenLeftTop:
                    Left = 10;
                    Top = 10;
                    break;
                case SearchWindowPositions.MouseScreenRightTop:
                    Left = HorizonRight();
                    Top = 10;
                    break;
            }
        }

        private void UpdateNotifyIconText()
        {
            var menu = contextMenu;
            ((MenuItem)menu.Items[0]).Header = InternationalizationManager.Instance.GetTranslation("iconTrayOpen") + " (" + _settings.Hotkey + ")";
            ((MenuItem)menu.Items[1]).Header = InternationalizationManager.Instance.GetTranslation("GameMode");
            ((MenuItem)menu.Items[2]).Header = InternationalizationManager.Instance.GetTranslation("PositionReset");
            ((MenuItem)menu.Items[3]).Header = InternationalizationManager.Instance.GetTranslation("iconTraySettings");
            ((MenuItem)menu.Items[4]).Header = InternationalizationManager.Instance.GetTranslation("iconTrayExit");

        }

        private void InitializeNotifyIcon()
        {
            _notifyIcon = new NotifyIcon
            {
                Text = Infrastructure.Constant.FlowLauncher,
                Icon = Properties.Resources.app,
                Visible = !_settings.HideNotifyIcon
            };

            contextMenu = new ContextMenu();

            var openIcon = new FontIcon { Glyph = "\ue71e" };
            var open = new MenuItem
            {
                Header = InternationalizationManager.Instance.GetTranslation("iconTrayOpen") + " (" + _settings.Hotkey + ")",
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
            gamemode.Click += (o, e) => ToggleGameMode();
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

            _notifyIcon.ContextMenuStrip = new ContextMenuStrip(); // it need for close the context menu. if not, context menu can't close. 
            _notifyIcon.MouseClick += (o, e) =>
            {
                switch (e.Button)
                {
                    case MouseButtons.Left:
                        _viewModel.ToggleFlowLauncher();
                        break;

                    case MouseButtons.Right:
                        contextMenu.IsOpen = true;
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
        private void ToggleGameMode()
        {
            if (_viewModel.GameModeStatus)
            {
                _notifyIcon.Icon = Properties.Resources.app;
                _viewModel.GameModeStatus = false;
            }
            else
            {
                _notifyIcon.Icon = Properties.Resources.gamemode;
                _viewModel.GameModeStatus = true;
            }
        }
        private async void PositionReset()
        {
           _viewModel.Show();
           await Task.Delay(300); // If don't give a time, Positioning will be weird.
           Left = HorizonCenter();
           Top = VerticalCenter();
        }
        private void InitProgressbarAnimation()
        {
            var da = new DoubleAnimation(ProgressBar.X2, ActualWidth + 150,
            new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
            var da1 = new DoubleAnimation(ProgressBar.X1, ActualWidth + 50, new Duration(new TimeSpan(0, 0, 0, 0, 1600)));
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

            _animating = true;
            UpdatePosition();

            Storyboard windowsb = new Storyboard();
            Storyboard clocksb = new Storyboard();
            Storyboard iconsb = new Storyboard();
            CircleEase easing = new CircleEase();
            easing.EasingMode = EasingMode.EaseInOut;

            var WindowOpacity = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.25),
                FillBehavior = FillBehavior.Stop
            };

            var WindowMotion = new DoubleAnimation
            {
                From = Top + 10,
                To = Top,
                Duration = TimeSpan.FromSeconds(0.25),
                FillBehavior = FillBehavior.Stop
            };
            var IconMotion = new DoubleAnimation
            {
                    From = 12,
                    To = 0,
                    EasingFunction = easing,
                    Duration = TimeSpan.FromSeconds(0.36),
                    FillBehavior = FillBehavior.Stop
            };

            var ClockOpacity = new DoubleAnimation
            {
                From = 0,
                To = 1,
                EasingFunction = easing,
                Duration = TimeSpan.FromSeconds(0.36),
                FillBehavior = FillBehavior.Stop
            };
            double TargetIconOpacity = SearchIcon.Opacity; // Animation Target Opacity from Style
            var IconOpacity = new DoubleAnimation
            {
                From = 0,
                To = TargetIconOpacity,
                EasingFunction = easing,
                Duration = TimeSpan.FromSeconds(0.36),
                FillBehavior = FillBehavior.Stop
            };

            double right = ClockPanel.Margin.Right;
            var thicknessAnimation = new ThicknessAnimation
            {
                From = new Thickness(0, 12, right, 0),
                To = new Thickness(0, 0, right, 0),
                EasingFunction = easing,
                Duration = TimeSpan.FromSeconds(0.36),
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

            if (QueryTextBox.Text.Length == 0)
            {
                clocksb.Begin(ClockPanel);
            }
            iconsb.Begin(SearchIcon);
            windowsb.Begin(FlowMainWindow);
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
            
            if(_settings.UseAnimation)
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
                
                if (_settings.HideWhenDeactive)
                {
                    _viewModel.Hide();
                }
            }
        }

        private void UpdatePosition()
        {
            if (_animating)
                return;
            InitializePosition();
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (_animating)
                return;
            if (_settings.SearchWindowPosition == SearchWindowPositions.RememberLastLaunchLocation)
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
        
        public double HorizonCenter()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        public double VerticalCenter()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - QueryTextBox.ActualHeight) / 4 + dip1.Y;
            return top;
        }

        public double HorizonRight()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) - 10;
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
                    _viewModel.SelectNextItemCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Up:
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
                case Key.F12:
                    if (specialKeyState.CtrlPressed)
                    {
                        ToggleGameMode();
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
                case Key.F1:
                    PreviewToggle();
                    e.Handled = true;
                    break;

                default:
                    break;

            }
        }

        public void PreviewReset()
        {
            if (_settings.AlwaysPreview == true)
            {
                ResultArea.SetValue(Grid.ColumnSpanProperty, 1);
                Preview.Visibility = Visibility.Visible;
            }
            else
            {
                ResultArea.SetValue(Grid.ColumnSpanProperty, 2);
                Preview.Visibility = Visibility.Collapsed;
            }
        }
        public void PreviewToggle()
        {

            if (Preview.Visibility == Visibility.Collapsed)
            {
                ResultArea.SetValue(Grid.ColumnSpanProperty, 1);
                Preview.Visibility = Visibility.Visible;
            }
            else
            {
                ResultArea.SetValue(Grid.ColumnSpanProperty, 2);
                Preview.Visibility = Visibility.Collapsed;
            }
        }

        private void MoveQueryTextToEnd()
        {
            // QueryTextBox seems to be update with a DispatcherPriority as low as ContextIdle.
            // To ensure QueryTextBox is up to date with QueryText from the View, we need to Dispatch with such a priority
            Dispatcher.Invoke(() => QueryTextBox.CaretIndex = QueryTextBox.Text.Length, System.Windows.Threading.DispatcherPriority.ContextIdle);
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
            if(_viewModel.QueryText != QueryTextBox.Text)
            {
                BindingExpression be = QueryTextBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                be.UpdateSource();
            }
        }
    }
}
