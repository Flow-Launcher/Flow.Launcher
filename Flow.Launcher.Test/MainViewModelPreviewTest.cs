using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Flow.Launcher.Core.Plugin;
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
        public async Task GivenPreviewToggledOn_WhenNeverThenDefaultResultSelected_ThenInternalPreviewIsRestored_Async()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            await viewModel.TogglePreviewCommand.ExecuteAsync(null);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewToggledOnAndExternalPreviewVisible_WhenNeverThenDefaultResultSelected_ThenPreviewIsRestored_Async()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            await viewModel.TogglePreviewCommand.ExecuteAsync(null);

            // Simulate external preview becoming the active preview afterwards
            SetExternalPreviewVisible(viewModel, true);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewHidden_WhenAlwaysVisibilityResultSelected_ThenInternalPreviewAutoOpens_Async()
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
        public async Task GivenPreviewHidden_WhenMarkdownResultWithDefaultVisibilitySelected_ThenInternalPreviewStaysHidden_Async()
        {
            // Content type only controls rendering; it must not force the pane open.
            // Forcing is the job of PreviewVisibility.Always.
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem =
                ViewModel("Markdown", PreviewVisibility.Optional, settings, PreviewContentType.Markdown);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenAlwaysResultSelected_WhenDefaultResultSelected_ThenInternalPreviewAutoCloses_Async()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewToggledOn_WhenDefaultResultSelected_ThenInternalPreviewStaysVisible_Async()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            await viewModel.TogglePreviewCommand.ExecuteAsync(null);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenAlwaysResultThenNever_WhenNormalResultSelected_ThenInternalPreviewStaysHidden_Async()
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

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewToggledOffAndAlwaysPreviewOn_WhenDefaultResultSelected_ThenInternalPreviewStaysHidden_Async()
        {
            // User pressed F1 to close the preview while AlwaysPreview is on.
            // The toggle should still beat AlwaysPreview so selecting a normal result does not reopen the pane.
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);

            await viewModel.TogglePreviewCommand.ExecuteAsync(null);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenAlwaysPreviewOnAndPreviewHidden_WhenNeverThenDefaultResultSelected_ThenInternalPreviewReopens_Async()
        {
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewToggledOffAndAlwaysPreviewOn_WhenAlwaysResultSelected_ThenInternalPreviewReopens_Async()
        {
            // Always should force the pane open even when the user pressed F1 to close it.
            // A result's preview visibility beats the toggle preference.
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);

            await viewModel.TogglePreviewCommand.ExecuteAsync(null);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewToggleUnsetAndAlwaysPreviewOffAndAlwaysResult_WhenPreviewReset_ThenInternalPreviewOpens_Async()
        {
            // With AlwaysPreview off but the result forcing the pane open,
            // ResetPreviewAsync should show the internal preview.
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);
            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);

            // Setup correctness assertions
            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible); // no stale external preview
            ClassicAssert.IsFalse(PluginManager.UseExternalPreview()); // no plugin that provides external previews

            await viewModel.ResetPreviewAsync();

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenNoToggleAndAlwaysPreviewOffAndDefaultResult_WhenPreviewReset_ThenInternalPreviewStaysHidden_Async()
        {
            // No override and nothing forcing the pane open,
            // so ResetPreviewAsync should close the internal preview.
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);
            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);

            // Setup correctness assertions
            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible); // no stale external preview
            ClassicAssert.IsFalse(PluginManager.UseExternalPreview()); // no plugin that provides external previews
            ClassicAssert.IsFalse(settings.AlwaysPreview); // global AlwaysPreview setting is off

            await viewModel.ResetPreviewAsync();

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task
            GivenExternalPreviewVisibleAndAlwaysResult_WhenPreviewReset_ThenExternalClosedAndInternalOpens_Async()
        {
            // Stale external preview is visible but the result forces the pane open.
            // ResetPreviewAsync should close external then show internal.
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);
            SetExternalPreviewVisible(viewModel, true);
            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);

            // Setup correctness assertions
            ClassicAssert.IsTrue(viewModel.ExternalPreviewVisible); // external is marked visible
            ClassicAssert.IsFalse(PluginManager.UseExternalPreview()); // no plugin that provides external previews

            await viewModel.ResetPreviewAsync();

            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible);
            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task
            GivenExternalPreviewVisibleAndNormalResult_WhenPreviewReset_ThenExternalAndInternalAreBothHidden_Async()
        {
            // Stale external preview is visible but nothing is forcing the pane open,
            // so ResetPreviewAsync should close external and hide internal.
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);
            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
            SetExternalPreviewVisible(viewModel, true);

            // Setup correctness assertions
            ClassicAssert.IsTrue(viewModel.ExternalPreviewVisible); // external is marked visible
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible); // internal is marked hidden
            ClassicAssert.IsFalse(PluginManager.UseExternalPreview()); // no plugin that provides external previews
            ClassicAssert.IsFalse(settings.AlwaysPreview); // global AlwaysPreview setting is off

            await viewModel.ResetPreviewAsync();

            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenPreviewToggledOffAndAlwaysPreviewOn_WhenNeverThenDefaultResult_ThenPreviewStaysHidden_Async()
        {
            // User pressed F1 to close the preview, navigates through a Never result, then to a normal result.
            // The AlwaysPreview restore path must not re-open the pane against the user's intent.
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);

            await viewModel.TogglePreviewCommand.ExecuteAsync(null);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Optional, settings);
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

    }
}
