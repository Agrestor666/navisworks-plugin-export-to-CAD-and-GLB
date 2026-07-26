using System;
using System.Collections.Generic;
using System.IO;
using NavisworksExport.Geometry;
using NavisworksExport.Glb;

namespace GlbWriterHarness
{
    /// <summary>
    /// Phase 2 throwaway: writes a unit cube with distinct per-face colors (Navisworks Z-up coords)
    /// through GlbWriter so the axis swap + vertex colors can be checked in a glTF viewer / PowerPoint.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var outPath = args.Length > 0
                ? args[0]
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "glb-writer-harness-cube.glb");

            var triangles = BuildUnitCube();
            GlbWriter.WriteGlb(triangles, unitsToMetersScale: 1.0, outPath);
            Console.WriteLine($"Wrote {triangles.Count} triangles to:{Environment.NewLine}{outPath}");
            return 0;
        }

        /// <summary>
        /// Axis-aligned unit cube centered at origin, Z-up (Navisworks space).
        /// Faces: +Z cyan (top), -Z magenta (bottom), +Y yellow, -Y blue, +X red, -X green.
        /// After GlbWriter conversion, +Z becomes +Y (upright).
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

            // +X (red)
            AddQuad(list, p100, p110, p111, p101, red);
            // -X (green)
            AddQuad(list, p010, p000, p001, p011, green);
            // +Y (yellow) — toward +Y in NW; becomes -Z in glTF
            AddQuad(list, p010, p011, p111, p110, yellow);
            // -Y (blue)
            AddQuad(list, p000, p100, p101, p001, blue);
            // +Z top (cyan) — becomes +Y up in glTF
            AddQuad(list, p001, p101, p111, p011, cyan);
            // -Z bottom (magenta)
            AddQuad(list, p000, p010, p110, p100, magenta);

            return list;
        }

        private static void AddQuad(
            List<ExtractedTriangle> list,
            in Vec3 a, in Vec3 b, in Vec3 c, in Vec3 d,
            in Rgba color)
        {
            list.Add(new ExtractedTriangle(a, b, c, color, color, color));
            list.Add(new ExtractedTriangle(a, c, d, color, color, color));
        }
    }
}
