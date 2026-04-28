using System.Collections.Generic;
using IsogridGenerator.Core.Grid;
using IsogridGenerator.Core.Parameters;

namespace IsogridGenerator.Core.Surfaces
{
    /// <summary>
    /// Grid strategy for flat faces. Pocket triangles are in sketch-plane (face) coordinates.
    /// The SW layer creates a sketch directly on the face and uses FeatureCut4 to depth D.
    /// </summary>
    public class PlanarStrategy : ISurfaceStrategy
    {
        public IReadOnlyList<Triangle2D> GeneratePockets(Bounds2D bounds, IsogridParameters p) =>
            TriangleGridGenerator.GeneratePockets(bounds.Width, bounds.Height, p);
    }
}
