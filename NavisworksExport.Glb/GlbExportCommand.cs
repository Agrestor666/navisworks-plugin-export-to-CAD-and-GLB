using System;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NavisworksExport.Geometry;
using NwApplication = Autodesk.Navisworks.Api.Application;

namespace NavisworksExport.Glb
{
    [Plugin("GlbExport", "NWXP", ToolTip = "Export selection to GLB", DisplayName = "Export to GLB")]
    [AddInPlugin(AddInLocation.AddIn, Icon = "Images\\glb16.png", LargeIcon = "Images\\glb32.png")]
    public class GlbExportCommand : AddInPlugin
    {
        private const string Caption = "Export to GLB";

        public override int Execute(params string[] parameters)
        {
            var doc = NwApplication.ActiveDocument;
            var selection = doc?.CurrentSelection?.SelectedItems;
            if (selection == null || selection.Count == 0)
            {
                ShowError("Select one or more items to export. The current selection is empty.");
                return 1;
            }

            string filePath;
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Export selection to GLB";
                dialog.Filter = "glTF Binary (*.glb)|*.glb";
                dialog.DefaultExt = "glb";
                dialog.AddExtension = true;
                dialog.FileName = "selection.glb";
                dialog.OverwritePrompt = true;

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return 0;
                }

                filePath = dialog.FileName;
            }

            try
            {
                var triangles = new SelectionGeometryExtractor().Extract(selection);
                if (triangles.Count == 0)
                {
                    ShowError("The selection has no extractable mesh geometry. Nothing was exported.");
                    return 1;
                }

                var scale = UnitConversion.ScaleFactor(doc!.Units, Units.Meters);
                GlbWriter.WriteGlb(triangles, scale, filePath);

                MessageBox.Show(
                    $"Exported {triangles.Count} triangles to:{Environment.NewLine}{filePath}",
                    Caption,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                ShowError($"Export failed:{Environment.NewLine}{ex.Message}");
                return 1;
            }
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
