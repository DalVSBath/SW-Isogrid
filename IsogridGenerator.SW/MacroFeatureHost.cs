using IsogridGenerator.Core.Parameters;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swpublished;
using System;

namespace IsogridGenerator.SW
{
    /// <summary>
    /// Implements the SolidWorks macro feature callbacks so the isogrid appears as a single
    /// re-editable feature ("Isogrid1") in the feature tree rather than hundreds of raw cuts.
    ///
    /// TODO (Phase 9): Wire this into IsogridOrchestrator.Run by calling
    ///   IFeatureManager.InsertMacroFeature3 instead of driving SketchBuilder directly.
    ///
    /// Callback contract (ISwComFeature):
    ///   Regenerate — re-runs generation from stored parameters.
    ///   Edit       — re-opens the PropertyManagerPage with current values.
    ///   Security   — returns allowed operation bitmask.
    /// </summary>
    public class MacroFeatureHost : ISwComFeature
    {
        private const string ParamA = "A";
        private const string ParamB = "B";
        private const string ParamD = "D";
        private const string ParamT = "T";
        private const string ParamR = "R";

        public object Regenerate(ISldWorks app, IModelDoc2 doc, IFeature feature)
        {
            try
            {
                var data = (IMacroFeatureData)feature.GetDefinition();
                var p = LoadParameters(data);

                // TODO: Retrieve the stored face selection from macro feature data.
                // data.GetSelections3(out object[] selObjs, out int[] selMarks, out int[] views, out object[] compXforms);
                // var face = selObjs[0] as IFace2;

                // var orchestrator = new IsogridOrchestrator();
                // orchestrator.Run(doc, face, p);

                return null!;  // null = success
            }
            catch (Exception ex)
            {
                // Returning a non-null string shows it in the feature error balloon.
                return $"Isogrid regeneration failed: {ex.Message}";
            }
        }

        public object Edit(ISldWorks app, IModelDoc2 doc, IFeature feature)
        {
            // TODO: Deserialize stored parameters, pre-populate PMP, show it.
            // var data = (IMacroFeatureData)feature.GetDefinition();
            // var p    = LoadParameters(data);
            // var pmp  = new IsogridPropertyManagerPage(app, doc);
            // pmp.ShowWithExistingValues(p);
            return true;
        }

        public object Security(ISldWorks app, IModelDoc2 doc, IFeature feature)
        {
            return 0;  // 0 = default security, all operations allowed
        }

        private static IsogridParameters LoadParameters(IMacroFeatureData data)
        {
            // COM out-params come back as plain object; cast after the call.
            data.GetParameters(out object namesObj, out object _, out object valuesObj);
            var names  = (string[])namesObj;
            var values = (object[])valuesObj;

            double Get(string key)
            {
                for (int i = 0; i < names.Length; i++)
                    if (names[i] == key) return Convert.ToDouble(values[i]);
                throw new InvalidOperationException($"Macro feature parameter '{key}' not found.");
            }

            return new IsogridParameters(Get(ParamA), Get(ParamB), Get(ParamD), Get(ParamT), Get(ParamR));
        }

        public static (string[] names, object[] values) SerializeParameters(IsogridParameters p)
        {
            return (
                new[] { ParamA, ParamB, ParamD, ParamT, ParamR },
                new object[] { p.A, p.B, p.D, p.T, p.R });
        }

        public object Edit(object app, object modelDoc, object feature)
        {
            return Edit((ISldWorks)app, (IModelDoc2)modelDoc, (IFeature)feature);
        }

        public object Regenerate(object app, object modelDoc, object feature)
        {
            return Regenerate((ISldWorks)app, (IModelDoc2)modelDoc, (IFeature)feature);
        }

        public object Security(object app, object modelDoc, object feature)
        {
            return Security((ISldWorks)app, (IModelDoc2)modelDoc, (IFeature)feature);
        }
    }
}
