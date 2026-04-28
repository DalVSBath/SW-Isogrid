using System;
using Xunit;
using IsogridGenerator.Core.Grid;
using IsogridGenerator.Core.Parameters;

namespace IsogridGenerator.Tests
{
    /// <summary>
    /// Tests for <see cref="Triangle2D.ComputeFillets"/> and the <see cref="CornerFillet"/> struct.
    ///
    /// All geometric assertions are derived from the exact formulae for a 60° corner fillet:
    ///   Tangent length  = r / tan(30°)  = r√3
    ///   Arc centre dist from vertex = r / sin(30°) = 2r
    ///   Arc centre dist from each adjacent edge (perpendicular) = r (by definition)
    /// </summary>
    public class CornerFilletTests
    {
        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a canonical up-triangle (▲) with the given edge length.
        ///   V0 = origin, V1 = (a,0), V2 = (a/2, a·√3/2)
        /// All interior angles are exactly 60°.
        /// </summary>
        private static Triangle2D MakeEquilateral(double edgeLength)
        {
            double h = edgeLength * Math.Sqrt(3.0) / 2.0;
            return new Triangle2D(
                new GridPoint(0,              0),
                new GridPoint(edgeLength,     0),
                new GridPoint(edgeLength / 2, h));
        }

        /// <summary>
        /// Creates the pocket triangle produced by <see cref="TriangleGridGenerator"/> for
        /// the given edge length <paramref name="a"/> and rib thickness <paramref name="b"/>.
        /// </summary>
        private static Triangle2D MakePocket(double a = 0.010, double b = 0.001)
        {
            var p = new IsogridParameters(a, b, 0.005, 0.002, 0.0);
            double scaled = a * p.PocketScaleFactor;
            return MakeEquilateral(scaled);
        }

        /// <summary>Perpendicular distance from point <paramref name="p"/> to the infinite
        /// line passing through <paramref name="a"/> and <paramref name="b"/>.</summary>
        private static double PointToLineDistance(GridPoint p, GridPoint a, GridPoint b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            return Math.Abs((p.X - a.X) * dy - (p.Y - a.Y) * dx) / len;
        }

        /// <summary>Returns true when <paramref name="p"/> lies on the finite segment
        /// <paramref name="a"/> → <paramref name="b"/> within floating-point tolerance.</summary>
        private static bool IsOnSegment(GridPoint p, GridPoint a, GridPoint b, double tol = 1e-9)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            if (len2 < 1e-20) return p.DistanceTo(a) < tol;
            double t     = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
            double projX = a.X + t * dx, projY = a.Y + t * dy;
            return t >= -tol && t <= 1 + tol
                && Math.Abs(p.X - projX) < tol
                && Math.Abs(p.Y - projY) < tol;
        }

        private static void AssertClose(double expected, double actual, string label, double tol = 1e-9)
            => Assert.True(Math.Abs(expected - actual) < tol,
                $"{label}: expected {expected:G12}, got {actual:G12}, diff={Math.Abs(expected - actual):G4}");

        // ── Degenerate case ───────────────────────────────────────────────────────

        [Fact]
        public void FilletRadius_Zero_Returns_Degenerate_Fillets()
        {
            var tri     = MakeEquilateral(0.010);
            var fillets = tri.ComputeFillets(0.0);

            Assert.Equal(3, fillets.Length);
            Assert.All(fillets, f => Assert.True(f.IsDegenerate,
                "r=0 should produce degenerate fillets"));
        }

        [Fact]
        public void FilletRadius_Zero_TangentPoints_CollapseToVertex()
        {
            var tri     = MakeEquilateral(0.010);
            var fillets = tri.ComputeFillets(0.0);

            // TangentA == TangentB == vertex for all three corners.
            var verts = new[] { tri.V0, tri.V1, tri.V2 };
            for (int i = 0; i < 3; i++)
            {
                AssertClose(0, fillets[i].TangentA.DistanceTo(verts[i]), $"TangentA@V{i} collapses");
                AssertClose(0, fillets[i].TangentB.DistanceTo(verts[i]), $"TangentB@V{i} collapses");
            }
        }

        // ── Tangent points ────────────────────────────────────────────────────────

