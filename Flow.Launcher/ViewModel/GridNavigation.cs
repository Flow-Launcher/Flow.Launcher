using System;

namespace Flow.Launcher.ViewModel
{
    public readonly record struct GridMove(int Index, bool LeavesGrid);

    public static class GridNavigation
    {
        private const int NoSelection = -1;

        public static int NextCell(int index, int count)
        {
            if (count == 0) return NoSelection;
            if (IsOutOfRange(index, count)) return Anchor(index, count);
            return Math.Min(index + 1, count - 1);
        }

        public static int PrevCell(int index, int count)
        {
            if (count == 0) return NoSelection;
            if (IsOutOfRange(index, count)) return Anchor(index, count);
            return Math.Max(index - 1, 0);
        }

        public static int NextRow(int index, int count, int columns)
        {
            if (count == 0) return NoSelection;
            if (IsOutOfRange(index, count)) return Anchor(index, count);

            columns = Math.Max(1, columns);
            var below = index + columns;
            if (below < count) return below;
            return IsInLastRow(index, count, columns) ? index : count - 1;
        }

        public static GridMove PrevRow(int index, int count, int columns)
        {
            if (count == 0) return new GridMove(NoSelection, false);
            if (IsOutOfRange(index, count)) return new GridMove(Anchor(index, count), false);

            columns = Math.Max(1, columns);
            var above = index - columns;
            return above >= 0 ? new GridMove(above, false) : new GridMove(index, true);
        }

        public static int NextPage(int index, int count, int pageSize)
        {
            if (count == 0) return NoSelection;
            if (IsOutOfRange(index, count)) return Anchor(index, count);
            return Math.Min(index + pageSize, count - 1);
        }

        public static int PrevPage(int index, int count, int pageSize)
        {
            if (count == 0) return NoSelection;
            if (IsOutOfRange(index, count)) return Anchor(index, count);
            return Math.Max(index - pageSize, 0);
        }

        private static bool IsOutOfRange(int index, int count) => index < 0 || index >= count;

        private static int Anchor(int index, int count) => Math.Clamp(index, 0, count - 1);

        private static bool IsInLastRow(int index, int count, int columns) => index / columns == (count - 1) / columns;
    }
}
