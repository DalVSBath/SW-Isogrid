using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace IsogridGenerator.SW
{
    /// <summary>
    /// Creates SolidWorks features (cut extrude, wrap, project curve) from an open sketch.
    /// All depth values are in metres.
    /// </summary>
    public class FeatureBuilder
    {
        private readonly IModelDoc2 _doc;

        public FeatureBuilder(IModelDoc2 doc) { _doc = doc; }

        /// <summary>
        /// Cuts the active sketch to depth D using FeatureCut4.
        /// Must be called immediately after closing the sketch.
        /// </summary>
        public IFeature? CutExtrude(double depthMetres, bool flipDirection = true)
        {
            // Full 27-parameter signature (verified from SolidWorks interop):
            // FeatureCut4(SingleDir, Reverse, BothDirections,
            //             Type, Type2, Depth, Depth2,
            //             DraftWhileExtruding, DraftWhileExtruding2,
            //             DraftOutward, DraftOutward2, DraftAngle, DraftAngle2,
            //             OffsetReverse, OffsetReverse2,
            //             TranslateSurface, TranslateSurface2,
            //             Merge, UseFeatScope, UseAutoSelect,
            //             AssemblyFeatureScope, AutoSelectComponents, PropagateFeatureToParts,
            //             T0, StartOffset, FlipStartSurface, OptimizeGeometry)
            //
            // Types 4+5 (both int) come before depths 6+7 (both double).

            var feature = _doc.FeatureManager.FeatureCut4(
                /* 1  SingleDir */              true,
                /* 2  Reverse */                flipDirection,
                /* 3  BothDirections */         false,
                /* 4  Type  (dir-1 end cond) */ (int)swEndConditions_e.swEndCondBlind,
                /* 5  Type2 (dir-2 end cond) */ (int)swEndConditions_e.swEndCondBlind,
                /* 6  Depth  (dir-1) */         depthMetres,
                /* 7  Depth2 (dir-2, unused) */ 0.0,
                /* 8  DraftWhileExtruding */    false,
                /* 9  DraftWhileExtruding2 */   false,
                /* 10 DraftOutward */           false,
                /* 11 DraftOutward2 */          false,
                /* 12 DraftAngle */             0.0,
                /* 13 DraftAngle2 */            0.0,
                /* 14 OffsetReverse */          false,
                /* 15 OffsetReverse2 */         false,
                /* 16 TranslateSurface */       false,
                /* 17 TranslateSurface2 */      false,
                /* 18 Merge */                  true,
                /* 19 UseFeatScope */           false,
                /* 20 UseAutoSelect */          true,
                /* 21 AssemblyFeatureScope */   false,
                /* 22 AutoSelectComponents */   false,
                /* 23 PropagateFeatureToParts */ false,
                /* 24 T0 (start cond: 0=sketch plane) */ 0,
                /* 25 StartOffset */            0.0,
                /* 26 FlipStartSurface */       false,
                /* 27 OptimizeGeometry */       false);

            return feature;
        }

        /// <summary>
        /// Applies a Wrap-Deboss feature to map a flat sketch onto a cylindrical or conical face.
        /// </summary>
        public IFeature? WrapDeboss(IFace2 targetFace, double depthMetres)
        {
            // TODO (Phase 7): Implement InsertWrapFeature.
            // The active sketch must cover a width of 2πR for cylinders.
            //
            // _doc.FeatureManager.InsertWrapFeature(
            //     WrapType:  (int)swWrapType_e.swWrapDeboss,
            //     Thickness: depthMetres,
            //     ...);
            return null;
        }

        /// <summary>
        /// Projects the active sketch onto the target face for the freeform strategy.
        /// Shows a warning first because projection does not preserve arc lengths.
        /// </summary>
        public IFeature? ProjectAndCut(IFace2 targetFace, double depthMetres)
        {
            var result = MessageBox.Show(
                "Freeform surface detected.\n\n" +
                "Triangle sizes will not be uniform due to projection distortion " +
                "in high-curvature regions.\n\n" +
                "Continue?",
                "Isogrid Generator — Freeform Warning",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return null;

            // TODO (Phase 8):
            // 1. Project sketch onto face via InsertProjectCurveFeature
            // 2. Cut with OffsetFromSurface end condition at depth D
            return null;
        }
    }
}
