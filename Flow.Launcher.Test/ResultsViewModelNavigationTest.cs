using System;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.ViewModel;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
[Apartment(ApartmentState.STA)]
public class ResultsViewModelNavigationTest
{
    [TestCase(17, 15, 0, 15)]
    [TestCase(17, 15, 15, 16)]
    [TestCase(17, 15, 16, 16)]
    [TestCase(3, 15, 0, 2)]
    [TestCase(1, 15, 0, 0)]
    [TestCase(0, 15, -1, -1)]
    public void NextPageStopsAtLastResult(int count, int pageSize, int selectedIndex, int expected)
    {
        var viewModel = CreateViewModel(count, pageSize, selectedIndex);

        viewModel.SelectNextPage();

        Assert.That(viewModel.SelectedIndex, Is.EqualTo(expected));
    }

    [TestCase(17, 15, 16, 1)]
    [TestCase(17, 15, 1, 0)]
    [TestCase(17, 15, 0, 0)]
    [TestCase(3, 15, 2, 0)]
    [TestCase(1, 15, 0, 0)]
    [TestCase(0, 15, -1, -1)]
    public void PreviousPageStopsAtFirstResult(int count, int pageSize, int selectedIndex, int expected)
    {
        var viewModel = CreateViewModel(count, pageSize, selectedIndex);

        viewModel.SelectPrevPage();

        Assert.That(viewModel.SelectedIndex, Is.EqualTo(expected));
    }

    [Test]
    public void RepeatedPageDownReachesAndKeepsLastResult()
    {
        var viewModel = CreateViewModel(17, 15, 0);

        viewModel.SelectNextPage();
        Assert.That(viewModel.SelectedIndex, Is.EqualTo(15));
        viewModel.SelectNextPage();
        Assert.That(viewModel.SelectedIndex, Is.EqualTo(16));
        viewModel.SelectNextPage();
        Assert.That(viewModel.SelectedIndex, Is.EqualTo(16));
    }

    [Test]
    public void RepeatedPageUpReachesAndKeepsFirstResult()
    {
        var viewModel = CreateViewModel(17, 15, 16);

        viewModel.SelectPrevPage();
        Assert.That(viewModel.SelectedIndex, Is.EqualTo(1));
        viewModel.SelectPrevPage();
        Assert.That(viewModel.SelectedIndex, Is.Zero);
        viewModel.SelectPrevPage();
        Assert.That(viewModel.SelectedIndex, Is.Zero);
    }

    [Test]
    public void DownArrowStillWrapsFromLastResult()
    {
        var viewModel = CreateViewModel(17, 15, 16);

        viewModel.SelectNextResult();

        Assert.That(viewModel.SelectedIndex, Is.Zero);
    }

    [Test]
    public void UpArrowStillWrapsFromFirstResult()
    {
        var viewModel = CreateViewModel(17, 15, 0);

        viewModel.SelectPrevResult();

        Assert.That(viewModel.SelectedIndex, Is.EqualTo(16));
    }

    [Test]
    public void PageNavigationUsesVisibleRowsWhenPreviewIsOn()
    {
        // Preview reserves enough space for its minimum usable height.
        const int resultCount = 17;
        const int configuredPageSize = 3;
        const int startingIndex = 0;
        var viewModel = CreateViewModel(resultCount, configuredPageSize, startingIndex, previewVisible: true);
        var expectedVisibleRows = (int)Math.Ceiling(Constant.PreviewMinimumHeight / viewModel.ItemHeightSize);

        viewModel.SelectNextPage();

        Assert.That(viewModel.SelectedIndex, Is.EqualTo(expectedVisibleRows));
    }

    [Test]
    public void PageNavigationClampsToLastResultWhenPreviewHasFewerResultsThanVisibleRows()
    {
        const int resultCount = 3;
        const int configuredPageSize = 3;
        const int startingIndex = 0;
        var viewModel = CreateViewModel(resultCount, configuredPageSize, startingIndex, previewVisible: true);
        var expectedVisibleRows = (int)Math.Ceiling(Constant.PreviewMinimumHeight / viewModel.ItemHeightSize);

        viewModel.SelectNextPage();

        Assert.That(expectedVisibleRows, Is.GreaterThan(resultCount));
        Assert.That(viewModel.SelectedIndex, Is.EqualTo(resultCount - 1));
    }

    [Test]
    public void PageNavigationUsesConfiguredRowsWhenPreviewIsOff()
    {
        const int resultCount = 17;
        const int configuredPageSize = 3;
        const int startingIndex = 0;
        var viewModel = CreateViewModel(resultCount, configuredPageSize, startingIndex);

        viewModel.SelectNextPage();

        Assert.That(viewModel.SelectedIndex, Is.EqualTo(configuredPageSize));
    }

