using System.Collections.Generic;
using IsogridGenerator.Core.Grid;
using IsogridGenerator.Core.Parameters;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace IsogridGenerator.SW
{
    /// <summary>
    /// Translates Core geometry (Triangle2D lists) into SolidWorks sketch entities.
    ///
    /// Coordinate convention: Core geometry is in 2D (X, Y) starting from (0, 0).
    /// The <paramref name="offsetX"/> / <paramref name="offsetY"/> parameters shift
    /// each coordinate to the face's position in sketch space (from UV parameterisation).
    /// All values are in metres — SW API is SI throughout.
    /// </summary>
    public class SketchBuilder
    {
        private readonly IModelDoc2 _doc;

        public SketchBuilder(IModelDoc2 doc) { _doc = doc; }

        /// <summary>
        /// Opens a sketch on the given face, draws all pocket triangles, adds fillets,
        /// and closes the sketch. Returns true on success.
        ///
        /// <paramref name="offsetX"/> and <paramref name="offsetY"/> are the face's UV
        /// origin in sketch space (from <c>IFace2.GetUVBounds()</c>).  Adding them to
        /// every generated coordinate places the grid exactly on the face regardless of
        /// where the face sits in the model's coordinate system.
        ///
        /// Caller is responsible for wrapping this in an undo step and disabling/re-enabling
        /// graphics updates.
        /// </summary>
        public IFeature? DrawPocketsOnFace(
            IFace2 face,
            IReadOnlyList<Triangle2D> pockets,
            IsogridParameters p,
            double offsetX = 0.0,
            double offsetY = 0.0)
        {
            // Select the face so SW knows where to attach the new sketch.
            // IFace2 implements IEntity; Select2(append=false, mark=0) is the
            // standard programmatic selection call. This must happen BEFORE InsertSketch2.
            _doc.ClearSelection2(true);
            var entity = (IEntity)face;
            entity.Select2(false, 0);

            _doc.InsertSketch2(true);
            var sm = _doc.SketchManager;

            // Batch all lines first, then all fillets (reduces SW recalculation passes).
            DrawAllLines(sm, pockets, offsetX, offsetY);

            if (p.R > 0)
                ApplyFillets(sm, p.R);

            // Capture the sketch feature BEFORE closing — ActiveSketch becomes null
            // after InsertSketch2(false). ISketch and IFeature share the same COM object.
            var sketchFeature = _doc.SketchManager.ActiveSketch as IFeature;

            _doc.InsertSketch2(false);  // close sketch
            return sketchFeature;
        }

        private static void DrawAllLines(
            ISketchManager sm,
            IReadOnlyList<Triangle2D> pockets,
            double offsetX,
            double offsetY)
        {
            sm.AddToDB = true;  // batch mode — suppress intermediate regenerations

            foreach (var tri in pockets)
            {
                // Draw 3 edges as individual line segments.
                // CreateLine: (x1,y1,z1, x2,y2,z2) — z=0 for sketch-plane entities.
                // offsetX/Y shift from [0,W]×[0,H] space to face-local UV space so the
                // grid lands on the selected face rather than at the sketch origin.
                sm.CreateLine(tri.V0.X + offsetX, tri.V0.Y + offsetY, 0,
                              tri.V1.X + offsetX, tri.V1.Y + offsetY, 0);
                sm.CreateLine(tri.V1.X + offsetX, tri.V1.Y + offsetY, 0,
                              tri.V2.X + offsetX, tri.V2.Y + offsetY, 0);
                sm.CreateLine(tri.V2.X + offsetX, tri.V2.Y + offsetY, 0,
                              tri.V0.X + offsetX, tri.V0.Y + offsetY, 0);
            }

            sm.AddToDB = false;
        }

        private static void ApplyFillets(ISketchManager sm, double radius)
        {
            // TODO (Phase 6): Select vertex pairs and call sm.CreateFillet(radius, options).
            // The fillet API expects sketch entities to be selected before calling.
            // Strategy:
            //   foreach vertex shared by two adjacent lines:
            //     select both lines near that vertex
            //     sm.CreateFillet(radius, (int)swSketchFilletOptions_e.swSketchFillet_ConstrainAngle)
        }
    }
}
