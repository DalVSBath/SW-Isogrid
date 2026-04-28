using System;

namespace IsogridGenerator.Core.Parameters
{
    public class IsogridParameters
    {
        /// <summary>Triangle edge length (metres).</summary>
        public double A { get; }
        /// <summary>Rib thickness (metres). Must be less than A/2.</summary>
        public double B { get; }
        /// <summary>Rib depth / pocket cut depth (metres).</summary>
        public double D { get; }
        /// <summary>Residual skin thickness (metres).</summary>
        public double T { get; }
        /// <summary>Corner fillet radius (metres). Must be less than B/2.</summary>
        public double R { get; }

        public IsogridParameters(double a, double b, double d, double t, double r)
        {
            A = a; B = b; D = d; T = t; R = r;
        }

        public double RowHeight => A * Math.Sqrt(3.0) / 2.0;

        // Scale factor applied to each pocket triangle (inset by B/2 from each edge).
        // Derived from equilateral triangle inradius = A/(2√3):
        //   new_inradius = A/(2√3) - B/2  →  scale = 1 - B·√3/A
        public double PocketScaleFactor => 1.0 - (B * Math.Sqrt(3.0)) / A;

        public static IsogridParameters FromMillimetres(
            double aMm, double bMm, double dMm, double tMm, double rMm)
            => new IsogridParameters(
                aMm * 1e-3, bMm * 1e-3, dMm * 1e-3, tMm * 1e-3, rMm * 1e-3);
    }
}
