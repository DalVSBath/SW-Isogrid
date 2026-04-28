using System;

namespace IsogridGenerator.Core.Grid
{
    public readonly struct GridPoint : IEquatable<GridPoint>
    {
        public double X { get; }
        public double Y { get; }

        public GridPoint(double x, double y) { X = x; Y = y; }

        public double DistanceTo(GridPoint other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static GridPoint operator +(GridPoint a, GridPoint b) =>
            new GridPoint(a.X + b.X, a.Y + b.Y);

        public static GridPoint operator -(GridPoint a, GridPoint b) =>
            new GridPoint(a.X - b.X, a.Y - b.Y);

        public static GridPoint operator *(GridPoint p, double s) =>
            new GridPoint(p.X * s, p.Y * s);

        public bool Equals(GridPoint other) =>
            Math.Abs(X - other.X) < 1e-12 && Math.Abs(Y - other.Y) < 1e-12;

        public override bool Equals(object obj) => obj is GridPoint p && Equals(p);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Math.Round(X, 10).GetHashCode();
                hash = hash * 31 + Math.Round(Y, 10).GetHashCode();
                return hash;
            }
        }

        public override string ToString() => $"({X:G6}, {Y:G6})";
    }
}
