using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.SettingPages.Views;
using Flow.Launcher.ViewModel;
using NUnit.Framework;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class SettingWindowViewModelTest
    {
        [Test]
        public void PendingNavigation_ConsumeReturnsValueOnceThenNull()
        {
            var viewModel = new SettingWindowViewModel(new Settings());

            viewModel.SetPendingNavigation(typeof(SettingsPanePlugins), "Calculator");

            Assert.That(viewModel.ConsumePendingPageType(), Is.EqualTo(typeof(SettingsPanePlugins)));
            Assert.That(viewModel.ConsumePendingFilterText(), Is.EqualTo("Calculator"));
            Assert.That(viewModel.ConsumePendingPageType(), Is.Null);
            Assert.That(viewModel.ConsumePendingFilterText(), Is.Null);
        }

        [Test]
        public void PendingNavigation_NothingPending_ConsumeReturnsNull()
        {
            var viewModel = new SettingWindowViewModel(new Settings());

            Assert.That(viewModel.ConsumePendingPageType(), Is.Null);
            Assert.That(viewModel.ConsumePendingFilterText(), Is.Null);
        }

        [Test]
        public void PendingNavigation_FilterDefaultsToNull()
        {
            var viewModel = new SettingWindowViewModel(new Settings());

            viewModel.SetPendingNavigation(typeof(SettingsPaneTheme));

            Assert.That(viewModel.ConsumePendingPageType(), Is.EqualTo(typeof(SettingsPaneTheme)));
            Assert.That(viewModel.ConsumePendingFilterText(), Is.Null);
        }
    }
}
