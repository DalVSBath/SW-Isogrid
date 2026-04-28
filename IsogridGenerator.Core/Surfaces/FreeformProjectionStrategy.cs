using System.Collections.Generic;
using IsogridGenerator.Core.Grid;
using IsogridGenerator.Core.Parameters;

namespace IsogridGenerator.Core.Surfaces
{
    /// <summary>
    /// Grid strategy for freeform (double-curvature) faces using flat projection.
    /// Triangles are computed in the flat projection plane above the surface.
    /// The SW layer projects them onto the face via InsertProjectCurveFeature and
    /// cuts with an "Offset from surface" end condition.
    ///
    /// Limitation: projection preserves XY position, not arc length — triangles will
    /// appear stretched in high-curvature regions. This is the V1 "good enough" approach.
    /// True geodesic UV meshing is the V2 path.
    /// </summary>
    public class FreeformProjectionStrategy : ISurfaceStrategy
    {
        public IReadOnlyList<Triangle2D> GeneratePockets(Bounds2D bounds, IsogridParameters p) =>
            TriangleGridGenerator.GeneratePockets(bounds.Width, bounds.Height, p);
    }
}
