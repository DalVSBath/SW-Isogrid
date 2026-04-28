using SolidWorks.Interop.sldworks;

namespace IsogridGenerator.SW
{
    public static class FaceAnalyzer
    {
        /// <summary>
        /// Identifies the underlying surface type of a SolidWorks face.
        /// Uses ISurface boolean interrogation methods rather than the Identity integer,
        /// which is more readable and version-stable.
        /// </summary>
        public static SurfaceType Identify(IFace2 face)
        {
            var surf = face.GetSurface() as ISurface;
            if (surf == null) return SurfaceType.Freeform;

            if (surf.IsPlane())    return SurfaceType.Planar;
            if (surf.IsCylinder()) return SurfaceType.Cylindrical;
            if (surf.IsCone())     return SurfaceType.Conical;
            if (surf.IsSphere())   return SurfaceType.Spherical;

            return SurfaceType.Freeform;
        }

        /// <summary>
        /// Returns the cylinder radius (metres) from a cylindrical face's surface.
        /// Caller must ensure the face is cylindrical before calling.
        /// </summary>
        public static double GetCylinderRadius(IFace2 face)
        {
            var surf = (ISurface)face.GetSurface();
            // CylinderParams returns: [Ox,Oy,Oz, Ax,Ay,Az, R]
            var p = (double[])surf.CylinderParams;
            return p[6];
        }

        /// <summary>Returns the face bounding box in model coordinates (metres).</summary>
        public static (double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
            GetBoundingBox(IFace2 face)
        {
            var box = (double[])face.GetBox();
            // box: [minX, minY, minZ, maxX, maxY, maxZ]
            return (box[0], box[1], box[2], box[3], box[4], box[5]);
        }
    }
}
