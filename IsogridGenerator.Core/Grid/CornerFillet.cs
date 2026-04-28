namespace IsogridGenerator.Core.Grid
{
    /// <summary>
    /// Geometric description of a single rounded corner on an inset pocket triangle.
    ///
    /// At each 60° vertex of an equilateral pocket, a circular arc of radius <see cref="Radius"/>
    /// replaces the sharp corner.  The arc is fully determined by two tangent points (where the
    /// arc meets the adjacent straight edges) and the arc centre:
    ///
    ///   Tangent length from vertex = r / tan(30°) = r√3
    ///   Arc centre distance from vertex = r / sin(30°) = 2r
    ///   Arc centre distance from each adjacent edge (perpendicular) = r   (by definition)
    ///
    /// All coordinates are in the same 2-D metre space as <see cref="Triangle2D"/>.
    /// </summary>
    public readonly struct CornerFillet
    {
        /// <summary>
        /// Where the arc meets the first adjacent edge (toward the "next" vertex in winding order).
        /// </summary>
        public GridPoint TangentA { get; }

        /// <summary>
        /// Where the arc meets the second adjacent edge (toward the "previous" vertex in winding order).
        /// </summary>
        public GridPoint TangentB { get; }

        /// <summary>Centre of the circular fillet arc.</summary>
        public GridPoint ArcCenter { get; }

        /// <summary>Fillet radius in metres.</summary>
        public double Radius { get; }

        /// <summary>
        /// <c>true</c> when <see cref="Radius"/> is zero — no arc entity is needed and
        /// <see cref="TangentA"/> / <see cref="TangentB"/> coincide with the vertex itself.
        /// </summary>
        public bool IsDegenerate => Radius <= 0.0;

        public CornerFillet(GridPoint tangentA, GridPoint tangentB, GridPoint arcCenter, double radius)
        {
            TangentA  = tangentA;
            TangentB  = tangentB;
            ArcCenter = arcCenter;
            Radius    = radius;
        }

        public override string ToString() =>
            $"CornerFillet(r={Radius * 1e3:F3} mm, A={TangentA}, B={TangentB}, C={ArcCenter})";
    }
}
