using Flow.Launcher.Plugin;

namespace Flow.Launcher.ViewModel.WelcomePages
{
    public abstract class PageViewModelBase : BaseModel
    {
        /// <summary>
        /// Gets if the user can navigate to the next page
        /// </summary>
        public abstract bool CanNavigateNext { get; protected set; }

        /// <summary>
        /// Gets if the user can navigate to the previous page
        /// </summary>
        public abstract bool CanNavigatePrevious { get; protected set; }

        public abstract string PageTitle { get; }
    }
}
