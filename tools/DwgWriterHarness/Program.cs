using System;
using System.Collections.Generic;
using System.IO;
using NavisworksExport.AutoCad2026;
using NavisworksExport.Geometry;

namespace DwgWriterHarness
{
    /// <summary>
    /// Phase 2 throwaway: writes a unit cube with distinct per-face colors (Navisworks Z-up)
    /// through DwgWriter so PolyfaceMesh shading + per-face color can be checked in AutoCAD/TrueView.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var outPath = args.Length > 0
                ? args[0]
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "dwg-writer-harness-cube.dwg");

            var fragments = new List<ExtractedFragment>
            {
                new ExtractedFragment(BuildUnitCube()),
            };

            DwgWriter.WriteDwg(fragments, outPath);

            var triangleCount = fragments[0].Triangles.Count;
            var info = new FileInfo(outPath);
            Console.WriteLine(
                $"Wrote {triangleCount} triangles ({info.Length} bytes) to:{Environment.NewLine}{outPath}");
            return info.Exists && info.Length > 0 ? 0 : 1;
        }

        /// <summary>
        /// Axis-aligned unit cube centered at origin, Z-up (Navisworks / AutoCAD space).
        /// Faces: +Z cyan (top), -Z magenta (bottom), +Y yellow, -Y blue, +X red, -X green.
        /// </summary>
        private static IReadOnlyList<ExtractedTriangle> BuildUnitCube()
        {
            var p000 = new Vec3(-0.5, -0.5, -0.5);
            var p001 = new Vec3(-0.5, -0.5, 0.5);
            var p010 = new Vec3(-0.5, 0.5, -0.5);
            var p011 = new Vec3(-0.5, 0.5, 0.5);
            var p100 = new Vec3(0.5, -0.5, -0.5);
            var p101 = new Vec3(0.5, -0.5, 0.5);
            var p110 = new Vec3(0.5, 0.5, -0.5);
            var p111 = new Vec3(0.5, 0.5, 0.5);

            var red = new Rgba(1f, 0.1f, 0.1f);
            var green = new Rgba(0.1f, 0.8f, 0.1f);
            var blue = new Rgba(0.2f, 0.4f, 1f);
            var yellow = new Rgba(1f, 0.9f, 0.1f);
            var cyan = new Rgba(0.1f, 0.9f, 0.9f);
            var magenta = new Rgba(0.9f, 0.2f, 0.9f);

            var list = new List<ExtractedTriangle>(12);

            AddQuad(list, p100, p110, p111, p101, red);
            AddQuad(list, p010, p000, p001, p011, green);
            AddQuad(list, p010, p011, p111, p110, yellow);
            AddQuad(list, p000, p100, p101, p001, blue);
            AddQuad(list, p001, p101, p111, p011, cyan);
            AddQuad(list, p000, p010, p110, p100, magenta);

            return list;
        }

        private static void AddQuad(
            List<ExtractedTriangle> list,
            in Vec3 a, in Vec3 b, in Vec3 c, in Vec3 d,
            in Rgba color)
        {
            var n = Vec3.Cross(b - a, c - a).NormalizedOr(Vec3.UnitZ);
            list.Add(new ExtractedTriangle(a, b, c, n, n, n, color, color, color));
            list.Add(new ExtractedTriangle(a, c, d, n, n, n, color, color, color));
        }
    }
}
