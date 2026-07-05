using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    internal class MainViewModelPreviewTest
    {
        [Test]
        public async Task GivenManualPreviewWasVisible_WhenNeverResultThenDefaultResultSelected_ThenInternalPreviewIsRestoredAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenExternalPreviewWasVisible_WhenNeverResultThenDefaultResultSelected_ThenPreviewIsRestoredAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);
            SetExternalPreviewVisible(viewModel, true);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewHidden_WhenAlwaysVisibilityResultSelected_ThenInternalPreviewAutoOpensAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewHidden_WhenMarkdownResultWithDefaultVisibilitySelected_ThenInternalPreviewStaysHiddenAsync()
        {
            // Content type only controls rendering; it must not force the pane open.
            // Forcing is the job of PreviewVisibility.Always.
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem =
                ViewModel("Markdown", PreviewVisibility.Default, settings, PreviewContentType.Markdown);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewAutoOpenedForForcedResult_WhenDefaultResultSelected_ThenInternalPreviewAutoClosesAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewManuallyVisible_WhenDefaultResultSelected_ThenInternalPreviewStaysVisibleAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewAutoOpenedForForcedResult_WhenNeverThenDefaultResultSelected_ThenInternalPreviewStaysHiddenAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenAlwaysPreviewOnAndPreviewManuallyHidden_WhenDefaultResultSelected_ThenInternalPreviewStaysHiddenAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenAlwaysPreviewOnAndPreviewHidden_WhenNeverThenDefaultResultSelected_ThenInternalPreviewReopensAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenAlwaysPreviewOnAndPreviewManuallyHidden_WhenAlwaysVisibilityResultSelected_ThenInternalPreviewReopensAsync()
        {
            // Always must force the pane open for any content type, even when the user
            // manually closed the pane while the global Always Preview setting is on.
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public void GivenAlwaysPreviewOffAndForcedResult_WhenPreviewReset_ThenInternalPreviewOpens()
        {
            // Reproduces the reopen bug: ResetPreview runs each time the main window is shown.
            // With Always Preview off it must still honour a forced result instead of hiding the pane,
            // otherwise the preview only reappears once the user hovers a result.
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);
            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);

            viewModel.ResetPreview();

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public void GivenAlwaysPreviewOffAndDefaultResult_WhenPreviewReset_ThenInternalPreviewStaysHidden()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);
            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);

            viewModel.ResetPreview();

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenAlwaysPreviewOnAndManuallyClosedThenNeverResult_WhenDefaultResultSelected_ThenPreviewStaysHiddenAsync()
        {
            // Regression: user closes preview via F1, navigates through a Never result, then to a
            // normal result — the AlwaysPreview restore path must not re-open the pane against
            // the user's explicit intent.
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);
            SetPreviewManuallyClosed(viewModel, true);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        private static MainViewModel CreatePreviewViewModel(Settings settings, int resultAreaColumn)
        {
            var viewModel = (MainViewModel)RuntimeHelpers.GetUninitializedObject(typeof(MainViewModel));
            typeof(MainViewModel)
                .GetField("<Settings>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(viewModel, settings);
            viewModel.ResultAreaColumn = resultAreaColumn;
            return viewModel;
        }

        private static ResultViewModel ViewModel(
            string title,
            PreviewVisibility visibility,
            Settings settings,
            PreviewContentType contentType = PreviewContentType.ImageWithText)
            => new(
                new Result
                {
                    Title = title,
                    PreviewVisibility = visibility,
                    Preview = new Result.PreviewInfo
                    {
                        ContentType = contentType
                    }
                },
                settings);

        private static int ResultAreaColumnPreviewShown
            => (int)typeof(MainViewModel)
                .GetField("ResultAreaColumnPreviewShown", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);

        private static int ResultAreaColumnPreviewHidden
            => (int)typeof(MainViewModel)
                .GetField("ResultAreaColumnPreviewHidden", BindingFlags.NonPublic | BindingFlags.Static)
                .GetValue(null);

        private static async Task InvokeUpdatePreviewAsync(MainViewModel viewModel)
        {
            var task = (Task)typeof(MainViewModel)
                .GetMethod("UpdatePreviewAsync", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(viewModel, null);
            await task;
        }

        private static void SetExternalPreviewVisible(MainViewModel viewModel, bool visible)
            => typeof(MainViewModel)
                .GetField("<ExternalPreviewVisible>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(viewModel, visible);

        private static void SetPreviewManuallyClosed(MainViewModel viewModel, bool value)
            => typeof(MainViewModel)
                .GetField("_previewManuallyClosed", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(viewModel, value);
    }
}
