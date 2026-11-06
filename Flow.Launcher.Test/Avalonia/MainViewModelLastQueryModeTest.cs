using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Avalonia
{
    [TestFixture]
    [NonParallelizable]
    public class MainViewModelLastQueryModeTest
    {
        [Test]
        public void Show_WhenLastQueryModeIsEmpty_ClearsExistingQuery()
        {
            var viewModel = CreateViewModel(LastQueryMode.Empty);
            viewModel.QueryText = "existing query";

            var request = ShowAndCaptureFocusRequest(viewModel);

            ClassicAssert.AreEqual(string.Empty, viewModel.QueryText);
            AssertShowFocusRequest(request, QueryTextFocusMode.SelectAll);
        }

        [Test]
        public void Show_WhenLastQueryModeIsPreserved_KeepsExistingQueryAndMovesCaretToEnd()
        {
            var viewModel = CreateViewModel(LastQueryMode.Preserved);
            viewModel.QueryText = "existing query";

            var request = ShowAndCaptureFocusRequest(viewModel);

            ClassicAssert.AreEqual("existing query", viewModel.QueryText);
            ClassicAssert.IsTrue(viewModel.LastQuerySelected);
            AssertShowFocusRequest(request, QueryTextFocusMode.CaretAtEnd);
        }

        [Test]
        public void Show_WhenLastQueryModeIsSelected_KeepsExistingQueryAndSelectsItOnce()
        {
            var viewModel = CreateViewModel(LastQueryMode.Selected);
            viewModel.QueryText = "existing query";

            var firstRequest = ShowAndCaptureFocusRequest(viewModel);
            var secondRequest = ShowAndCaptureFocusRequest(viewModel);

            ClassicAssert.AreEqual("existing query", viewModel.QueryText);
            ClassicAssert.IsTrue(viewModel.LastQuerySelected);
            AssertShowFocusRequest(firstRequest, QueryTextFocusMode.SelectAll);
            AssertShowFocusRequest(secondRequest, QueryTextFocusMode.Focus);
        }

        [Test]
        public void Show_WhenFocusIsRequested_RequestsFocusBeforeWindowBecomesVisible()
        {
            var viewModel = CreateViewModel(LastQueryMode.Empty);
            QueryTextFocusRequest? request = null;
            bool? visibilityWhenFocusRequested = null;

            viewModel.QueryTextFocusRequested += focusRequest =>
            {
                request = focusRequest;
                visibilityWhenFocusRequested = viewModel.MainWindowVisibility;
            };

            viewModel.Show();

            ClassicAssert.IsTrue(request.HasValue);
            AssertShowFocusRequest(request.Value, QueryTextFocusMode.SelectAll);
            ClassicAssert.IsTrue(visibilityWhenFocusRequested.HasValue);
            ClassicAssert.IsFalse(visibilityWhenFocusRequested.Value);
            ClassicAssert.IsTrue(viewModel.MainWindowVisibility);
        }

        [Test]
        public void ShowWithInjectedQuery_WhenFocusIsRequested_StagesQueryBeforeWindowBecomesVisible()
        {
            var viewModel = CreateViewModel(LastQueryMode.Selected);
            QueryTextFocusRequest? request = null;
            bool? visibilityWhenFocusRequested = null;
            string queryTextWhenFocusRequested = null;

            viewModel.QueryTextFocusRequested += focusRequest =>
            {
                request = focusRequest;
                visibilityWhenFocusRequested = viewModel.MainWindowVisibility;
                queryTextWhenFocusRequested = viewModel.QueryText;
            };

            viewModel.ShowWithInjectedQuery("injected query");

            ClassicAssert.IsTrue(request.HasValue);
            AssertShowFocusRequest(request.Value, QueryTextFocusMode.CaretAtEnd);
            ClassicAssert.AreEqual("injected query", queryTextWhenFocusRequested);
            ClassicAssert.IsTrue(visibilityWhenFocusRequested.HasValue);
            ClassicAssert.IsFalse(visibilityWhenFocusRequested.Value);
            ClassicAssert.IsTrue(viewModel.MainWindowVisibility);
        }

        [Test]
        public void Show_WhenLastQueryModeIsActionKeywordPreserved_KeepsOnlyActionKeywordAndMovesCaretToEnd()
        {
            const string actionKeyword = "lq-preserved";
            RegisterActionKeyword(actionKeyword);
            try
            {
                var viewModel = CreateViewModel(LastQueryMode.ActionKeywordPreserved);
                viewModel.QueryText = $"{actionKeyword} search terms";

                var request = ShowAndCaptureFocusRequest(viewModel);

                ClassicAssert.AreEqual($"{actionKeyword} ", viewModel.QueryText);
                ClassicAssert.IsTrue(viewModel.LastQuerySelected);
                AssertShowFocusRequest(request, QueryTextFocusMode.CaretAtEnd);
            }
            finally
            {
                RemoveActionKeyword(actionKeyword);
            }
        }

        [Test]
        public void Show_WhenLastQueryModeIsActionKeywordSelected_KeepsOnlyActionKeywordAndSelectsItOnce()
        {
            const string actionKeyword = "lq-selected";
            RegisterActionKeyword(actionKeyword);
            try
            {
                var viewModel = CreateViewModel(LastQueryMode.ActionKeywordSelected);
                viewModel.QueryText = $"{actionKeyword} search terms";

                var firstRequest = ShowAndCaptureFocusRequest(viewModel);
                var secondRequest = ShowAndCaptureFocusRequest(viewModel);

                ClassicAssert.AreEqual($"{actionKeyword} ", viewModel.QueryText);
                ClassicAssert.IsTrue(viewModel.LastQuerySelected);
                AssertShowFocusRequest(firstRequest, QueryTextFocusMode.SelectAll);
                AssertShowFocusRequest(secondRequest, QueryTextFocusMode.Focus);
            }
            finally
            {
                RemoveActionKeyword(actionKeyword);
            }
        }

        [Test]
        public void Hide_WhenLastQueryModeIsPreserved_DoesNotClearExistingQuery()
        {
            var viewModel = CreateViewModel(LastQueryMode.Preserved);
            viewModel.QueryText = "existing query";

            viewModel.Hide();

            ClassicAssert.AreEqual("existing query", viewModel.QueryText);
            ClassicAssert.IsTrue(viewModel.LastQuerySelected);
        }

        [Test]
        public void Hide_WhenLastQueryModeIsEmpty_HidesBeforeClearingExistingQuery()
        {
            var viewModel = CreateViewModel(LastQueryMode.Empty);
            viewModel.QueryText = "existing query";
            string queryTextWhenHideRequested = null;
            viewModel.HideRequested += () => queryTextWhenHideRequested = viewModel.QueryText;

            viewModel.Hide();

            ClassicAssert.AreEqual("existing query", queryTextWhenHideRequested);
            ClassicAssert.AreEqual(string.Empty, viewModel.QueryText);
        }

        [Test]
        public void Hide_WhenLastQueryModeIsSelected_SelectsBeforeHideAndNextShowOnlyFocuses()
        {
            var viewModel = CreateViewModel(LastQueryMode.Selected);
            viewModel.QueryText = "existing query";
            viewModel.MainWindowVisibility = true;
            QueryTextFocusRequest? hideFocusRequest = null;
            bool? visibilityWhenFocusRequested = null;
            bool? visibilityWhenHideRequested = null;
            var eventOrder = new List<string>();

            void CaptureFocus(QueryTextFocusRequest request)
            {
                hideFocusRequest = request;
                visibilityWhenFocusRequested = viewModel.MainWindowVisibility;
                eventOrder.Add("focus");
            }

            void CaptureHide()
            {
                visibilityWhenHideRequested = viewModel.MainWindowVisibility;
                eventOrder.Add("hide");
            }

            viewModel.QueryTextFocusRequested += CaptureFocus;
            viewModel.HideRequested += CaptureHide;
            viewModel.Hide();
            viewModel.QueryTextFocusRequested -= CaptureFocus;
            viewModel.HideRequested -= CaptureHide;

            ClassicAssert.AreEqual(2, eventOrder.Count);
            ClassicAssert.AreEqual("focus", eventOrder[0]);
            ClassicAssert.AreEqual("hide", eventOrder[1]);
            ClassicAssert.IsTrue(hideFocusRequest.HasValue);
            ClassicAssert.IsFalse(hideFocusRequest.Value.ShowWindow);
            ClassicAssert.IsFalse(hideFocusRequest.Value.ActivateWindow);
            ClassicAssert.AreEqual(QueryTextFocusMode.SelectAll, hideFocusRequest.Value.Mode);
            ClassicAssert.IsTrue(visibilityWhenFocusRequested.HasValue);
            ClassicAssert.IsTrue(visibilityWhenFocusRequested.Value);
            ClassicAssert.IsTrue(visibilityWhenHideRequested.HasValue);
            ClassicAssert.IsTrue(visibilityWhenHideRequested.Value);
            ClassicAssert.IsFalse(viewModel.MainWindowVisibility);

            var showRequest = ShowAndCaptureFocusRequest(viewModel);

            AssertShowFocusRequest(showRequest, QueryTextFocusMode.Focus);
        }

        private static MainViewModel CreateViewModel(LastQueryMode lastQueryMode)
        {
            return new MainViewModel(new Settings { LastQueryMode = lastQueryMode });
        }

        private static QueryTextFocusRequest ShowAndCaptureFocusRequest(MainViewModel viewModel)
        {
            QueryTextFocusRequest? request = null;
            viewModel.QueryTextFocusRequested += CaptureRequest;
            viewModel.Show();
            viewModel.QueryTextFocusRequested -= CaptureRequest;

            ClassicAssert.IsTrue(request.HasValue);
            return request.Value;

            void CaptureRequest(QueryTextFocusRequest focusRequest) => request = focusRequest;
        }

        private static void AssertShowFocusRequest(QueryTextFocusRequest request, QueryTextFocusMode expectedMode)
        {
            ClassicAssert.IsTrue(request.ShowWindow);
            ClassicAssert.IsTrue(request.ActivateWindow);
            ClassicAssert.AreEqual(expectedMode, request.Mode);
        }

        private static void RegisterActionKeyword(string actionKeyword)
        {
            var pluginPair = new PluginPair
            {
                Plugin = new NoopPlugin(),
                Metadata = new PluginMetadata
                {
                    ID = Guid.NewGuid().ToString("N"),
                    Name = $"Test plugin {actionKeyword}",
                    ActionKeywords = [actionKeyword],
                }
            };

            GetNonGlobalPlugins()[actionKeyword] = [pluginPair];
        }

        private static void RemoveActionKeyword(string actionKeyword)
        {
            GetNonGlobalPlugins().TryRemove(actionKeyword, out _);
        }

        private static ConcurrentDictionary<string, List<PluginPair>> GetNonGlobalPlugins()
        {
            var field = typeof(PluginManager).GetField("_nonGlobalPlugins", BindingFlags.Static | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(field);

            return (ConcurrentDictionary<string, List<PluginPair>>)field!.GetValue(null)!;
        }

        private sealed class NoopPlugin : IAsyncPlugin
        {
            public Task InitAsync(PluginInitContext context) => Task.CompletedTask;

            public Task<List<Result>> QueryAsync(Query query, CancellationToken token) => Task.FromResult(new List<Result>());
        }
    }
}
