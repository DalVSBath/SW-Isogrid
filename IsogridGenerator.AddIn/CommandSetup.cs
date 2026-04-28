using System.Windows.Forms;
using CADBooster.SolidDna;
using IsogridGenerator.UI;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace IsogridGenerator.AddIn
{
    internal class CommandSetup
    {
        private const int GroupId    = 1;
        private const int CmdIsogrid = 0;

        private readonly IsogridAddIn _addIn;
        private ICommandGroup? _group;

        public CommandSetup(IsogridAddIn addIn) { _addIn = addIn; }

        public void Register()
        {
            // SolidDNA 4.0: use the add-in's own CommandManager property, which already
            // holds the cookie SolidWorks assigned during ConnectToSW.
            // GetCommandManager(0) always returns null — 0 is not a valid cookie.
            var cmdMgr = _addIn.CommandManager?.UnsafeObject as ICommandManager;
            if (cmdMgr == null) return;

            int errors = -1;

            _group = cmdMgr.CreateCommandGroup2(
                GroupId,
                "Isogrid Generator",
                "Isogrid pattern generator",
                "Isogrid pattern generator",
                -1,    // position (-1 = end)
                true,  // ignorePreviousVersion
                ref errors);

            // errors == 0: new group created.
            // errors == 1: group loaded from a previous SolidWorks session (toolbar positions cached) — not a real error.
            // errors  < 0: actual failure.
            if (_group == null || errors < 0)
            {
                System.Windows.Forms.MessageBox.Show(
                    $"CommandManager group creation failed (error {errors}).\n\n" +
                    "Possible causes:\n" +
                    "  • The add-in was not fully unloaded before reloading — restart SolidWorks.\n" +
                    "  • CommandManager cookie is invalid — rebuild as Administrator.",
                    "Isogrid Generator");
                return;
            }

            _group.AddCommandItem2(
                "Isogrid",
                -1,
                "Generate an isogrid pattern on the selected face",
                "Generate Isogrid",
                0,
                nameof(IsogridAddIn.OnIsogridClick),
                "",
                CmdIsogrid,
                (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem));

            _group.HasMenu    = true;
            // HasToolbar must be true even when no floating toolbar is wanted.
            // SolidWorks' ribbon-tab engine depends on the underlying toolbar
            // infrastructure being registered; false silently prevents tab commands
            // from ever appearing.
            _group.HasToolbar = true;

            // Set icons before Activate() — SolidWorks reads them during activation.
            // Arrays are indexed by the user-defined command ID (CmdIsogrid = 0).
            // IconHelper generates the PNGs on first call and caches the paths.
            var (largeIcon, smallIcon) = IconHelper.EnsureIcons();
            if (largeIcon != null)
            {
                // LargeIconList / SmallIconList are typed as string in the interop —
                // they point to a bitmap strip where all command icons sit side-by-side.
                // With a single command the strip is just the icon itself.
                _group.LargeIconList = largeIcon;
                _group.SmallIconList = smallIcon;
                // MainIconList is the group-level icon shown in the CommandManager
                // dropdown header. It is typed as object and accepts a string[] of
                // paths at different DPI sizes.
                _group.MainIconList = new[] { largeIcon };
            }

            _group.Activate();

            // SolidWorks assigns real command IDs after Activate().
            // CommandID is a COM indexed property — use get_CommandID(index),
            // not a cast to int[], which causes:
            // "Indexed property 'ICommandGroup.CommandID' has non-optional arguments"
            int swCmdId = _group.get_CommandID(CmdIsogrid);

            // Register the tab for Part and Assembly documents.
            AddToTab(cmdMgr, swCmdId, (int)swDocumentTypes_e.swDocPART);
            AddToTab(cmdMgr, swCmdId, (int)swDocumentTypes_e.swDocASSEMBLY);
        }

        private static void AddToTab(ICommandManager cmdMgr, int swCmdId, int docType)
        {
            if (swCmdId <= 0)
            {
                MessageBox.Show(
                    $"Isogrid Generator: invalid SW command ID ({swCmdId}).\n" +
                    "The ribbon tab will not be created.",
                    "Isogrid Generator");
                return;
            }

            // Remove any tab cached from a previous add-in session.
            // Without this, GetCommandTab finds the old (stale) tab and AddCommandTabBox
            // appends a second box to it, producing duplicate buttons in the ribbon.
            var staleTab = cmdMgr.GetCommandTab(docType, "Isogrid Generator");
            if (staleTab != null)
                cmdMgr.RemoveCommandTab(staleTab);

            var tab = cmdMgr.AddCommandTab(docType, "Isogrid Generator");
            if (tab == null)
            {
                MessageBox.Show(
                    $"Isogrid Generator: AddCommandTab returned null for docType {docType}.",
                    "Isogrid Generator");
                return;
            }

            var box = tab.AddCommandTabBox();
            if (box == null) return;

            bool ok = box.AddCommands(
                new[] { swCmdId },
                new[] { (int)swCommandTabButtonTextDisplay_e.swCommandTabButton_TextBelow });

            if (!ok)
                MessageBox.Show(
                    $"Isogrid Generator: AddCommands failed for command ID {swCmdId}.",
                    "Isogrid Generator");
        }

        public void Unregister()
        {
            // SolidWorks removes command groups automatically when the add-in unloads.
        }

        public void HandleIsogridClick()
        {
            var swApp = SolidWorksEnvironment.Application?.UnsafeObject as ISldWorks;
            var doc   = swApp?.ActiveDoc as IModelDoc2;

            if (doc == null)
            {
                MessageBox.Show(
                    "Open a part document before running Isogrid Generator.",
                    "Isogrid Generator",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            var pmp = new IsogridPropertyManagerPage(swApp!, doc);
            pmp.Show();
        }
    }
}
