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
        private bool _animating;

        #endregion

        public MainWindow(Settings settings, MainViewModel mainVM)
        {
            DataContext = mainVM;
            _viewModel = mainVM;
            _settings = settings;
            InitializeComponent();
            InitializePosition();
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OnClosing(object sender, CancelEventArgs e)
        {
            _settings.WindowTop = Top;
            _settings.WindowLeft = Left;
            _notifyIcon.Visible = false;
            _viewModel.Save();
            e.Cancel = true;
            await PluginManager.DisposePluginsAsync();
            Environment.Exit(0);
        }

        private void OnInitialized(object sender, EventArgs e)
        {
        }

        private void OnLoaded(object sender, RoutedEventArgs _)
        {
            HideStartup();
            // show notify icon when flowlauncher is hidden
            InitializeNotifyIcon();
            WindowsInteropHelper.DisableControlBox(this);
            InitProgressbarAnimation();
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
                                UpdatePosition();
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
                            Dispatcher.Invoke(async () =>
                            {
                                if (_viewModel.ProgressBarVisibility == Visibility.Hidden && !isProgressBarStoryboardPaused)
                                {
                                    await Task.Delay(50);
                                    _progressBarStoryboard.Stop(ProgressBar);
                                    isProgressBarStoryboardPaused = true;
                                }
                                else if (_viewModel.MainWindowVisibility == Visibility.Visible &&
                                         isProgressBarStoryboardPaused)
                                {
                                    _progressBarStoryboard.Begin(ProgressBar, true);
                                    isProgressBarStoryboardPaused = false;
                                }
                            }, System.Windows.Threading.DispatcherPriority.Render);

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
                }
            };
        }

        private void InitializePosition()
        {
            if (_settings.RememberLastLaunchLocation)
            {
                Top = _settings.WindowTop;
                Left = _settings.WindowLeft;
            }
            else
            {
                Left = WindowLeft();
                Top = WindowTop();
            }
        }

        private void UpdateNotifyIconText()
        {
            var menu = contextMenu;
            ((MenuItem)menu.Items[1]).Header = InternationalizationManager.Instance.GetTranslation("iconTrayOpen");
            ((MenuItem)menu.Items[2]).Header = InternationalizationManager.Instance.GetTranslation("iconTraySettings");
            ((MenuItem)menu.Items[3]).Header = InternationalizationManager.Instance.GetTranslation("iconTrayExit");
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

            var header = new MenuItem
            {
                Header = "Flow Launcher",
                IsEnabled = false
            };
            var open = new MenuItem
            {
                Header = InternationalizationManager.Instance.GetTranslation("iconTrayOpen")
            };
            var settings = new MenuItem
            {
                Header = InternationalizationManager.Instance.GetTranslation("iconTraySettings")
            };
            var exit = new MenuItem
            {
                Header = InternationalizationManager.Instance.GetTranslation("iconTrayExit")
            };

            open.Click += (o, e) => _viewModel.ToggleFlowLauncher();
            settings.Click += (o, e) => App.API.OpenSettingDialog();
            exit.Click += (o, e) => Close();
            contextMenu.Items.Add(header);
            contextMenu.Items.Add(open);
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
            Storyboard sb = new Storyboard();
            var da = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.18),
                FillBehavior = FillBehavior.Stop
            };

            var da2 = new DoubleAnimation
            {
                From = Top + 8,
                To = Top,
                Duration = TimeSpan.FromSeconds(0.18),
                FillBehavior = FillBehavior.Stop
            };
            Storyboard.SetTarget(da, this);
            Storyboard.SetTargetProperty(da, new PropertyPath(Window.OpacityProperty));
            Storyboard.SetTargetProperty(da2, new PropertyPath(Window.TopProperty));
            sb.Children.Add(da);
            sb.Children.Add(da2);
            sb.Completed += (_, _) => _animating = false;
            _settings.WindowLeft = Left;
            _settings.WindowTop = Top;
            sb.Begin(FlowMainWindow);
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void OnPreviewMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender != null && e.OriginalSource != null)
            {
                var r = (ResultListBox)sender;
                var d = (DependencyObject)e.OriginalSource;
                var item = ItemsControl.ContainerFromElement(r, d) as ListBoxItem;
                var result = (ResultViewModel)item?.DataContext;
                if (result != null)
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        _viewModel.OpenResultCommand.Execute(null);
                    }
                    else if (e.ChangedButton == MouseButton.Right)
                    {
                        _viewModel.LoadContextMenuCommand.Execute(null);
                    }
                }
            }
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
            if (_viewModel.MainWindowVisibility != Visibility.Collapsed)
            {
                // need time to initialize the main query window animation
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

            if (_settings.RememberLastLaunchLocation)
            {
                Left = _settings.WindowLeft;
                Top = _settings.WindowTop;
            }
            else
            {
                Left = WindowLeft();
                Top = WindowTop();
            }
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            if (_animating)
                return;
            if (_settings.RememberLastLaunchLocation)
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

        public double WindowLeft()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.X, 0);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, screen.WorkingArea.Width, 0);
            var left = (dip2.X - ActualWidth) / 2 + dip1.X;
            return left;
        }

        public double WindowTop()
        {
            var screen = Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            var dip1 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Y);
            var dip2 = WindowsInteropHelper.TransformPixelsToDIP(this, 0, screen.WorkingArea.Height);
            var top = (dip2.Y - QueryTextBox.ActualHeight) / 4 + dip1.Y;
            return top;
        }

        /// <summary>
        /// Register up and down key
        /// todo: any way to put this in xaml ?
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
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
                default:
                    break;

            }
        }

        private void MoveQueryTextToEnd()
        {
            QueryTextBox.CaretIndex = QueryTextBox.Text.Length;
        }
    }
}