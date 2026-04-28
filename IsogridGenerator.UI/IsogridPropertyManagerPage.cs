using System;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using IsogridGenerator.SW;
using IsogridGenerator.Core.Grid;

namespace IsogridGenerator.UI
{
    /// <summary>
    /// SolidWorks PropertyManagerPage that collects isogrid parameters from the user.
    ///
    /// UX flow
    /// -------
    ///   1. User opens the page and selects a planar face.
    ///   2. User clicks "Preview Grid" — a read-only sketch is drawn on the face so the
    ///      grid can be reviewed in the viewport.  The sketch is deleted automatically
    ///      when the page closes (OK or Cancel).
    ///   3. User adjusts parameters and can re-click Preview at any time.
    ///   4. OK → preview sketch removed, final sketch + cut feature created.
    ///   5. Cancel → preview sketch removed, no feature created.
    ///
    /// Preview implementation
    /// ----------------------
    ///   DrawPreview() creates a SolidWorks sketch directly on the selected face using
    ///   the same UV-offset + pocket logic used by the final cut, then closes the sketch
    ///   so it appears as a visible overlay in the model tree (named "IsogridPreview").
    ///   ClearPreview() removes it via EditUndo so it leaves no trace.
    /// </summary>
    public class IsogridPropertyManagerPage : IPropertyManagerPage2Handler9
    {
        // ── Control IDs (must be unique within the page) ──────────────────────
        private const int IdFaceSelection  = 20;
        private const int IdBoxA           = 40;
        private const int IdBoxB           = 41;
        private const int IdBoxD           = 42;
        private const int IdBoxT           = 43;
        private const int IdBoxR           = 44;
        private const int IdPreviewButton  = 50;

        private IPropertyManagerPage2?       _page;
        private IPropertyManagerPageSelectionbox? _faceSelection;
        private IPropertyManagerPageNumberbox?    _boxA, _boxB, _boxD, _boxT, _boxR;

        // Set to true in OnClose so the selection-box clear that SolidWorks triggers
        // during its own closing sequence does not wipe _inputs.SelectedFace before
        // HandleOk() has a chance to read it.
        private bool _isClosing;

        // Real cut feature shown as the preview.  Null when no preview is active.
        // On OK the reference is nulled (feature kept).  On Cancel or re-preview the
        // feature is deleted from the tree.
        private IFeature? _previewFeature;

        private readonly ISldWorks   _swApp;
        private readonly IModelDoc2  _doc;
        private readonly IsogridInputs _inputs = new IsogridInputs();

        public IsogridPropertyManagerPage(ISldWorks swApp, IModelDoc2 doc)
        {
            _swApp = swApp;
            _doc   = doc;
        }

        public void Show()
        {
            int errors = 0;
            // swPropertyManagerOptions_OkButton = 1, swPropertyManagerOptions_CancelButton = 2
            _page = (IPropertyManagerPage2)_swApp.CreatePropertyManagerPage(
                "Isogrid Generator",
                1 | 2,
                this,
                ref errors);

            if (_page == null || errors != 0)
            {
                MessageBox.Show("Failed to create PropertyManagerPage.");
                return;
            }

            BuildControls();
            _page.Show2(0);
        }

