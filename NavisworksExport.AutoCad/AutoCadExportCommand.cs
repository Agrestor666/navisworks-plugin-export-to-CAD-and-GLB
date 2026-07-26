using System.Windows.Forms;
using Autodesk.Navisworks.Api.Plugins;
using NwApplication = Autodesk.Navisworks.Api.Application;

namespace NavisworksExport.AutoCad
{
    [Plugin("AutoCadExport", "NWXP", ToolTip = "Export selection to AutoCAD", DisplayName = "Export to AutoCAD")]
    [AddInPlugin(AddInLocation.AddIn)]
    public class AutoCadExportCommand : AddInPlugin
    {
        public override int Execute(params string[] parameters)
        {
            var count = NwApplication.ActiveDocument?.CurrentSelection?.SelectedItems?.Count ?? 0;
            MessageBox.Show($"Selected items: {count}", "Export to AutoCAD (scaffold)");
            return 0;
        }
    }
}
