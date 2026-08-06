using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Flow.Launcher.Avalonia.ViewModel;
using Flow.Launcher.Infrastructure.UserSettings;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test.Avalonia
{
    [TestFixture]
    public class ProgrammaticQueryFocusRequestTest
    {
        [Test]
        public void Show_WhenCalled_ThenRequestsSelectAllFocus()
        {
            var viewModel = new MainViewModel(new Settings());
            QueryTextFocusRequest? requestedFocus = null;
            viewModel.QueryTextFocusRequested += request => requestedFocus = request;

            viewModel.Show();

            ClassicAssert.IsTrue(requestedFocus.HasValue);
            ClassicAssert.IsTrue(requestedFocus.Value.ShowWindow);
            ClassicAssert.IsTrue(requestedFocus.Value.ActivateWindow);
            ClassicAssert.AreEqual(QueryTextFocusMode.SelectAll, requestedFocus.Value.Mode);
        }

        [Test]
        public void ShowWithInjectedQuery_WhenCalled_ThenRequestsCaretAtEndAndLeavesResultsViewActiveWithClearedContext()
        {
            var viewModel = new MainViewModel(new Settings());
            viewModel.ActiveView = ActiveView.ContextMenu;
            viewModel.ContextMenu.AddResult(new ResultViewModel { Title = "context item" });

            QueryTextFocusRequest? requestedFocus = null;
            viewModel.QueryTextFocusRequested += request => requestedFocus = request;

            viewModel.ShowWithInjectedQuery("abc");

            ClassicAssert.AreEqual("abc", viewModel.QueryText);
            ClassicAssert.AreEqual(ActiveView.Results, viewModel.ActiveView);
            ClassicAssert.AreEqual(0, viewModel.ContextMenu.Results.Count);

            ClassicAssert.IsTrue(requestedFocus.HasValue);
            ClassicAssert.IsTrue(requestedFocus.Value.ShowWindow);
            ClassicAssert.IsTrue(requestedFocus.Value.ActivateWindow);
            ClassicAssert.AreEqual(QueryTextFocusMode.CaretAtEnd, requestedFocus.Value.Mode);
        }

        [Test]
        public async Task ExpandedQueryRewrite_WhenApplied_ThenRequestsCaretAtEndWithoutShowingWindow()
        {
            var settings = new Settings();
            settings.BuiltinShortcuts = new()
            {
                new BuiltinShortcutModel("{magic}", "test", () => "expanded")
            };

            var viewModel = new MainViewModel(settings);
            QueryTextFocusRequest? requestedFocus = null;
            viewModel.QueryTextFocusRequested += request => requestedFocus = request;

            var buildQueryMethod = typeof(MainViewModel).GetMethod("BuildQueryAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            ClassicAssert.IsNotNull(buildQueryMethod);

            var queryBuilder = new StringBuilder("run {magic}");
            var queryBuilderTmp = new StringBuilder("run {magic}");
            var invocationResult = buildQueryMethod!.Invoke(viewModel, new object[]
            {
                settings.BuiltinShortcuts,
                queryBuilder,
                queryBuilderTmp
            });

            ClassicAssert.IsNotNull(invocationResult);
            await (Task)invocationResult!;

            ClassicAssert.AreEqual("run expanded", viewModel.QueryText);
            ClassicAssert.IsTrue(requestedFocus.HasValue);
            ClassicAssert.IsFalse(requestedFocus.Value.ShowWindow);
            ClassicAssert.IsFalse(requestedFocus.Value.ActivateWindow);
            ClassicAssert.AreEqual(QueryTextFocusMode.CaretAtEnd, requestedFocus.Value.Mode);
        }
    }
}
