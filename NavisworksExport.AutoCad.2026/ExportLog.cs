using System;
using System.IO;

namespace NavisworksExport.AutoCad2026
{
    internal static class ExportLog
    {
        public static readonly string LogPath =
            Path.Combine(Path.GetTempPath(), "NavisworksExport.AutoCad.2026.log");

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
