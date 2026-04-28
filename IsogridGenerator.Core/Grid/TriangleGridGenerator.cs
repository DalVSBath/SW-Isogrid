using System;
using System.Collections.Generic;
using IsogridGenerator.Core.Parameters;

namespace IsogridGenerator.Core.Grid
{
    /// <summary>
    /// Generates a list of inset equilateral triangle pockets for an isogrid pattern.
    ///
    /// Coordinate system: origin at bottom-left of the bounding region,
    /// X rightward, Y upward. All values in metres.
    ///
    /// The tessellation produces two triangle types per grid cell:
    ///   Up ▲: base on bottom, apex at top.
    ///   Down ▽: apex at bottom, base on top.
    ///
    /// Each pocket is the original triangle scaled toward its centroid by
    /// PocketScaleFactor = 1 - B·√3/A, which sets the perpendicular distance
    /// from each pocket edge to the original triangle edge equal to B/2,
    /// giving a rib width of B between adjacent pockets.
    /// </summary>
    public static class TriangleGridGenerator
    {
        public static IReadOnlyList<Triangle2D> GeneratePockets(
            double width, double height, IsogridParameters p)
        {
            if (p.PocketScaleFactor <= 0)
                throw new ArgumentException(
                    $"Rib thickness B={p.B*1e3:F2} mm is too large for edge length " +
                    $"A={p.A*1e3:F2} mm. Require B < A/√3 ≈ {p.A/Math.Sqrt(3)*1e3:F2} mm.");

            double a = p.A;
            double h = p.RowHeight;            // a·√3/2
            double scale = p.PocketScaleFactor;

            // Extra columns/rows ensure boundary triangles are generated.
            int rowCount = (int)Math.Ceiling(height / h) + 2;
            int colCount = (int)Math.Ceiling(width  / a) + 2;

            var pockets = new List<Triangle2D>(rowCount * colCount * 2);

            for (int row = -1; row < rowCount; row++)
            {
                double yBase = row * h;

                // Odd rows are shifted right by a/2 so their vertices sit exactly
                // between those of the adjacent even rows — producing the alternating
                // ▲▼▲▼ / ▼▲▼▲ tessellation shown in the spec.
                // Math.Abs handles the negative start row correctly.
                bool oddRow = Math.Abs(row) % 2 == 1;

                for (int col = -1; col < colCount; col++)
                {
                    double xBase = col * a;

                    // Up ▲: for even rows starts at xBase; for odd rows shifted +a/2
                    // so the base straddles the gap between even-row vertices.
                    var up = MakeUp(xBase + (oddRow ? a / 2.0 : 0.0), yBase, a, h);
                    if (CentroidInBounds(up, width, height))
                        pockets.Add(InsetTriangle(up, scale));

                    // Down ▼: for even rows starts at xBase (apex at xBase+a, base at top);
                    // for odd rows shifted -a/2 so the apex slots into the gap between
                    // even-row top vertices.
                    var down = MakeDown(xBase + (oddRow ? -a / 2.0 : 0.0), yBase, a, h);
                    if (CentroidInBounds(down, width, height))
                        pockets.Add(InsetTriangle(down, scale));
                }
            }

            return pockets;
        }

        // Up ▲: V0 bottom-left, V1 bottom-right, V2 top-centre.
        private static Triangle2D MakeUp(double x, double y, double a, double h) =>
            new Triangle2D(
                new GridPoint(x,         y),
                new GridPoint(x + a,     y),
                new GridPoint(x + a/2.0, y + h));

        // Down ▽: V0 bottom-centre, V1 top-left, V2 top-right.
        // Sits between the up triangle of column col and the up triangle of column col+1.
        private static Triangle2D MakeDown(double x, double y, double a, double h) =>
            new Triangle2D(
                new GridPoint(x + a,         y),
                new GridPoint(x + a/2.0,     y + h),
                new GridPoint(x + 3.0*a/2.0, y + h));

        private static Triangle2D InsetTriangle(Triangle2D t, double scaleFactor)
        {
            var c = t.Centroid;
            return new Triangle2D(
                ScaleFrom(t.V0, c, scaleFactor),
                ScaleFrom(t.V1, c, scaleFactor),
                ScaleFrom(t.V2, c, scaleFactor));
        }

        // Moves v toward (or away from) origin along the line origin→v, scaled by factor.
        private static GridPoint ScaleFrom(GridPoint v, GridPoint origin, double factor) =>
            origin + (v - origin) * factor;

        // Include pockets whose centroid falls within the generation bounds.
        // Triangles partially outside the bounds are intentionally excluded to avoid
        // open sketch profiles; the SketchBuilder trims boundary ribs in SolidWorks.
        private static bool CentroidInBounds(Triangle2D t, double width, double height)
        {
            var c = t.Centroid;
            return c.X >= 0.0 && c.X <= width
                && c.Y >= 0.0 && c.Y <= height;
        }
    }
}
