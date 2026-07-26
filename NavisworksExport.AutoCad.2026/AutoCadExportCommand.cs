using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using NwApplication = Autodesk.Navisworks.Api.Application;

namespace NavisworksExport.AutoCad2026
{
    [Plugin("AutoCadExport2026", "NWXP", ToolTip = "Export selection to AutoCAD", DisplayName = "Export to AutoCAD")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class AutoCadExportCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            var count = NwApplication.ActiveDocument?.CurrentSelection?.SelectedItems?.Count ?? 0;
            MessageBox.Show($"Selected items: {count}", "Export to AutoCAD (2026 scaffold)");
            return 0;
        }
    }
}
