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
        public async Task GivenManualOverrideTrue_WhenNeverThenDefaultResultSelected_ThenInternalPreviewIsRestoredAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);
            SetManualPreviewOverride(viewModel, true);

            viewModel.PreviewSelectedItem = ViewModel("Never", PreviewVisibility.Never, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenManualOverrideTrueAndExternalPreviewVisible_WhenNeverThenDefaultResultSelected_ThenPreviewIsRestoredAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);
            SetExternalPreviewVisible(viewModel, true);
            SetManualPreviewOverride(viewModel, true);

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
        public async Task GivenAlwaysResultSelected_WhenDefaultResultSelected_ThenInternalPreviewAutoClosesAsync()
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
        public async Task GivenManualOverrideTrue_WhenDefaultResultSelected_ThenInternalPreviewStaysVisibleAsync()
        {
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);
            SetManualPreviewOverride(viewModel, true);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenAlwaysResultThenNever_WhenNormalResultSelected_ThenInternalPreviewStaysHiddenAsync()
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
        public async Task GivenManualOverrideFalseAndAlwaysPreviewOn_WhenDefaultResultSelected_ThenInternalPreviewStaysHiddenAsync()
        {
            // User closed the preview with F1 (_manualPreviewOverride = false) while
            // AlwaysPreview is on. The manual override should still beat AlwaysPreview
            // so selecting a normal result does not reopen the pane.
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);
            SetManualPreviewOverride(viewModel, false);

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
        public async Task GivenManualOverrideFalseAndAlwaysPreviewOn_WhenAlwaysResultSelected_ThenInternalPreviewReopensAsync()
        {
            // Always must force the pane open even when the user explicitly closed
            // the preview with F1. Result-level visibility beats manual override.
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);
            SetManualPreviewOverride(viewModel, false);

            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);
            await InvokeUpdatePreviewAsync(viewModel);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);

            viewModel.PreviewSelectedItem = ViewModel("Forced", PreviewVisibility.Always, settings);
            await InvokeUpdatePreviewAsync(viewModel);

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenManualOverrideUnsetAndAlwaysPreviewOffAndAlwaysResult_WhenPreviewReset_ThenInternalPreviewOpens()
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
            ClassicAssert.IsNull(ReadManualPreviewOverride(viewModel)); // user has not manually toggled preview
            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible); // no stale external preview
            ClassicAssert.IsFalse(PluginManager.UseExternalPreview()); // no plugin that provides external previews

            await viewModel.ResetPreviewAsync();

            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenManualOverrideUnsetAndAlwaysPreviewOffAndDefaultResult_WhenPreviewReset_ThenInternalPreviewStaysHidden()
        {
            // No override and nothing forcing the pane open,
            // so ResetPreviewAsync should close the internal preview.
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);
            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);

            // Setup correctness assertions
            ClassicAssert.IsNull(ReadManualPreviewOverride(viewModel)); // user has not manually toggled preview
            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible); // no stale external preview
            ClassicAssert.IsFalse(PluginManager.UseExternalPreview()); // no plugin that provides external previews
            ClassicAssert.IsFalse(settings.AlwaysPreview); // global AlwaysPreview setting is off

            await viewModel.ResetPreviewAsync();

            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task
            GivenExternalPreviewVisibleAndAlwaysResult_WhenPreviewReset_ThenExternalClosedAndInternalOpens()
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
            ClassicAssert.IsNull(ReadManualPreviewOverride(viewModel)); // user has not manually toggled preview
            ClassicAssert.IsTrue(viewModel.ExternalPreviewVisible); // external is marked visible
            ClassicAssert.IsFalse(PluginManager.UseExternalPreview()); // no plugin that provides external previews

            await viewModel.ResetPreviewAsync();

            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible);
            ClassicAssert.IsTrue(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task
            GivenExternalPreviewVisibleAndNormalResult_WhenPreviewReset_ThenExternalAndInternalAreBothHidden()
        {
            // Stale external preview is visible but nothing is forcing the pane open,
            // so ResetPreviewAsync should close external and hide internal.
            var settings = new Settings
            {
                AlwaysPreview = false
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewShown);
            SetExternalPreviewVisible(viewModel, true);
            viewModel.PreviewSelectedItem = ViewModel("Normal", PreviewVisibility.Default, settings);

            // Setup correctness assertions
            ClassicAssert.IsNull(ReadManualPreviewOverride(viewModel)); // user has not manually toggled preview
            ClassicAssert.IsTrue(viewModel.ExternalPreviewVisible); // external is marked visible
            ClassicAssert.IsFalse(PluginManager.UseExternalPreview()); // no plugin that provides external previews
            ClassicAssert.IsFalse(settings.AlwaysPreview); // global AlwaysPreview setting is off

            await viewModel.ResetPreviewAsync();

            ClassicAssert.IsFalse(viewModel.ExternalPreviewVisible);
            ClassicAssert.IsFalse(viewModel.InternalPreviewVisible);
        }

        [Test]
        public async Task GivenManualOverrideFalseAndAlwaysPreviewOn_WhenNeverThenDefaultResult_ThenPreviewStaysHiddenAsync()
        {
            // Regression: user closes preview via F1 (_manualOverride = false),
            // navigates through a Never result, then to a normal result.
            // The AlwaysPreview restore path must not re-open the pane against
            // the user's explicit intent.
            var settings = new Settings
            {
                AlwaysPreview = true
            };
            var viewModel = CreatePreviewViewModel(settings, ResultAreaColumnPreviewHidden);
            SetManualPreviewOverride(viewModel, false);

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

        private static void SetManualPreviewOverride(MainViewModel viewModel, bool? value)
            => typeof(MainViewModel)
                .GetField("_manualPreviewOverride", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(viewModel, value);

        private static bool? ReadManualPreviewOverride(MainViewModel viewModel)
            => (bool?)typeof(MainViewModel)
                .GetField("_manualPreviewOverride", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(viewModel);
    }
}
