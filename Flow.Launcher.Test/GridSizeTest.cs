using Flow.Launcher.ViewModel;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
public class GridSizeTest
{
    [Test]
    public void Grow_AddsAColumnAndARow()
    {
        Assert.That(new GridSize(6, 3).Grow(), Is.EqualTo(new GridSize(7, 4)));
    }

    [Test]
    public void Shrink_RemovesAColumnAndARow()
    {
        Assert.That(new GridSize(6, 3).Shrink(), Is.EqualTo(new GridSize(5, 2)));
    }

    [Test]
    public void Grow_StopsEachDimensionAtItsMaximum()
    {
        Assert.That(new GridSize(GridSize.MaxColumns, 3).Grow(), Is.EqualTo(new GridSize(GridSize.MaxColumns, 4)));
        Assert.That(new GridSize(6, GridSize.MaxRows).Grow(), Is.EqualTo(new GridSize(7, GridSize.MaxRows)));
    }

    [Test]
    public void Shrink_StopsEachDimensionAtItsMinimum()
    {
        Assert.That(new GridSize(GridSize.MinColumns, 3).Shrink(), Is.EqualTo(new GridSize(GridSize.MinColumns, 2)));
        Assert.That(new GridSize(6, GridSize.MinRows).Shrink(), Is.EqualTo(new GridSize(5, GridSize.MinRows)));
    }

    [TestCase(0, 0, GridSize.MinColumns, GridSize.MinRows)]
    [TestCase(100, 100, GridSize.MaxColumns, GridSize.MaxRows)]
    [TestCase(6, 3, 6, 3)]
    public void Clamped_KeepsHandEditedSettingsInRange(int columns, int rows, int expectedColumns, int expectedRows)
    {
        Assert.That(new GridSize(columns, rows).Clamped(), Is.EqualTo(new GridSize(expectedColumns, expectedRows)));
    }
}
