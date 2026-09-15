using System;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using ACadSharp.Types.Units;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NavisworksExport.Geometry;
using NwApplication = Autodesk.Navisworks.Api.Application;

namespace NavisworksExport.AutoCad2026
{
    [Plugin("AutoCadExport2026", "NWXP", ToolTip = "Export selection to AutoCAD", DisplayName = "Export to AutoCAD")]
    [AddInPlugin(AddInLocation.AddIn, Icon = "Images\\autocad16.png", LargeIcon = "Images\\autocad32.png")]
    public class AutoCadExportCommand : AddInPlugin
    {
        private const string Caption = "Export to AutoCAD";

        public override int Execute(params string[] parameters)
        {
            ExportLog.Write("Execute entered");
            try
            {
                // ModuleInitializer already installed the resolver; keep this for defense in depth.
                PluginAssemblyResolver.Install();
                return RunExport();
            }
            catch (Exception ex)
            {
                ExportLog.Write("FAILED: " + ex);
                ShowError(
                    $"Export failed:{Environment.NewLine}{ex.GetType().Name}: {ex.Message}" +
                    $"{Environment.NewLine}{Environment.NewLine}Details: {ExportLog.LogPath}");
                return 1;
            }
        }

        /// <summary>
        /// Kept out of <see cref="Execute"/> so that assembly/type-load failures are raised when this
        /// method is JIT-compiled — inside the caller's try — instead of escaping unhandled.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private int RunExport()
        {
            var doc = NwApplication.ActiveDocument;
            var selection = doc?.CurrentSelection?.SelectedItems;
            ExportLog.Write($"selection = {(selection == null ? "<null>" : selection.Count.ToString())}");
            if (selection == null || selection.Count == 0)
            {
                ShowError("Select one or more items to export. The current selection is empty.");
                return 1;
            }

            ExportLog.Write("geometry assembly: " + typeof(SelectionGeometryExtractor).Assembly.Location);
            ExportLog.Write("acadsharp assembly: " + typeof(ACadSharp.CadDocument).Assembly.Location);

            string filePath;
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Export selection to AutoCAD";
                dialog.Filter = "AutoCAD Drawing (*.dwg)|*.dwg";
                dialog.DefaultExt = "dwg";
                dialog.AddExtension = true;
                dialog.FileName = "selection.dwg";
                dialog.OverwritePrompt = true;

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    ExportLog.Write("dialog cancelled");
                    return 0;
                }

                filePath = dialog.FileName;
            }

            ExportLog.Write("target = " + filePath);

            ExportLog.Write("extract start");
            var fragments = new SelectionGeometryExtractor().ExtractGrouped(selection, ExportLog.Write);
            var triangleCount = 0;
            foreach (var fragment in fragments)
            {
                if (fragment?.Triangles != null)
                {
                    triangleCount += fragment.Triangles.Count;
                }
            }

            ExportLog.Write($"extract done: {fragments.Count} fragment(s), {triangleCount} triangles");
            if (triangleCount == 0)
            {
                ShowError("The selection has no extractable mesh geometry. Nothing was exported.");
                return 1;
            }

            var docUnits = doc!.Units;
            var coordinateScale = UnitConversion.ScaleFactor(docUnits, Units.Millimeters);
            var insUnits = MapToInsUnits(docUnits);
            ExportLog.Write($"units = {docUnits}, coordinateScale = {coordinateScale}, insUnits = {insUnits}");

            ExportLog.Write("write start");
            DwgWriter.WriteDwg(fragments, filePath, coordinateScale, insUnits, ExportLog.Write);
            ExportLog.Write("write done");

            MessageBox.Show(
                $"Exported {triangleCount} triangles to:{Environment.NewLine}{filePath}",
                Caption,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static UnitsType MapToInsUnits(Units nwUnits)
        {
            switch (nwUnits)
            {
                case Units.Millimeters: return UnitsType.Millimeters;
                case Units.Centimeters: return UnitsType.Centimeters;
                case Units.Meters:      return UnitsType.Meters;
                case Units.Kilometers:  return UnitsType.Kilometers;
                case Units.Inches:      return UnitsType.Inches;
                case Units.Feet:        return UnitsType.Feet;
                case Units.Yards:       return UnitsType.Yards;
                case Units.Miles:       return UnitsType.Miles;
                case Units.Micrometers: return UnitsType.Microns;
                case Units.Mils:        return UnitsType.Mils;
                case Units.Microinches: return UnitsType.Microinches;
                default:                return UnitsType.Unitless;
            }
        }
    }
}