    [Test]
    public void PreviewDoesNotReduceConfiguredPageSize()
    {
        // Preview can expand the list, but it must not shrink a larger configured page.
        const int resultCount = 17;
        const int configuredPageSize = 8;
        const int startingIndex = 0;
        var viewModel = CreateViewModel(resultCount, configuredPageSize, startingIndex, previewVisible: true);
        var expectedVisibleRows = Math.Max(
            configuredPageSize,
            (int)Math.Ceiling(Constant.PreviewMinimumHeight / viewModel.ItemHeightSize));

        viewModel.SelectNextPage();

        Assert.That(viewModel.SelectedIndex, Is.EqualTo(expectedVisibleRows));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PageNavigationWaitsForSynchronizedCollectionRefresh(bool nextPage)
    {
        var viewModel = CreateViewModel(17, 15, 15);
        using var navigationStarted = new ManualResetEventSlim();
        Thread navigation = null;
        Exception navigationFailure = null;
        var started = false;
        var blockedOnLock = false;
        var completedDuringRefresh = false;

        BindingOperations.AccessCollection(viewModel.Results, () =>
        {
            navigation = new Thread(() =>
            {
                navigationStarted.Set();
                try
                {
                    if (nextPage)
                        viewModel.SelectNextPage();
                    else
                        viewModel.SelectPrevPage();
                }
                catch (Exception exception)
                {
                    navigationFailure = exception;
                }
            }) { IsBackground = true };
            navigation.Start();

            started = navigationStarted.Wait(TimeSpan.FromSeconds(5));
            SpinWait.SpinUntil(
                () => (navigation.ThreadState & ThreadState.WaitSleepJoin) != 0 || !navigation.IsAlive,
                TimeSpan.FromSeconds(5));
            blockedOnLock = (navigation.ThreadState & ThreadState.WaitSleepJoin) != 0;
            completedDuringRefresh = !navigation.IsAlive;
            viewModel.Results.Clear();
        }, writeAccess: true);

        Assert.That(navigation.Join(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(navigationFailure, Is.Null);
        Assert.That(started, Is.True);
        Assert.That(blockedOnLock, Is.True);
        Assert.That(completedDuringRefresh, Is.False);
        Assert.That(viewModel.SelectedIndex, Is.EqualTo(-1));
    }

    [TestCase(true, 15)]
    [TestCase(false, 0)]
    public void PageNavigationWaitsForInitialRefreshSelection(bool nextPage, int expected)
    {
        var viewModel = CreateViewModel(0, 15, -1);
        var refreshThreadId = Environment.CurrentManagedThreadId;
        using var navigationStarted = new ManualResetEventSlim();
        Thread navigation = null;
        Exception navigationFailure = null;
        var blockedOnLock = false;
        var visibilityAfterNavigation = Visibility.Collapsed;

        // Pause the real refresh at its initial selection notification. Paging must
        // wait for the entire refresh transition, including making the list visible.
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(ResultsViewModel.SelectedIndex) ||
                Environment.CurrentManagedThreadId != refreshThreadId)
                return;

            navigation = new Thread(() =>
            {
                navigationStarted.Set();
                try
                {
                    if (nextPage)
                        viewModel.SelectNextPage();
                    else
                        viewModel.SelectPrevPage();
                    visibilityAfterNavigation = viewModel.Visibility;
                }
                catch (Exception exception)
                {
                    navigationFailure = exception;
                }
            }) { IsBackground = true };
            navigation.Start();
            navigationStarted.Wait(TimeSpan.FromSeconds(5));
            SpinWait.SpinUntil(
                () => (navigation.ThreadState & ThreadState.WaitSleepJoin) != 0 || !navigation.IsAlive,
                TimeSpan.FromSeconds(5));
            blockedOnLock = (navigation.ThreadState & ThreadState.WaitSleepJoin) != 0;
        };

        var results = new System.Collections.Generic.List<Result>();
        for (var index = 0; index < 17; index++)
            results.Add(new Result { Title = $"Result {index}", PluginID = "refresh-test" });

        viewModel.AddResults(results, "refresh-test");

        Assert.That(navigation, Is.Not.Null, "The real refresh must initialize selection.");
        Assert.That(navigation.Join(TimeSpan.FromSeconds(5)), Is.True);
        Assert.That(navigationFailure, Is.Null);
        Assert.That(blockedOnLock, Is.True, "Paging must wait for refresh selection to finish.");
        Assert.That(visibilityAfterNavigation, Is.EqualTo(Visibility.Visible));
        Assert.That(viewModel.SelectedIndex, Is.EqualTo(expected));
    }

    private static ResultsViewModel CreateViewModel(
        int count,
        int pageSize,
        int selectedIndex,
        bool previewVisible = false)
    {
        var settings = new Settings
        {
            MaxResultsToShow = pageSize
        };

        var viewModel = new ResultsViewModel(settings, null, previewVisible);
        for (var index = 0; index < count; index++)
        {
            viewModel.Results.Add(new ResultViewModel(new Result { Title = $"Result {index}" }, settings));
        }

        viewModel.SelectedIndex = selectedIndex;
        return viewModel;
    }
}
