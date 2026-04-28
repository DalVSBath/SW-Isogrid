using System;
using System.Collections.Generic;
using IsogridGenerator.Core.Grid;
using IsogridGenerator.Core.Parameters;

namespace IsogridGenerator.Core.Surfaces
{
    /// <summary>
    /// Grid strategy for cylindrical faces. The cylinder is unrolled to a flat rectangle
    /// of width = 2πR and height = cylinder axis length. The SW layer wraps the
    /// resulting sketch back onto the surface using IFeatureManager.InsertWrapFeature (Deboss).
    /// </summary>
    public class CylindricalStrategy : ISurfaceStrategy
    {
        private readonly double _radius;  // metres

        public CylindricalStrategy(double radius) { _radius = radius; }

        public double UnrolledWidth => 2.0 * Math.PI * _radius;

        public IReadOnlyList<Triangle2D> GeneratePockets(Bounds2D bounds, IsogridParameters p)
        {
            // bounds.Width is ignored — unrolled width is always 2πR.
            var unrolledBounds = new Bounds2D(UnrolledWidth, bounds.Height);
            return TriangleGridGenerator.GeneratePockets(
                unrolledBounds.Width, unrolledBounds.Height, p);
        }
    }
}
