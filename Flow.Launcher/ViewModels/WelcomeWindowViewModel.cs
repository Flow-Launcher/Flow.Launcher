using System.Reactive;
using System.Windows.Input;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModels.WelcomePages;
using ReactiveUI;

namespace Flow.Launcher.ViewModel
{
    public class WelcomeWindowViewModel : ReactiveObject
    {
        private PageViewModelBase _currentPage;

        public WelcomeWindowViewModel(Settings settings)
        {
            Pages =
            [
                new WelcomePage1ViewModel(settings), new WelcomePage2ViewModel(settings),
                new WelcomePage3ViewModel(settings), new WelcomePage4ViewModel(settings),
                new WelcomePage5ViewModel(settings)
            ];

            CurrentPage = Pages[0];

            // Create Observables which will activate to deactivate our commands based on CurrentPage state
            var canNavNext = this.WhenAnyValue(x => x.CurrentPage.CanNavigateNext);
            var canNavPrev = this.WhenAnyValue(x => x.CurrentPage.CanNavigatePrevious);

            NavigateNextCommand = ReactiveCommand.Create(NavigateNext, canNavNext);
            NavigatePreviousCommand = ReactiveCommand.Create(NavigatePrevious, canNavPrev);
        }

        public ICommand NavigateNextCommand { get; set; }

        public PageViewModelBase CurrentPage
        {
            get { return _currentPage; }
            private set { this.RaiseAndSetIfChanged(ref _currentPage, value); }
        }

        public string PageDisplay => CurrentPage.PageTitle;

        private PageViewModelBase[] Pages { get; }

        private int CurrentPageIndex { get; set; }

        private void NavigateNext()
        {
            // get the current index and add 1
            CurrentPageIndex++;

            //  /!\ Be aware that we have no check if the index is valid. You may want to add it on your own. /!\
            CurrentPage = Pages[CurrentPageIndex];
        }

        /// <summary>
        /// Gets a command that navigates to the previous page
        /// </summary>
        public ICommand NavigatePreviousCommand { get; }

        private void NavigatePrevious()
        {
            // get the current index and subtract 1
            CurrentPageIndex--;

            //  /!\ Be aware that we have no check if the index is valid. You may want to add it on your own. /!\
            CurrentPage = Pages[CurrentPageIndex];
        }
    }
}
