using System;
using System.Collections.Generic;
using IsogridGenerator.Core.Grid;
using SolidWorks.Interop.sldworks;

namespace IsogridGenerator.SW
{
    // RGB(255, 165, 0) as Windows COLORREF (R + G*256 + B*65536)
    internal static class PreviewBuilder
    {
        internal const int OrangeColorRef = 255 + 165 * 256;

        private static readonly double[] OrangeMatProps =
            { 1.0, 0.647, 0.0, 0.2, 0.9, 0.3, 0.5, 0.0, 0.0 };

        private const int MaxPreviewTriangles = 75;

        internal static List<IBody2> Build(
            ISldWorks swApp,
            IFace2 face,
            IReadOnlyList<Triangle2D> pockets,
            double offsetX,
            double offsetY)
        {
            var modeler = (IModeler)swApp.GetModeler();
            var surface = (ISurface)face.GetSurface();
            var bodies  = new List<IBody2>(Math.Min(pockets.Count, MaxPreviewTriangles));

            int step = pockets.Count > MaxPreviewTriangles
                ? pockets.Count / MaxPreviewTriangles
                : 1;

            for (int idx = 0; idx < pockets.Count && bodies.Count < MaxPreviewTriangles; idx += step)
            {
                var tri = pockets[idx];
                var p0 = EvalUV(surface, tri.V0.X + offsetX, tri.V0.Y + offsetY);
                var p1 = EvalUV(surface, tri.V1.X + offsetX, tri.V1.Y + offsetY);
                var p2 = EvalUV(surface, tri.V2.X + offsetX, tri.V2.Y + offsetY);
                if (p0 == null || p1 == null || p2 == null) continue;

                var c01 = MakeSeg(modeler, p0, p1);
                var c12 = MakeSeg(modeler, p1, p2);
                var c20 = MakeSeg(modeler, p2, p0);
                if (c01 == null || c12 == null || c20 == null) continue;

                var body = (IBody2?)modeler.CreateWireBody(new Curve[] { c01, c12, c20 }, 0);
                if (body == null) continue;

                body.MaterialPropertyValues2 = OrangeMatProps;
                bodies.Add(body);
            }

            return bodies;
        }

        private static double[]? EvalUV(ISurface surface, double u, double v)
        {
            var r = (double[])surface.Evaluate(u, v, 0, 0);
            return r?.Length >= 3 ? r : null;
        }

        private static Curve? MakeSeg(IModeler modeler, double[] p1, double[] p2)
        {
            double dx = p2[0] - p1[0], dy = p2[1] - p1[1], dz = p2[2] - p1[2];
            if (dx * dx + dy * dy + dz * dz < 1e-20) return null;
            var raw = (ICurve?)modeler.CreateLine(p1, new[] { dx, dy, dz });
            return raw?.CreateTrimmedCurve2(p1[0], p1[1], p1[2], p2[0], p2[1], p2[2]);
        }
    }
}
