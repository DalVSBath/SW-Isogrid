using System.Collections.Generic;
using IsogridGenerator.Core.Grid;
using IsogridGenerator.Core.Parameters;

namespace IsogridGenerator.Core.Surfaces
{
    /// <summary>
    /// Computes the 2D pocket triangles for a surface type.
    /// Implementations know the geometry of their surface (e.g. how to unroll a cylinder)
    /// but have no SolidWorks references. The SW layer maps the returned triangles to
    /// sketch entities and feature operations.
    /// </summary>
    public interface ISurfaceStrategy
    {
        /// <summary>
        /// Returns inset pocket triangles in the 2D coordinate space appropriate for the
        /// surface (e.g. unrolled cylinder coordinates). All values are in metres.
        /// </summary>
        IReadOnlyList<Triangle2D> GeneratePockets(Bounds2D bounds, IsogridParameters p);
    }
}
