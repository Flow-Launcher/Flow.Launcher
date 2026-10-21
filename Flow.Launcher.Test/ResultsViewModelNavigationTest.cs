using System.Threading;
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

    private static ResultsViewModel CreateViewModel(int count, int pageSize, int selectedIndex)
    {
        var settings = new Settings { MaxResultsToShow = pageSize };
        var viewModel = new ResultsViewModel(settings, null);
        for (var index = 0; index < count; index++)
        {
            viewModel.Results.Add(new ResultViewModel(new Result { Title = $"Result {index}" }, settings));
        }

        viewModel.SelectedIndex = selectedIndex;
        return viewModel;
    }
}
