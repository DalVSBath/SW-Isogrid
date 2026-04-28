using System;

namespace IsogridGenerator.Core.Grid
{
    public class Triangle2D
    {
        public GridPoint V0 { get; }
        public GridPoint V1 { get; }
        public GridPoint V2 { get; }

        public Triangle2D(GridPoint v0, GridPoint v1, GridPoint v2)
        {
            V0 = v0; V1 = v1; V2 = v2;
        }

        public GridPoint Centroid => new GridPoint(
            (V0.X + V1.X + V2.X) / 3.0,
            (V0.Y + V1.Y + V2.Y) / 3.0);

        public double EdgeLength01 => V0.DistanceTo(V1);
        public double EdgeLength12 => V1.DistanceTo(V2);
        public double EdgeLength20 => V2.DistanceTo(V0);

        // True when all three interior angles are within tolerance of 60°.
        public bool IsEquilateral(double toleranceDeg = 0.001)
        {
            double tol = toleranceDeg * Math.PI / 180.0;
            return Math.Abs(InteriorAngle(V0, V1, V2) - Math.PI / 3.0) < tol
                && Math.Abs(InteriorAngle(V1, V2, V0) - Math.PI / 3.0) < tol
                && Math.Abs(InteriorAngle(V2, V0, V1) - Math.PI / 3.0) < tol;
        }

        public static double InteriorAngle(GridPoint vertex, GridPoint a, GridPoint b)
        {
            double ax = a.X - vertex.X, ay = a.Y - vertex.Y;
            double bx = b.X - vertex.X, by = b.Y - vertex.Y;
            double dot = ax * bx + ay * by;
            double mag = Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by);
            return Math.Acos(Math.Max(-1.0, Math.Min(1.0, dot / mag)));
        }

        // Signed area — positive when vertices are counter-clockwise.
        public double SignedArea =>
            0.5 * ((V1.X - V0.X) * (V2.Y - V0.Y) - (V2.X - V0.X) * (V1.Y - V0.Y));

        /// <summary>
        /// Computes the circular fillet geometry at each of the three corners of this triangle.
        /// Returns an array of length 3: index 0 = fillet at V0, 1 = V1, 2 = V2.
        ///
        /// For a 60° interior angle, the tangent length from each vertex is r√3 and the arc
        /// centre lies at distance 2r from the vertex along the angle bisector.
        ///
        /// When <paramref name="r"/> is 0 all three fillets are degenerate
        /// (<see cref="CornerFillet.IsDegenerate"/> = true).
        /// </summary>
        /// <param name="r">
        /// Fillet radius in metres.  Must satisfy
        /// <c>r ≤ edgeLength / (2√3)</c> so that adjacent fillets on the same edge do not overlap.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="r"/> is negative or too large for the triangle's edge length.
        /// </exception>
        public CornerFillet[] ComputeFillets(double r)
        {
            if (r < 0.0)
                throw new ArgumentOutOfRangeException(nameof(r), "Fillet radius must be >= 0.");

            // Maximum radius before adjacent fillets on the same edge begin to overlap.
            // Each fillet consumes a tangent length of r√3 from each end of an edge;
            // two fillets share each edge, so the limit is: 2·r√3 ≤ edgeLength.
            double edgeLen = EdgeLength01;   // all three edges equal for equilateral triangles
            double maxR    = edgeLen / (2.0 * Math.Sqrt(3.0));

            if (r > maxR + 1e-12)
                throw new ArgumentOutOfRangeException(nameof(r),
                    $"Fillet radius {r * 1e3:F4} mm exceeds the maximum {maxR * 1e3:F4} mm " +
                    $"for an edge length of {edgeLen * 1e3:F4} mm. " +
                    $"Fillets would overlap on each edge.");

            if (r <= 0.0)
            {
                // Degenerate: tangent points collapse to the vertex itself.
                return new[]
                {
                    new CornerFillet(V0, V0, V0, 0.0),
                    new CornerFillet(V1, V1, V1, 0.0),
                    new CornerFillet(V2, V2, V2, 0.0),
                };
            }

            return new[]
            {
                ComputeCornerFillet(V0, V1, V2, r),
                ComputeCornerFillet(V1, V2, V0, r),
                ComputeCornerFillet(V2, V0, V1, r),
            };
        }

        // Computes the fillet at `vertex` with adjacent edges going toward `nextVert` and `prevVert`.
        // TangentA is on the edge toward nextVert; TangentB is on the edge toward prevVert.
        private static CornerFillet ComputeCornerFillet(
            GridPoint vertex, GridPoint nextVert, GridPoint prevVert, double r)
        {
            var uNext = Normalize(nextVert - vertex);
            var uPrev = Normalize(prevVert - vertex);

            // Tangent length from the vertex to where the arc meets each adjacent edge.
            // For a 60° interior angle: t = r / tan(30°) = r * √3.
            double tangentLen = r / Math.Tan(Math.PI / 6.0);   // = r * √3

            var tangentA = vertex + uNext * tangentLen;
            var tangentB = vertex + uPrev * tangentLen;

            // Arc centre lies on the interior angle bisector at distance r / sin(30°) = 2r
            // from the vertex, so its perpendicular distance to each adjacent edge equals r.
            var bisector  = Normalize(uNext + uPrev);
            var arcCenter = vertex + bisector * (r / Math.Sin(Math.PI / 6.0));  // = 2r

            return new CornerFillet(tangentA, tangentB, arcCenter, r);
        }

        private static GridPoint Normalize(GridPoint v)
        {
            double mag = Math.Sqrt(v.X * v.X + v.Y * v.Y);
            if (mag < 1e-15)
                throw new InvalidOperationException(
                    $"Cannot normalize a zero-length vector at ({v.X}, {v.Y}). " +
                    "Check that triangle vertices are distinct.");
            return new GridPoint(v.X / mag, v.Y / mag);
        }

        public override string ToString() =>
            $"Triangle({V0}, {V1}, {V2})";
    }
}

