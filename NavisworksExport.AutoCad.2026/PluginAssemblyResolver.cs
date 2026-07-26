using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace NavisworksExport.AutoCad2026
{
    /// <summary>
    /// Probes this plugin's deploy folder for private dependencies (ACadSharp, Geometry, …).
    /// Must run at assembly load — before the host's GetTypes scan — because writer types in this
    /// assembly reference ACadSharp and otherwise surface as ReflectionTypeLoadException in Message Center.
    /// </summary>
    internal static class PluginAssemblyResolver
    {
        private static int _installed;

        [ModuleInitializer]
        internal static void Init() => Install();

        public static void Install()
        {
            if (Interlocked.Exchange(ref _installed, 1) == 1)
            {
                return;
            }

            var folder = PluginFolder();
            ExportLog.Write("resolver install; plugin folder = " + (folder ?? "<unknown>"));
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += (_, args) => Resolve(folder!, args);
        }

        private static Assembly? Resolve(string folder, ResolveEventArgs args)
        {
            try
            {
                var simpleName = new AssemblyName(args.Name).Name;
                if (string.IsNullOrEmpty(simpleName))
                {
                    return null;
                }

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
            var assembly = typeof(PluginAssemblyResolver).Assembly;
            if (!string.IsNullOrEmpty(assembly.Location))
            {
                return Path.GetDirectoryName(assembly.Location);
            }

            return string.IsNullOrEmpty(assembly.CodeBase)
                ? null
                : Path.GetDirectoryName(new Uri(assembly.CodeBase).LocalPath);
        }
    }
}
