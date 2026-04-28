using SolidWorks.Interop.sldworks;
using IsogridGenerator.Core.Parameters;

namespace IsogridGenerator.UI
{
    /// <summary>
    /// Holds the raw values collected from the PropertyManagerPage (millimetres)
    /// and the face the user selected. Convert to SI before calling the orchestrator.
    /// </summary>
    public class IsogridInputs
    {
        // All dimensions in millimetres as entered by the user.
        public double A_mm { get; set; } = 20.0;   // triangle edge length
        public double B_mm { get; set; } =  2.0;   // rib thickness
        public double D_mm { get; set; } =  5.0;   // rib depth
        public double T_mm { get; set; } =  2.0;   // skin thickness
        public double R_mm { get; set; } =  0.5;   // corner fillet radius

        public IFace2? SelectedFace { get; set; }

        public IsogridParameters ToSiParameters() =>
            IsogridParameters.FromMillimetres(A_mm, B_mm, D_mm, T_mm, R_mm);

        public string? Validate()
        {
            if (SelectedFace == null)        return "Select a face before clicking OK.";
            if (A_mm <= 0)                   return "Triangle edge length A must be > 0.";
            if (B_mm <= 0)                   return "Rib thickness B must be > 0.";
            if (D_mm <= 0)                   return "Rib depth D must be > 0.";
            if (T_mm <= 0)                   return "Skin thickness T must be > 0.";
            if (R_mm < 0)                    return "Corner fillet radius R must be ≥ 0.";
            if (B_mm >= A_mm / 2.0)          return $"Rib thickness B must be < A/2 = {A_mm/2:F2} mm.";
            if (R_mm >= B_mm / 2.0 && R_mm > 0)
                return $"Corner fillet radius R must be < B/2 = {B_mm/2:F2} mm.";
            return null;  // valid
        }
    }
}
