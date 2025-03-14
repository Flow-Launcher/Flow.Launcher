using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using Flow.Launcher.Resources.Pages;
using ModernWpf.Media.Animation;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.ViewModel;
using Flow.Launcher.Infrastructure.UserSettings;

namespace Flow.Launcher
{
    public partial class WelcomeWindow : Window
    {
        private readonly WelcomeViewModel _viewModel;

        private readonly NavigationTransitionInfo _forwardTransitionInfo = new SlideNavigationTransitionInfo()
        {
            Effect = SlideNavigationTransitionEffect.FromRight
        };
        private readonly NavigationTransitionInfo _backTransitionInfo = new SlideNavigationTransitionInfo()
        {
            Effect = SlideNavigationTransitionEffect.FromLeft
        };

        public WelcomeWindow()
        {
            _viewModel = Ioc.Default.GetRequiredService<WelcomeViewModel>();
            DataContext = _viewModel;
            InitializeComponent();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.PageNum < WelcomeViewModel.MaxPageNum)
            {
                _viewModel.PageNum++;
                ContentFrame.Navigate(PageTypeSelector(_viewModel.PageNum), null, _forwardTransitionInfo);
            }
            else
            {
                _viewModel.NextEnabled = false;
            }
        }

        private void BackwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.PageNum > 1)
            {
                _viewModel.PageNum--;
                ContentFrame.Navigate(PageTypeSelector(_viewModel.PageNum), null, _backTransitionInfo);
            }
            else
            {
                _viewModel.BackEnabled = false;
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static Type PageTypeSelector(int pageNumber)
        {
            return pageNumber switch
            {
                1 => typeof(WelcomePage1),
                2 => typeof(WelcomePage2),
                3 => typeof(WelcomePage3),
                4 => typeof(WelcomePage4),
                5 => typeof(WelcomePage5),
                _ => throw new ArgumentOutOfRangeException(nameof(pageNumber), pageNumber, "Unexpected Page Number")
            };
        }

        private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
        {
            if (Keyboard.FocusedElement is not TextBox textBox)
            {
                return;
            }
            var tRequest = new TraversalRequest(FocusNavigationDirection.Next);
            textBox.MoveFocus(tRequest);
        }

        private void OnActivated(object sender, EventArgs e)
        {
            Keyboard.ClearFocus();
        }

        private void ContentFrame_Loaded(object sender, RoutedEventArgs e)
        {
            ContentFrame.Navigate(PageTypeSelector(1)); /* Set First Page */
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Save settings when window is closed
            Ioc.Default.GetRequiredService<Settings>().Save();
        }
    }
}
