using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using NwApplication = Autodesk.Navisworks.Api.Application;

namespace NavisworksExport.Glb
{
    [Plugin("GlbExport", "NWXP", ToolTip = "Export selection to GLB", DisplayName = "Export to GLB")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class GlbExportCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            var count = NwApplication.ActiveDocument?.CurrentSelection?.SelectedItems?.Count ?? 0;
            MessageBox.Show($"Selected items: {count}", "Export to GLB (scaffold)");
            return 0;
        }
    }
}