        [Fact]
        public void TangentPoints_AreAtCorrectDistance_FromVertex()
        {
            double r   = 0.0003;
            var tri    = MakePocket();
            var fillets = tri.ComputeFillets(r);

            // For a 60° corner: tangent length = r / tan(30°) = r√3.
            double expectedLen = r / Math.Tan(Math.PI / 6.0);
            var verts = new[] { tri.V0, tri.V1, tri.V2 };

            for (int i = 0; i < 3; i++)
            {
                AssertClose(expectedLen, fillets[i].TangentA.DistanceTo(verts[i]),
                    $"TangentA distance from V{i}");
                AssertClose(expectedLen, fillets[i].TangentB.DistanceTo(verts[i]),
                    $"TangentB distance from V{i}");
            }
        }

        [Fact]
        public void TangentA_LiesOnEdge_ToNextVertex()
        {
            // ComputeFillets[i].TangentA is on the edge from Vi toward V(i+1).
            double r   = 0.0003;
            var tri    = MakePocket();
            var fillets = tri.ComputeFillets(r);

            Assert.True(IsOnSegment(fillets[0].TangentA, tri.V0, tri.V1), "fillet[0].TangentA on V0→V1");
            Assert.True(IsOnSegment(fillets[1].TangentA, tri.V1, tri.V2), "fillet[1].TangentA on V1→V2");
            Assert.True(IsOnSegment(fillets[2].TangentA, tri.V2, tri.V0), "fillet[2].TangentA on V2→V0");
        }

        [Fact]
        public void TangentB_LiesOnEdge_ToPrevVertex()
        {
            // ComputeFillets[i].TangentB is on the edge from Vi toward V(i+2 mod 3).
            double r   = 0.0003;
            var tri    = MakePocket();
            var fillets = tri.ComputeFillets(r);

            Assert.True(IsOnSegment(fillets[0].TangentB, tri.V0, tri.V2), "fillet[0].TangentB on V0→V2");
            Assert.True(IsOnSegment(fillets[1].TangentB, tri.V1, tri.V0), "fillet[1].TangentB on V1→V0");
            Assert.True(IsOnSegment(fillets[2].TangentB, tri.V2, tri.V1), "fillet[2].TangentB on V2→V1");
        }

        // ── Arc centre ────────────────────────────────────────────────────────────

        [Fact]
        public void ArcCenter_IsAtCorrectDistance_FromVertex()
        {
            // For a 60° corner: arc centre distance from vertex = r / sin(30°) = 2r.
            double r   = 0.0003;
            var tri    = MakePocket();
            var fillets = tri.ComputeFillets(r);

            var verts = new[] { tri.V0, tri.V1, tri.V2 };
            for (int i = 0; i < 3; i++)
                AssertClose(2.0 * r, fillets[i].ArcCenter.DistanceTo(verts[i]),
                    $"ArcCenter distance from V{i}");
        }

        [Fact]
        public void ArcCenter_IsAtRadius_FromEachAdjacentEdge()
        {
            // The arc centre must be exactly r from both edges that form the corner.
            double r   = 0.0003;
            var tri    = MakePocket();
            var fillets = tri.ComputeFillets(r);

            // fillet[0] at V0: adjacent edges are V0→V1 and V0→V2.
            AssertClose(r, PointToLineDistance(fillets[0].ArcCenter, tri.V0, tri.V1),
                "fillet[0] centre to edge V0V1");
            AssertClose(r, PointToLineDistance(fillets[0].ArcCenter, tri.V0, tri.V2),
                "fillet[0] centre to edge V0V2");

            // fillet[1] at V1: adjacent edges are V1→V2 and V1→V0.
            AssertClose(r, PointToLineDistance(fillets[1].ArcCenter, tri.V1, tri.V2),
                "fillet[1] centre to edge V1V2");
            AssertClose(r, PointToLineDistance(fillets[1].ArcCenter, tri.V1, tri.V0),
                "fillet[1] centre to edge V1V0");

            // fillet[2] at V2: adjacent edges are V2→V0 and V2→V1.
            AssertClose(r, PointToLineDistance(fillets[2].ArcCenter, tri.V2, tri.V0),
                "fillet[2] centre to edge V2V0");
            AssertClose(r, PointToLineDistance(fillets[2].ArcCenter, tri.V2, tri.V1),
                "fillet[2] centre to edge V2V1");
        }

