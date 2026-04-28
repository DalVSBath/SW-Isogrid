using System;
using System.Linq;
using Xunit;
using IsogridGenerator.Core.Grid;
using IsogridGenerator.Core.Parameters;

namespace IsogridGenerator.Tests
{
    public class TriangleGridGeneratorTests
    {
        // Default parameters: a=10mm, b=1mm, d=5mm, t=2mm, r=0.3mm (all in metres)
        private static IsogridParameters P(
            double a = 0.010, double b = 0.001, double d = 0.005,
            double t = 0.002, double r = 0.0003)
            => new IsogridParameters(a, b, d, t, r);

        [Fact]
        public void GeneratePockets_100x100_With_a10mm_Returns_Reasonable_Count()
        {
            var pockets = TriangleGridGenerator.GeneratePockets(0.100, 0.100, P());
            // Theoretical: ~4WH/(a²√3) ≈ 231 for a 100×100 mm region with a=10 mm.
            // With centroid-in-bounds filter, boundary triangles are excluded,
            // so actual count is somewhat lower.
            Assert.InRange(pockets.Count, 150, 350);
        }

        [Fact]
        public void AllPockets_AreEquilateral()
        {
            var pockets = TriangleGridGenerator.GeneratePockets(0.050, 0.050, P());
            Assert.All(pockets, tri =>
                Assert.True(tri.IsEquilateral(toleranceDeg: 0.001),
                    $"Non-equilateral triangle: {tri}"));
        }

        [Fact]
        public void AllPocketEdges_HaveExpectedLength()
        {
            var p       = P(a: 0.010, b: 0.001);
            var pockets = TriangleGridGenerator.GeneratePockets(0.050, 0.050, p);

            double expectedEdge = p.A * p.PocketScaleFactor;

            Assert.All(pockets, tri =>
            {
                AssertClose(expectedEdge, tri.EdgeLength01, "V0-V1");
                AssertClose(expectedEdge, tri.EdgeLength12, "V1-V2");
                AssertClose(expectedEdge, tri.EdgeLength20, "V2-V0");
            });
        }

        [Fact]
        public void AllInteriorAngles_Are60Degrees()
        {
            var pockets = TriangleGridGenerator.GeneratePockets(0.050, 0.050, P());
            double expected = Math.PI / 3.0;

            Assert.All(pockets, tri =>
            {
                AssertClose(expected, Triangle2D.InteriorAngle(tri.V0, tri.V1, tri.V2), "angle at V0");
                AssertClose(expected, Triangle2D.InteriorAngle(tri.V1, tri.V2, tri.V0), "angle at V1");
                AssertClose(expected, Triangle2D.InteriorAngle(tri.V2, tri.V0, tri.V1), "angle at V2");
            });
        }

        [Fact]
        public void ZeroRib_ProducesFullSizeTriangles()
        {
            var p       = P(b: 0.0);
            var pockets = TriangleGridGenerator.GeneratePockets(0.030, 0.030, p);

            Assert.All(pockets, tri =>
                AssertClose(p.A, tri.EdgeLength01, "edge with b=0"));
        }

        [Fact]
        public void RibTooLarge_Throws()
        {
            // b > A/√3 collapses the pocket to negative size.
            var p = P(a: 0.010, b: 0.020);
            Assert.Throws<ArgumentException>(() =>
                TriangleGridGenerator.GeneratePockets(0.050, 0.050, p));
        }

        [Fact]
        public void AllPocketCentroids_AreWithinBounds()
        {
            double w = 0.080, h = 0.060;
            var pockets = TriangleGridGenerator.GeneratePockets(w, h, P());

            Assert.All(pockets, tri =>
            {
                var c = tri.Centroid;
                Assert.True(c.X >= 0 && c.X <= w, $"Centroid X={c.X:G4} outside [0,{w}]");
                Assert.True(c.Y >= 0 && c.Y <= h, $"Centroid Y={c.Y:G4} outside [0,{h}]");
            });
        }

        [Fact]
        public void LargerRegion_ProducesMorePockets()
        {
            var small = TriangleGridGenerator.GeneratePockets(0.050, 0.050, P());
            var large = TriangleGridGenerator.GeneratePockets(0.100, 0.100, P());
            Assert.True(large.Count > small.Count,
                $"Expected more pockets for larger region: {large.Count} vs {small.Count}");
        }

        [Fact]
        public void SmallerTriangle_ProducesMorePocketsInSameRegion()
        {
            var coarse = TriangleGridGenerator.GeneratePockets(0.100, 0.100, P(a: 0.020));
            var fine   = TriangleGridGenerator.GeneratePockets(0.100, 0.100, P(a: 0.010));
            Assert.True(fine.Count > coarse.Count,
                $"Finer grid should have more pockets: {fine.Count} vs {coarse.Count}");
        }

        private static void AssertClose(double expected, double actual, string label,
            double tol = 1e-9)
        {
            Assert.True(Math.Abs(expected - actual) < tol,
                $"{label}: expected {expected:G10}, got {actual:G10}, diff={Math.Abs(expected-actual):G3}");
        }
    }
}
