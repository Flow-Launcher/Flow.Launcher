using System;

namespace Flow.Launcher.ViewModel
{
    public readonly record struct GridSize(int Columns, int Rows)
    {
        public const int MinColumns = 2;
        public const int MaxColumns = 10;
        public const int MinRows = 1;
        public const int MaxRows = 8;

        public GridSize Grow() => new GridSize(Columns + 1, Rows + 1).Clamped();

        public GridSize Shrink() => new GridSize(Columns - 1, Rows - 1).Clamped();

        public GridSize Clamped() => new(Math.Clamp(Columns, MinColumns, MaxColumns), Math.Clamp(Rows, MinRows, MaxRows));
    }
}
