namespace IsogridGenerator.Core.Surfaces
{
    /// <summary>
    /// Rectangular 2D region in which to generate a grid (all values in metres).
    ///
    /// <see cref="Width"/> and <see cref="Height"/> define the region size in local
    /// sketch / surface coordinates.  <see cref="OffsetX"/> and <see cref="OffsetY"/>
    /// are the sketch-space origin of that region — i.e. the bottom-left corner of the
    /// face expressed in the face's UV / sketch coordinate system.
    ///
    /// For a planar face these come directly from <c>IFace2.GetUVBounds()</c>, which
    /// returns physical distances (metres) along the face's local U and V axes.
    /// Those axes are the same as the SolidWorks sketch axes when a sketch is opened
    /// on that face, so adding OffsetX / OffsetY to every drawn coordinate places the
    /// grid exactly over the face in the sketch.
    /// </summary>
    public class Bounds2D
    {
        public double Width   { get; }
        public double Height  { get; }

        /// <summary>
        /// Sketch-space X (U) coordinate of the bottom-left corner of the face region.
        /// Add this to all generated triangle X coordinates before drawing in the sketch.
        /// </summary>
        public double OffsetX { get; }

        /// <summary>
        /// Sketch-space Y (V) coordinate of the bottom-left corner of the face region.
        /// Add this to all generated triangle Y coordinates before drawing in the sketch.
        /// </summary>
        public double OffsetY { get; }

        public Bounds2D(double width, double height,
                        double offsetX = 0.0, double offsetY = 0.0)
        {
            Width   = width;
            Height  = height;
            OffsetX = offsetX;
            OffsetY = offsetY;
        }
    }
}
