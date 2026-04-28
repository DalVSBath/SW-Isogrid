using System;
using IsogridGenerator.Core.Grid;
using IsogridGenerator.Core.Parameters;
using IsogridGenerator.Core.Surfaces;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace IsogridGenerator.SW
{
    /// <summary>
    /// Top-level coordinator: identifies the surface, picks a strategy, disables graphics
    /// for speed, drives SketchBuilder + FeatureBuilder, and wraps everything in one undo step.
    /// </summary>
    public class IsogridOrchestrator
    {
        /// <summary>
        /// Generates the pocket grid and draws it as a sketch on the face, but does not
        /// create a cut feature. The returned sketch feature shows the "area to be cut"
        /// as yellow contour lines — fast, non-destructive preview.
        /// </summary>
        public IFeature? RunSketchOnly(IModelDoc2 doc, IFace2 face, IsogridParameters p)
        {
            var surfaceType = FaceAnalyzer.Identify(face);
            var bounds      = GetBounds(face, surfaceType);
            var strategy    = CreateStrategy(surfaceType, face);
            var pockets     = strategy.GeneratePockets(bounds, p);

            doc.ClearSelection2(true);

            var view = doc.ActiveView as IModelView;
            if (view != null) view.EnableGraphicsUpdate = false;
            doc.FeatureManager.EnableFeatureTree       = false;
            doc.FeatureManager.EnableFeatureTreeWindow = false;

            IFeature? sketchFeature = null;
            try
            {
                var sketchBuilder = new SketchBuilder(doc);
                sketchFeature = sketchBuilder.DrawPocketsOnFace(face, pockets, p, bounds.OffsetX, bounds.OffsetY);
            }
            finally
            {
                doc.FeatureManager.EnableFeatureTree       = true;
                doc.FeatureManager.EnableFeatureTreeWindow = true;
                if (view != null) view.EnableGraphicsUpdate = true;
                doc.GraphicsRedraw2();
            }

            return sketchFeature;
        }

        public IFeature? Run(IModelDoc2 doc, IFace2 face, IsogridParameters p)
        {
            var surfaceType = FaceAnalyzer.Identify(face);
            var bounds      = GetBounds(face, surfaceType);
            var strategy    = CreateStrategy(surfaceType, face);
            var pockets     = strategy.GeneratePockets(bounds, p);

            doc.ClearSelection2(true);

            var view = doc.ActiveView as IModelView;

            // Disable graphics and feature tree updates for speed.
            // Always re-enable in finally — a crash must not leave SW in a broken state.
            if (view != null) view.EnableGraphicsUpdate = false;
            doc.FeatureManager.EnableFeatureTree        = false;
            doc.FeatureManager.EnableFeatureTreeWindow  = false;

            try
            {
                var sketchBuilder  = new SketchBuilder(doc);
                var featureBuilder = new FeatureBuilder(doc);

                // Pass the UV origin offset so the grid lands on the face in sketch space.
                sketchBuilder.DrawPocketsOnFace(face, pockets, p, bounds.OffsetX, bounds.OffsetY);
                return BuildFeature(featureBuilder, face, surfaceType, p);
            }
            finally
            {
                doc.FeatureManager.EnableFeatureTree       = true;
                doc.FeatureManager.EnableFeatureTreeWindow = true;
                if (view != null) view.EnableGraphicsUpdate = true;
                doc.GraphicsRedraw2();
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        private static Bounds2D GetBounds(IFace2 face, SurfaceType surfaceType)
        {
            switch (surfaceType)
            {
                case SurfaceType.Cylindrical:
                {
                    // For a cylinder, unroll the surface: width = circumference, height = axis length.
                    double R = FaceAnalyzer.GetCylinderRadius(face);
                    var (minX, minY, minZ, maxX, maxY, maxZ) = FaceAnalyzer.GetBoundingBox(face);
                    double H = Math.Sqrt(
                        Math.Pow(maxX - minX, 2) +
                        Math.Pow(maxY - minY, 2) +
                        Math.Pow(maxZ - minZ, 2));
                    // Offset is 0,0 because the unrolled cylinder starts at the seam.
                    return new Bounds2D(2 * Math.PI * R, H);
                }

                default:
                {
                    // For planar (and freeform as a best-effort) faces, use the face's own
                    // UV parameterisation.  For a planar surface, U and V are physical distances
                    // (metres) along the face's local axes — the same axes the SolidWorks sketch
                    // manager uses when a sketch is opened on that face.
                    //
                    // GetUVBounds → [uMin, uMax, vMin, vMax]
                    //
                    // The OffsetX / OffsetY carry the UV origin so that SketchBuilder can shift
                    // every drawn triangle by (uMin, vMin), placing the grid exactly on the face.
                    double[] uv   = (double[])face.GetUVBounds();
                    double uMin   = uv[0], uMax = uv[1];
                    double vMin   = uv[2], vMax = uv[3];
                    return new Bounds2D(uMax - uMin, vMax - vMin, uMin, vMin);
                }
            }
        }

        private static ISurfaceStrategy CreateStrategy(SurfaceType surfaceType, IFace2 face)
        {
            switch (surfaceType)
            {
                case SurfaceType.Planar:
                    return new PlanarStrategy();

                case SurfaceType.Cylindrical:
                    double R = FaceAnalyzer.GetCylinderRadius(face);
                    return new CylindricalStrategy(R);

                case SurfaceType.Conical:
                    // TODO (Phase 7): Extract half-angle and slant height from IFace2.
                    return new ConicalStrategy(halfAngleRad: Math.PI / 6, slantHeight: 0.1);

                case SurfaceType.Spherical:
                case SurfaceType.Freeform:
                default:
                    return new FreeformProjectionStrategy();
            }
        }

        private static IFeature? BuildFeature(
            FeatureBuilder fb, IFace2 face, SurfaceType surfaceType, IsogridParameters p)
        {
            IFeature? feature = null;

            switch (surfaceType)
            {
                case SurfaceType.Planar:
                    feature = fb.CutExtrude(p.D);
                    break;

                case SurfaceType.Cylindrical:
                case SurfaceType.Conical:
                    feature = fb.WrapDeboss(face, p.D);
                    break;

                default:
                    feature = fb.ProjectAndCut(face, p.D);
                    break;
            }

            // Name the feature so it appears as a single labelled entry in the tree.
            if (feature != null)
                feature.Name = "Isogrid Generator";

            return feature;
        }
    }
}
