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
            int currentIndex = Array.IndexOf(WelcomeViewModel.PageSequence, _viewModel.CurrentPage);
            if (currentIndex < WelcomeViewModel.PageSequence.Length - 1)
            {
                WelcomePage nextPage = WelcomeViewModel.PageSequence[currentIndex + 1];
                
                Type nextPageType = PageTypeSelector(nextPage);
                ContentFrame.Navigate(nextPageType, null, _forwardTransitionInfo);
                
                _viewModel.CurrentPage = nextPage;
            }
        }
        private void BackwardButton_Click(object sender, RoutedEventArgs e)
        {
            int currentIndex = Array.IndexOf(WelcomeViewModel.PageSequence, _viewModel.CurrentPage);
            if (currentIndex > 0)
            {
                WelcomePage prevPage = WelcomeViewModel.PageSequence[currentIndex - 1];
                Type prevPageType = PageTypeSelector(prevPage);
                ContentFrame.Navigate(prevPageType, null, _backTransitionInfo);
                _viewModel.CurrentPage = prevPage;
            }
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static Type PageTypeSelector(WelcomePage page)
        {
            Type result = page switch
            {
                WelcomePage.Intro => typeof(WelcomePage1),
                WelcomePage.Features => typeof(WelcomePage2),
                WelcomePage.UserType => typeof(WelcomePageUserType),
                WelcomePage.Hotkeys => typeof(WelcomePage3),
                WelcomePage.Commands => typeof(WelcomePage4),
                WelcomePage.Finish => typeof(WelcomePage5),
                _ => throw new ArgumentOutOfRangeException(nameof(page), page, "Unexpected page type")
            };
            return result;
        }

        // This method is used to convert the page number to the corresponding page type.
        private static Type PageTypeSelector(int pageNumber)
        {
            return PageTypeSelector((WelcomePage)pageNumber);
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
            ContentFrame.Navigate(PageTypeSelector(_viewModel.CurrentPage));
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // Save settings when window is closed
            Ioc.Default.GetRequiredService<Settings>().Save();
        }
    }
}
