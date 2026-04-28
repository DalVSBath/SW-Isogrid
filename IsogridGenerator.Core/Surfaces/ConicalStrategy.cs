using System;
using System.Collections.Generic;
using IsogridGenerator.Core.Grid;
using IsogridGenerator.Core.Parameters;

namespace IsogridGenerator.Core.Surfaces
{
    /// <summary>
    /// Grid strategy for conical faces. The cone is unrolled to a flat sector of radius
    /// equal to the slant height. Pocket triangles are in sector coordinates.
    /// The SW layer wraps the sketch using InsertWrapFeature (Deboss).
    /// </summary>
    public class ConicalStrategy : ISurfaceStrategy
    {
        private readonly double _halfAngle;  // cone half-angle (radians)
        private readonly double _slantHeight; // metres

        public ConicalStrategy(double halfAngleRad, double slantHeight)
        {
            _halfAngle = halfAngleRad;
            _slantHeight = slantHeight;
        }

        // Arc length of the unrolled sector base = 2π·R·sin(halfAngle)
        // where R = slant height.
        public double UnrolledArcLength => 2.0 * Math.PI * _slantHeight * Math.Sin(_halfAngle);

        public IReadOnlyList<Triangle2D> GeneratePockets(Bounds2D bounds, IsogridParameters p)
        {
            // TODO (Phase 7): Generate triangles in unrolled sector coordinates.
            // For now, approximate with the arc-length rectangle (exact for small half-angles).
            var unrolledBounds = new Bounds2D(UnrolledArcLength, _slantHeight);
            return TriangleGridGenerator.GeneratePockets(
                unrolledBounds.Width, unrolledBounds.Height, p);
        }
    }
}