        private void BuildControls()
        {
            if (_page == null) return;

            int enabled = (int)swAddControlOptions_e.swControlOptions_Enabled
                        | (int)swAddControlOptions_e.swControlOptions_Visible;
            short indent = (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;

            // ── Selection box ────────────────────────────────────────────────────
            _faceSelection = (IPropertyManagerPageSelectionbox)_page.AddControl2(
                IdFaceSelection,
                (short)swPropertyManagerPageControlType_e.swControlType_Selectionbox,
                "Select face",
                indent,
                enabled,
                "Click here, then select a face in the viewport");
            _faceSelection.Height = 50;
            _faceSelection.SetSelectionFilters(new int[] { (int)swSelectType_e.swSelFACES });
            _faceSelection.SingleEntityOnly = true;

            // ── Number boxes (values entered in mm) ──────────────────────────────
            _boxA = AddNumberBox(IdBoxA,
                "Edge length A (mm)",
                "Equilateral triangle edge length. Typical range: 10\u201350 mm.",
                _inputs.A_mm, 0.5, 500.0);

            _boxB = AddNumberBox(IdBoxB,
                "Rib thickness B (mm)",
                "Width of each rib separating triangles. Must be < A / 2.",
                _inputs.B_mm, 0.1, 100.0);

            _boxD = AddNumberBox(IdBoxD,
                "Rib depth D (mm)",
                "Pocket cut depth / rib height.",
                _inputs.D_mm, 0.1, 100.0);

            _boxT = AddNumberBox(IdBoxT,
                "Skin thickness T (mm)",
                "Residual material thickness at the base of each pocket.",
                _inputs.T_mm, 0.1, 100.0);

            _boxR = AddNumberBox(IdBoxR,
                "Corner fillet R (mm)",
                "Internal fillet radius at triangle corners. Enter 0 for sharp corners.",
                _inputs.R_mm, 0.0, 50.0);

            // ── Preview button ────────────────────────────────────────────────────
            _page.AddControl2(
                IdPreviewButton,
                (short)swPropertyManagerPageControlType_e.swControlType_Button,
                "Preview Grid",
                indent,
                enabled,
                "Draw the isogrid sketch on the selected face so you can review the layout " +
                "before committing the cut. Click again after changing parameters to refresh.");
        }

        private IPropertyManagerPageNumberbox AddNumberBox(
            int id, string caption, string tip,
            double defaultValue, double min, double max)
        {
            int enabled = (int)swAddControlOptions_e.swControlOptions_Enabled
                        | (int)swAddControlOptions_e.swControlOptions_Visible;
            short indent = (short)swPropertyManagerPageControlLeftAlign_e.swControlAlign_Indent;

            var box = (IPropertyManagerPageNumberbox)_page!.AddControl2(
                id,
                (short)swPropertyManagerPageControlType_e.swControlType_Numberbox,
                caption,
                indent,
                enabled,
                tip);

            box.SetRange2(
                (short)swNumberboxUnitType_e.swNumberBox_UnitlessDouble,
                min, max,
                true,   // Inclusive — clamp to [Min, Max]
                0.1,    // normal scroll increment (mm)
                1.0,    // fast scroll increment (mm)
                0.01);  // slow scroll increment (mm)
            box.Value = defaultValue;

            return box;
        }

        // ── Preview helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Builds a temporary wire body from the isogrid pocket edges and displays it
        /// in the viewport using the same orange-yellow colour SolidWorks uses for its
        /// own feature previews.  The body lives only in memory — it has no feature-tree
        /// entry and does not dirty the undo stack.
        /// </summary>
        private void DrawPreview()
        {
            ClearPreview();

            if (_inputs.SelectedFace == null) return;

            SyncInputsFromBoxes();

            var error = _inputs.Validate();
            if (error != null) return;   // silently skip preview on invalid input

            try
            {
                var orchestrator = new IsogridOrchestrator();
                _previewFeature  = orchestrator.RunSketchOnly(_doc, _inputs.SelectedFace, _inputs.ToSiParameters());

                if (_previewFeature != null)
                {
                    // Rename so the user can see it's a preview.  On OK we rename back
                    // to "Isogrid Generator".
                    _previewFeature.Name = "Isogrid Generator (Preview)";
                    _doc.ClearSelection2(true);
                    _doc.GraphicsRedraw2();
                }
            }
            catch
            {
                // On any error skip the preview — do not disturb the PMP.
                _previewFeature = null;
            }
        }

        /// <summary>
        /// Deletes the preview feature (and its absorbed sketch) from the tree.
        /// Safe to call when no preview is active.
        /// </summary>
        private void ClearPreview()
        {
            if (_previewFeature == null) return;
            try
            {
                _doc.ClearSelection2(true);
                if (_previewFeature.Select2(false, 0))
                    _doc.Extension.DeleteSelection2(
                        (int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            }
            catch { }
            finally
            {
                _previewFeature = null;
                try { _doc.GraphicsRedraw2(); } catch { }
            }
        }

        // ── IPropertyManagerPage2Handler9 callbacks ──────────────────────────────

        public void AfterActivation() { }

        public void AfterClose() { }

        public bool OnHelp() => false;

        public bool OnNextPage() => false;

        public bool OnPreviousPage() => false;

        public bool OnEnableNextButton() => false;

        public bool OnEnablePreviousButton() => false;

        public void OnClose(int reason)
        {
            // Mark as closing FIRST so OnSelectionboxSelectionChanged's count==0 callback
            // (fired by SolidWorks as it clears the selection box during close) does not
            // null out _inputs.SelectedFace before HandleOk() reads it.
            _isClosing = true;

            if (reason == (int)swPropertyManagerPageCloseReasons_e.swPropertyManagerPageClose_Okay)
                HandleOk();
            else
                ClearPreview();   // Cancel → remove preview feature
        }

        public void OnWhatsNew() { }

        public void OnButtonPress(int id)
        {
            if (id == IdPreviewButton)
                DrawPreview();
        }

        public void OnCheckboxCheck(int id, bool @checked) { }

        public void OnComboboxEditChanged(int id, string text) { }

        public void OnComboboxSelectionChanged(int id, int item) { }

        public void OnGroupCheck(int id, bool @checked) { }

        public void OnGroupExpand(int id, bool expanded) { }

        public bool OnKeystroke(int wparam, int message, int lparam, int id) => false;

        public void OnListboxSelectionChanged(int id, int item) { }

        public void OnNumberboxChanged(int id, double value)
        {
            switch (id)
            {
                case IdBoxA: _inputs.A_mm = value; break;
                case IdBoxB: _inputs.B_mm = value; break;
                case IdBoxD: _inputs.D_mm = value; break;
                case IdBoxT: _inputs.T_mm = value; break;
                case IdBoxR: _inputs.R_mm = value; break;
            }
        }

        public void OnNumberboxTrackingCompleted(int id, double value) { }

        public void OnOptionCheck(int id) { }

        public void OnPopupMenuItem(int id) { }

        public void OnPopupMenuItemUpdate(int id, ref int retval) { }

        public void OnSelectionboxCalloutCreated(int id) { }

        public void OnSelectionboxCalloutDestroyed(int id) { }

        public void OnSelectionboxFocusChanged(int id) { }

        public void OnSelectionboxSelectionChanged(int id, int count)
        {
            UpdateSelectedFaceFromBox(id, count);
        }

        public void OnSliderPositionChanged(int id, double value) { }

        public void OnSliderTrackingCompleted(int id, double value) { }

        public bool OnSubmitSelection(int id, object selection, int selType, ref string itemText) =>
            selType == (int)swSelectType_e.swSelFACES;

        public bool OnTabClicked(int id) => false;

        public void OnTextboxChanged(int id, string text) { }

        public void OnUndo() { }

        public void OnRedo() { }

        public void OnListboxRMBUp(int id, int posX, int posY) { }

        public void OnNumberBoxTrackingCompleted(int id, double value) { }

        public int  OnActiveXControlCreated(int id, bool status) => 0;

        public void OnGainedFocus(int id) { }

        public void OnLostFocus(int id) { }

        public bool OnPreview() => false;

        public void OnSelectionboxListChanged(int id, int count)
        {
            UpdateSelectedFaceFromBox(id, count);
        }

        public int  OnWindowFromHandleControlCreated(int id, bool status) => 0;

        // ─────────────────────────────────────────────────────────────────────

        private void UpdateSelectedFaceFromBox(int id, int count)
        {
            if (id != IdFaceSelection) return;

            if (count > 0)
            {
                var selMgr = (ISelectionMgr)_doc.SelectionManager;
                int total = selMgr.GetSelectedObjectCount2(-1);
                for (int i = 1; i <= total; i++)
                {
                    if (selMgr.GetSelectedObject6(i, -1) is IFace2 face)
                    {
                        _inputs.SelectedFace = face;
                        return;
                    }
                }
            }
            else if (!_isClosing)
            {
                _inputs.SelectedFace = null;
                ClearPreview();
            }
        }

        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Copies the current control values into <see cref="_inputs"/>.</summary>
        private void SyncInputsFromBoxes()
        {
            if (_boxA != null) _inputs.A_mm = _boxA.Value;
            if (_boxB != null) _inputs.B_mm = _boxB.Value;
            if (_boxD != null) _inputs.D_mm = _boxD.Value;
            if (_boxT != null) _inputs.T_mm = _boxT.Value;
            if (_boxR != null) _inputs.R_mm = _boxR.Value;
        }

        private void HandleOk()
        {
            SyncInputsFromBoxes();

            // Belt-and-suspenders face read: if the selection-box callbacks didn't fire
            // (or fired before we could capture the face), read directly from the
            // SelectionManager now while the page is still alive.
            if (_inputs.SelectedFace == null)
            {
                try
                {
                    var selMgr = (ISelectionMgr)_doc.SelectionManager;
                    int total = selMgr.GetSelectedObjectCount2(-1);
                    for (int i = 1; i <= total; i++)
                    {
                        if (selMgr.GetSelectedObject6(i, -1) is IFace2 face)
                        {
                            _inputs.SelectedFace = face;
                            break;
                        }
                    }
                }
                catch { }
            }

            var error = _inputs.Validate();
            if (error != null)
            {
                MessageBox.Show(error, "Isogrid Generator \u2014 Validation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // If a preview sketch exists, select it so FeatureCut4 (UseAutoSelect=true)
            // picks it up directly — no need to redraw the sketch from scratch.
            // SolidWorks absorbs the sketch into the cut, so we null the reference
            // without calling ClearPreview() (which would delete an already-consumed sketch).
            if (_previewFeature != null)
            {
                try
                {
                    _doc.ClearSelection2(true);
                    _previewFeature.Select2(false, 0);

                    var fb      = new FeatureBuilder(_doc);
                    var feature = fb.CutExtrude(_inputs.ToSiParameters().D);

                    if (feature != null)
                    {
                        feature.Name    = "Isogrid Generator";
                        _previewFeature = null;
                        return;
                    }
                }
                catch { }

                // Sketch couldn't be reused — clear it and fall through to fresh run.
                ClearPreview();
            }

            try
            {
                var orchestrator = new IsogridOrchestrator();
                orchestrator.Run(_doc, _inputs.SelectedFace!, _inputs.ToSiParameters());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Generation failed:\n{ex.Message}", "Isogrid Generator",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
