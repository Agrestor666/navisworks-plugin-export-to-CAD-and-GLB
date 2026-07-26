using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.Plugins;
using NavisworksExport.Glb;
using NavisworksExport.Geometry;
using NwApplication = Autodesk.Navisworks.Api.Application;

namespace NavisworksExport.Glb2026
{
    [Plugin("GlbExport2026", "NWXP", ToolTip = "Export selection to GLB", DisplayName = "Export to GLB")]
    [AddInPlugin(AddInLocation.AddIn, Icon = "Images\\glb16.png", LargeIcon = "Images\\glb32.png")]
    public class GlbExportCommand : AddInPlugin
    {
        private const string Caption = "Export to GLB";

        public override int Execute(params string[] parameters)
        {
            ExportLog.Write("Execute entered");
            try
            {
                // The host does not probe the plugin folder for dependencies, so they must be
                // resolvable before RunExport is JIT-compiled and its references are loaded.
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
            ExportLog.Write("sharpgltf assembly: " + typeof(SharpGLTF.Schema2.ModelRoot).Assembly.Location);

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
                    ExportLog.Write("dialog cancelled");
                    return 0;
                }

                filePath = dialog.FileName;
            }

            ExportLog.Write("target = " + filePath);

            ExportLog.Write("extract start");
            var triangles = new SelectionGeometryExtractor().Extract(selection, ExportLog.Write);
            ExportLog.Write($"extract done: {triangles.Count} triangles");
            if (triangles.Count == 0)
            {
                ShowError("The selection has no extractable mesh geometry. Nothing was exported.");
                return 1;
            }

            var scale = UnitConversion.ScaleFactor(doc!.Units, Units.Meters);
            ExportLog.Write($"units = {doc.Units}, scale = {scale}");

            ExportLog.Write("write start");
            GlbWriter.WriteGlb(triangles, scale, filePath);
            ExportLog.Write("write done");

            MessageBox.Show(
                $"Exported {triangles.Count} triangles to:{Environment.NewLine}{filePath}",
                Caption,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }

        private static void ShowError(string message)
        {
            MessageBox.Show(message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Resolves this plugin's private dependencies (geometry twin, SharpGLTF, …) from the folder
        /// the plugin assembly was deployed to. Navisworks loads the plugin in a way that leaves that
        /// folder out of the CLR probing path, so unresolved references would otherwise throw
        /// <see cref="FileNotFoundException"/> when a referencing method is JIT-compiled.
        /// </summary>
        private static class PluginAssemblyResolver
        {
            private static int _installed;

            public static void Install()
            {
                if (Interlocked.Exchange(ref _installed, 1) == 1)
                {
                    return;
                }

                var folder = PluginFolder();
                ExportLog.Write("plugin folder = " + (folder ?? "<unknown>"));
                if (string.IsNullOrEmpty(folder))
                {
                    return;
                }

                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) => Resolve(folder!, args);
            }

            private static Assembly? Resolve(string folder, ResolveEventArgs args)
            {
                try
                {
                    var simpleName = new AssemblyName(args.Name).Name;
                    var candidate = Path.Combine(folder, simpleName + ".dll");
                    if (!File.Exists(candidate))
                    {
                        return null;
                    }

                    ExportLog.Write($"resolved {simpleName} -> {candidate}");
                    return Assembly.LoadFrom(candidate);
                }
                catch (Exception ex)
                {
                    ExportLog.Write($"resolve failed for {args.Name}: {ex.Message}");
                    return null;
                }
            }

            private static string? PluginFolder()
            {
                var assembly = typeof(GlbExportCommand).Assembly;
                if (!string.IsNullOrEmpty(assembly.Location))
                {
                    return Path.GetDirectoryName(assembly.Location);
                }

                // Assemblies loaded from a byte array report an empty Location.
                return string.IsNullOrEmpty(assembly.CodeBase)
                    ? null
                    : Path.GetDirectoryName(new Uri(assembly.CodeBase).LocalPath);
            }
        }

        private static class ExportLog
        {
            public static readonly string LogPath =
                Path.Combine(Path.GetTempPath(), "NavisworksExport.Glb.2026.log");

            public static void Write(string message)
            {
                try
                {
                    File.AppendAllText(
                        LogPath,
                        $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {message}{Environment.NewLine}");
                }
                catch
                {
                    // Diagnostics must never break the export.
                }
            }
        }
    }
}
