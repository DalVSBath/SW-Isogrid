using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace IsogridGenerator.SW
{
    /// <summary>
    /// Holds a list of temporary wire bodies (one per pocket triangle) and keeps them
    /// visible by re-calling DisplayWireFrameXOR on every BufferSwapNotify.
    /// Disposing unsubscribes the event and forces a redraw so the overlay vanishes.
    /// </summary>
    public sealed class LivePreview : IDisposable
    {
        private List<IBody2>? _bodies;
        private ModelView?    _view;
        private readonly IPartDoc   _partDoc;
        private readonly IModelDoc2 _doc;

        public int BodyCount => _bodies?.Count ?? 0;
        public int EdgeCount => _bodies?[0]?.GetEdgeCount() ?? -1;
        public bool IsTemp   => _bodies?[0]?.IsTemporaryBody() ?? false;

        public LivePreview(List<IBody2> bodies, IModelDoc2 doc)
        {
            _bodies  = bodies;
            _doc     = doc;
            _partDoc = (IPartDoc)doc;
            _view    = doc.ActiveView as ModelView;

            if (_view != null)
                _view.BufferSwapNotify += OnBufferSwap;

            DisplayAll();
            doc.GraphicsRedraw2();
        }

        private void DisplayAll()
        {
            if (_bodies == null) return;
            foreach (var b in _bodies)
                b.Display3(_partDoc, PreviewBuilder.OrangeColorRef, 1);
        }

        private int OnBufferSwap()
        {
            DisplayAll();
            return 0;
        }

        public void Dispose()
        {
            if (_view != null)
            {
                _view.BufferSwapNotify -= OnBufferSwap;
                _view = null;
            }

            if (_bodies != null)
            {
                // Release COM refs so SW cannot continue rendering these bodies
                // even if Display3 has registered them in an internal draw list.
                foreach (var b in _bodies)
                    try { Marshal.FinalReleaseComObject(b); } catch { }
                _bodies = null;
            }

            // GraphicsRedraw2 here may be a no-op if called from inside a PMP
            // callback; AfterClose in the page handler issues a second one once
            // the page is fully gone.
            try { _doc.GraphicsRedraw2(); } catch { }
        }
    }
}