        [Fact]
        public void ArcCenter_IsAtRadius_FromBothTangentPoints()
        {
            // The arc centre must be exactly r from both tangent points (they lie on the arc).
            double r   = 0.0003;
            var tri    = MakePocket();
            var fillets = tri.ComputeFillets(r);

            foreach (var f in fillets)
            {
                AssertClose(r, f.ArcCenter.DistanceTo(f.TangentA), "centre to TangentA");
                AssertClose(r, f.ArcCenter.DistanceTo(f.TangentB), "centre to TangentB");
            }
        }

        // ── Radius field ──────────────────────────────────────────────────────────

        [Fact]
        public void AllFillets_CarryCorrectRadius()
        {
            double r   = 0.00025;
            var tri    = MakePocket();
            var fillets = tri.ComputeFillets(r);

            Assert.All(fillets, f => AssertClose(r, f.Radius, "Radius field"));
            Assert.All(fillets, f => Assert.False(f.IsDegenerate));
        }

        // ── Scale invariance ──────────────────────────────────────────────────────

        [Fact]
        public void FilletGeometry_ScalesLinearlyWithRadius()
        {
            var tri = MakePocket();
            double r1 = 0.0001, r2 = 0.0002;

            var f1 = tri.ComputeFillets(r1);
            var f2 = tri.ComputeFillets(r2);

            // Tangent lengths and centre distances should double when r doubles.
            double tLen1 = f1[0].TangentA.DistanceTo(tri.V0);
            double tLen2 = f2[0].TangentA.DistanceTo(tri.V0);
            AssertClose(tLen1 * 2, tLen2, "tangent length doubles with r");

            double cDist1 = f1[0].ArcCenter.DistanceTo(tri.V0);
            double cDist2 = f2[0].ArcCenter.DistanceTo(tri.V0);
            AssertClose(cDist1 * 2, cDist2, "centre distance doubles with r");
        }

        // ── Validation ────────────────────────────────────────────────────────────

        [Fact]
        public void NegativeRadius_Throws()
            => Assert.Throws<ArgumentOutOfRangeException>(() =>
                MakeEquilateral(0.010).ComputeFillets(-0.001));

        [Fact]
        public void RadiusTooLarge_Throws()
        {
            var tri = MakePocket();
            // Maximum r before fillets overlap: edgeLen / (2√3).
            double edgeLen = tri.EdgeLength01;
            double maxR    = edgeLen / (2.0 * Math.Sqrt(3.0));

            // Exactly at the limit is allowed (within floating-point tolerance).
            tri.ComputeFillets(maxR);

            // Exceeding the limit must throw.
            Assert.Throws<ArgumentOutOfRangeException>(() => tri.ComputeFillets(maxR + 0.001));
        }

        [Fact]
        public void RadiusAtExactMaximum_TangentPointsMeetAtEdgeMidpoint()
        {
            // When r = maxR, the two fillets on each edge share the edge midpoint —
            // the tangent points from adjacent corners coincide at the centre of the edge.
            var tri    = MakePocket();
            double edgeLen = tri.EdgeLength01;
            double maxR    = edgeLen / (2.0 * Math.Sqrt(3.0));
            var fillets    = tri.ComputeFillets(maxR);

            // fillet[0].TangentA and fillet[1].TangentB both touch edge V0→V1.
            // At max r they should both be at the midpoint of V0→V1.
            var midpoint01 = new GridPoint(
                (tri.V0.X + tri.V1.X) / 2.0,
                (tri.V0.Y + tri.V1.Y) / 2.0);

            AssertClose(0, fillets[0].TangentA.DistanceTo(midpoint01),
                "fillet[0].TangentA at midpoint of V0V1", tol: 1e-9);
            AssertClose(0, fillets[1].TangentB.DistanceTo(midpoint01),
                "fillet[1].TangentB at midpoint of V0V1", tol: 1e-9);
        }
    }
}
