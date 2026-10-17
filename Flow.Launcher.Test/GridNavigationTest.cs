using Flow.Launcher.ViewModel;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
public class GridNavigationTest
{
    private const int Count = 10;
    private const int Columns = 4;

    [TestCase(0, 4)]
    [TestCase(5, 9)]
    [TestCase(6, 9)]
    [TestCase(7, 9)]
    [TestCase(8, 8)]
    [TestCase(9, 9)]
    public void NextRow_MovesDownWithoutWrapping(int index, int expected)
    {
        Assert.That(GridNavigation.NextRow(index, Count, Columns), Is.EqualTo(expected));
    }

    [TestCase(4, 0, false)]
    [TestCase(9, 5, false)]
    [TestCase(2, 2, true)]
    [TestCase(0, 0, true)]
    public void PrevRow_MovesUpOrLeavesGridFromFirstRow(int index, int expectedIndex, bool expectedLeaves)
    {
        Assert.That(GridNavigation.PrevRow(index, Count, Columns), Is.EqualTo(new GridMove(expectedIndex, expectedLeaves)));
    }

    [TestCase(3, 4)]
    [TestCase(9, 9)]
    public void NextCell_MovesRightAcrossRowsWithoutWrapping(int index, int expected)
    {
        Assert.That(GridNavigation.NextCell(index, Count), Is.EqualTo(expected));
    }

    [TestCase(4, 3)]
    [TestCase(0, 0)]
    public void PrevCell_MovesLeftAcrossRowsWithoutWrapping(int index, int expected)
    {
        Assert.That(GridNavigation.PrevCell(index, Count), Is.EqualTo(expected));
    }

    [Test]
    public void SingleColumn_BehavesLikeAListThatLeavesAtTheTop()
    {
        Assert.That(GridNavigation.NextRow(2, 5, 1), Is.EqualTo(3));
        Assert.That(GridNavigation.PrevRow(0, 5, 1), Is.EqualTo(new GridMove(0, true)));
    }

    [Test]
    public void SingleItem_StaysPutAndLeavesOnUp()
    {
        Assert.That(GridNavigation.NextRow(0, 1, Columns), Is.EqualTo(0));
        Assert.That(GridNavigation.NextCell(0, 1), Is.EqualTo(0));
        Assert.That(GridNavigation.PrevRow(0, 1, Columns), Is.EqualTo(new GridMove(0, true)));
    }

    [Test]
    public void EmptyResults_ReturnNoSelection()
    {
        Assert.That(GridNavigation.NextRow(0, 0, Columns), Is.EqualTo(-1));
        Assert.That(GridNavigation.NextCell(0, 0), Is.EqualTo(-1));
        Assert.That(GridNavigation.PrevCell(0, 0), Is.EqualTo(-1));
        Assert.That(GridNavigation.NextPage(0, 0, 8), Is.EqualTo(-1));
        Assert.That(GridNavigation.PrevPage(0, 0, 8), Is.EqualTo(-1));
        Assert.That(GridNavigation.PrevRow(0, 0, Columns), Is.EqualTo(new GridMove(-1, false)));
    }

    [Test]
    public void OutOfRangeIndex_IsAnchoredWithoutMoving()
    {
        Assert.That(GridNavigation.NextRow(-1, Count, Columns), Is.EqualTo(0));
        Assert.That(GridNavigation.NextCell(12, Count), Is.EqualTo(9));
        Assert.That(GridNavigation.PrevCell(-1, Count), Is.EqualTo(0));
        Assert.That(GridNavigation.PrevRow(15, Count, Columns), Is.EqualTo(new GridMove(9, false)));
    }

    [Test]
    public void ZeroColumns_IsTreatedAsOneColumn()
    {
        Assert.That(GridNavigation.NextRow(0, Count, 0), Is.EqualTo(1));
        Assert.That(GridNavigation.PrevRow(1, Count, 0), Is.EqualTo(new GridMove(0, false)));
    }

    [TestCase(1, 10, 8, 9)]
    [TestCase(5, 30, 8, 13)]
    public void NextPage_MovesByPageAndClamps(int index, int count, int pageSize, int expected)
    {
        Assert.That(GridNavigation.NextPage(index, count, pageSize), Is.EqualTo(expected));
    }

    [TestCase(5, 10, 8, 0)]
    [TestCase(20, 30, 8, 12)]
    public void PrevPage_MovesByPageAndClamps(int index, int count, int pageSize, int expected)
    {
        Assert.That(GridNavigation.PrevPage(index, count, pageSize), Is.EqualTo(expected));
    }
}
