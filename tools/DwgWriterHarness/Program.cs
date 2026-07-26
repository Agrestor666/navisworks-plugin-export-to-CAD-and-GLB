using System;
using System.Collections.Generic;
using System.IO;
using ACadSharp.Entities;
using ACadSharp.IO;
using NavisworksExport.Geometry;
using DwgWriter = NavisworksExport.AutoCad2026.DwgWriter;

namespace DwgWriterHarness
{
    /// <summary>
    /// Host-free check for <see cref="DwgWriter"/>. Writes a pipe, a 90° elbow and a colored cube,
    /// then reads the DWG back and verifies what AutoCAD would otherwise have to be opened to see:
    /// entity colors survive, every face is wound outward, curved surfaces keep their edges, and no
    /// facet is coarse enough to read as a flat panel under AutoCAD's flat shading.
    /// </summary>
    /// <remarks>
    /// Curved surfaces are the interesting case — a cube alone hides every defect, because its edges
    /// all meet at 90° and its facets are already consistently wound. The pipe and elbow are fed with
    /// deliberately alternating winding, the way Navisworks COM delivers it, so the writer's
    /// orientation pass is what has to fix them. The elbow is also deliberately under-tessellated,
    /// which is the shape that showed up in the host as a visibly angular bend.
    /// </remarks>
    internal static class Program
    {
        private const int PipeSegments = 24;

        /// <summary>Coarse on purpose: 45° around the tube and 15° along the bend.</summary>
        private const int ElbowSegments = 8;
        private const int ElbowSteps = 6;

        /// <summary>
        /// Largest angle between neighbouring facets the refined elbow may still have. Above roughly
        /// this the flat-shaded steps are visible as bands rather than as a gradient.
        /// </summary>
        private const double MaxElbowFacetDegrees = 12.0;

        private const double TubeRadius = 0.15;
        private const double BendRadius = 0.5;

        private static readonly Rgba PipeColor = new Rgba(0.8f, 0.4f, 0.1f);
        private static readonly Rgba ElbowColor = new Rgba(0.1f, 0.5f, 0.9f);

        private static int Main(string[] args)
        {
            var outPath = args.Length > 0
                ? args[0]
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "dwg-writer-harness.dwg");

            var pipe = BuildPipe();
            var elbow = BuildElbow();
            var cube = BuildUnitCube();

            var fragments = new List<ExtractedFragment>
            {
                new ExtractedFragment(pipe),
                new ExtractedFragment(elbow),
                new ExtractedFragment(cube),
            };

            Console.WriteLine(
                $"Input: pipe {pipe.Count} + elbow {elbow.Count} + cube {cube.Count} triangles, " +
                $"elbow facets up to {MaxFacetDegrees(SourceFaces(elbow)):0.0}°");

            DwgWriter.WriteDwg(fragments, outPath, Console.WriteLine);

            var info = new FileInfo(outPath);
            Console.WriteLine($"Wrote {info.Length} bytes to:{Environment.NewLine}{outPath}");
            Console.WriteLine();

            return Verify(outPath) ? 0 : 1;
        }

        private static bool Verify(string path)
        {
            var meshes = new List<PolyfaceMesh>();
            using (var reader = new DwgReader(path, null))
            {
                var doc = reader.Read();
                foreach (var entity in doc.Entities)
                {
                    if (entity is PolyfaceMesh mesh)
                    {
                        meshes.Add(mesh);
                    }
                }
            }

            Console.WriteLine($"Read back {meshes.Count} PolyfaceMesh entity/entities:");
            var ok = true;

            foreach (var mesh in meshes)
            {
                var rgb = $"{mesh.Color.R},{mesh.Color.G},{mesh.Color.B}";
                var edges = EdgeVisibility(mesh);
                var hidden = 0;
                foreach (var visible in edges.Values)
                {
                    if (!visible)
                    {
                        hidden++;
                    }
                }

                Console.WriteLine(
                    $"  RGB {rgb,-13} verts={mesh.Vertices.Count,-6} faces={mesh.Faces.Count,-6} " +
                    $"edges={edges.Count,-6} hidden={hidden}");
            }

            ok &= CheckColor(meshes, PipeColor, "pipe");
            ok &= CheckColor(meshes, ElbowColor, "elbow");
            ok &= CheckOutward(meshes, PipeColor, "pipe", PipeRadialDirection);
            ok &= CheckOutward(meshes, ElbowColor, "elbow", ElbowRadialDirection);
            ok &= CheckPipeFacetEdgesVisible(meshes);
            ok &= CheckElbowSmoothness(meshes);

            Console.WriteLine();
            Console.WriteLine(ok ? "PASS" : "FAIL");
            return ok;
        }

        private static bool CheckColor(List<PolyfaceMesh> meshes, in Rgba expected, string label)
        {
            var want = (ToByte(expected.R), ToByte(expected.G), ToByte(expected.B));
            var mesh = Find(meshes, expected);
            if (mesh == null)
            {
                Console.WriteLine(
                    $"  [FAIL] {label}: no mesh with RGB {want.Item1},{want.Item2},{want.Item3}");
                return false;
            }

            Console.WriteLine(
                $"  [ok]   {label} color round-trips as {want.Item1},{want.Item2},{want.Item3}");
            return true;
        }

        /// <summary>
        /// A PolyfaceMesh carries no normals, so AutoCAD lights each face from its winding order.
        /// Every face therefore has to be wound so the derived normal points out of the solid.
        /// </summary>
        private static bool CheckOutward(
            List<PolyfaceMesh> meshes,
            in Rgba color,
            string label,
            Func<Vec3, Vec3> outwardAt)
        {
            var mesh = Find(meshes, color);
            if (mesh == null)
            {
                return false;
            }

            var reversed = 0;
            foreach (var face in mesh.Faces)
            {
                var a = Location(mesh, face.Index1);
                var b = Location(mesh, face.Index2);
                var c = Location(mesh, face.Index3);

                var derived = Vec3.Cross(b - a, c - a);
                var centroid = new Vec3((a.X + b.X + c.X) / 3.0, (a.Y + b.Y + c.Y) / 3.0, (a.Z + b.Z + c.Z) / 3.0);
                if (Dot(derived, outwardAt(centroid)) <= 0.0)
                {
                    reversed++;
                }
            }

            if (reversed > 0)
            {
                Console.WriteLine(
                    $"  [FAIL] {label}: {reversed}/{mesh.Faces.Count} faces wound inward (shade dark)");
                return false;
            }

            Console.WriteLine($"  [ok]   {label}: all {mesh.Faces.Count} faces wound outward");
            return true;
        }

        /// <summary>
        /// The reported defect: a straight pipe drew nothing in 2D wireframe. Its barrel is only
        /// readable through the facet edges running along the axis, and at 24 segments those meet at
        /// just ~15° — so any generous crease angle hides all of them and the pipe disappears.
        /// </summary>
        private static bool CheckPipeFacetEdgesVisible(List<PolyfaceMesh> meshes)
        {
            var mesh = Find(meshes, PipeColor);
            if (mesh == null)
            {
                return false;
            }

            var axial = 0;
            var axialHidden = 0;
            foreach (var pair in EdgeVisibility(mesh))
            {
                var a = Location(mesh, (short)pair.Key.Item1);
                var b = Location(mesh, (short)pair.Key.Item2);

                // Runs along the tube: same position around the circumference, different height.
                var sameAround = Math.Abs(a.X - b.X) < 1e-9 && Math.Abs(a.Y - b.Y) < 1e-9;
                if (!sameAround || Math.Abs(a.Z - b.Z) < 1e-9)
                {
                    continue;
                }

                axial++;
                if (!pair.Value)
                {
                    axialHidden++;
                }
            }

            if (axial < PipeSegments || axialHidden > 0)
            {
                Console.WriteLine(
                    $"  [FAIL] pipe: {axial} axial facet edges (want at least {PipeSegments}), " +
                    $"{axialHidden} of them hidden — barrel is invisible in 2D wireframe");
                return false;
            }

            Console.WriteLine($"  [ok]   pipe: all {axial} axial facet edges visible in wireframe");
            return true;
        }

        /// <summary>
        /// The reported defect on bends: AutoCAD flat-shades a PolyfaceMesh, so a coarse elbow reads
        /// as a stack of panels no matter how good the source normals were. The writer's refinement
        /// pass has to bring neighbouring facets close enough together to pass for a gradient.
        /// </summary>
        private static bool CheckElbowSmoothness(List<PolyfaceMesh> meshes)
        {
            var mesh = Find(meshes, ElbowColor);
            if (mesh == null)
            {
                return false;
            }

            var worst = MaxFacetDegrees(MeshFaces(mesh));
            if (worst > MaxElbowFacetDegrees)
            {
                Console.WriteLine(
                    $"  [FAIL] elbow: facets still meet at up to {worst:0.0}° " +
                    $"(want at most {MaxElbowFacetDegrees:0.0}°) — the bend shades as flat panels");
                return false;
            }

            Console.WriteLine(
                $"  [ok]   elbow: {mesh.Faces.Count} facets, none meeting at more than {worst:0.0}°");
            return true;
        }

        /// <summary>
        /// Widest angle between any two facets that share an edge. Measured on unsigned normals so it
        /// reports the shape, not the winding — the source triangles arrive wound inconsistently.
        /// </summary>
        private static double MaxFacetDegrees(IEnumerable<(Vec3 A, Vec3 B, Vec3 C)> faces)
        {
            var normals = new List<Vec3>();
            var adjacency = new Dictionary<((long, long, long), (long, long, long)), int>();
            var worst = 0.0;

            foreach (var face in faces)
            {
                var index = normals.Count;
                normals.Add(Vec3.Cross(face.B - face.A, face.C - face.A).NormalizedOr(Vec3.UnitZ));
                Visit(face.A, face.B, index);
                Visit(face.B, face.C, index);
                Visit(face.C, face.A, index);
            }

            return worst;

            void Visit(in Vec3 a, in Vec3 b, int face)
            {
                var qa = Quantize(a);
                var qb = Quantize(b);
                var key = Precedes(qa, qb) ? (qa, qb) : (qb, qa);
                if (!adjacency.TryGetValue(key, out var other))
                {
                    adjacency[key] = face;
                    return;
                }

                var cos = Math.Min(1.0, Math.Abs(Dot(normals[other], normals[face])));
                worst = Math.Max(worst, Math.Acos(cos) * 180.0 / Math.PI);
            }
        }

        private static IEnumerable<(Vec3, Vec3, Vec3)> SourceFaces(IReadOnlyList<ExtractedTriangle> triangles)
        {
            foreach (var tri in triangles)
            {
                yield return (tri.V0, tri.V1, tri.V2);
            }
        }

        private static IEnumerable<(Vec3, Vec3, Vec3)> MeshFaces(PolyfaceMesh mesh)
        {
            foreach (var face in mesh.Faces)
            {
                yield return (
                    Location(mesh, face.Index1),
                    Location(mesh, face.Index2),
                    Location(mesh, face.Index3));
            }
        }

        private static (long, long, long) Quantize(in Vec3 v) => (
            (long)Math.Round(v.X * 1e6),
            (long)Math.Round(v.Y * 1e6),
            (long)Math.Round(v.Z * 1e6));

        private static bool Precedes(in (long X, long Y, long Z) a, in (long X, long Y, long Z) b)
        {
            if (a.X != b.X)
            {
                return a.X < b.X;
            }

            return a.Y != b.Y ? a.Y < b.Y : a.Z < b.Z;
        }

        /// <summary>
        /// Distinct edges of the mesh mapped to whether they are drawn. A PolyfaceMesh marks an edge
        /// invisible by negating the index it starts at, and a shared edge carries the flag in both
        /// of its faces, so edges have to be de-duplicated before they are counted.
        /// </summary>
        private static Dictionary<(int, int), bool> EdgeVisibility(PolyfaceMesh mesh)
        {
            var edges = new Dictionary<(int, int), bool>();
            foreach (var face in mesh.Faces)
            {
                Record(face.Index1, face.Index2);
                Record(face.Index2, face.Index3);
                Record(face.Index3, face.Index1);
            }

            void Record(short from, short to)
            {
                var a = Math.Abs((int)from);
                var b = Math.Abs((int)to);
                var key = a < b ? (a, b) : (b, a);
                var visible = from > 0;
                edges[key] = edges.TryGetValue(key, out var seen) ? seen && visible : visible;
            }

            return edges;
        }

        private static PolyfaceMesh? Find(List<PolyfaceMesh> meshes, in Rgba color)
        {
            byte r = ToByte(color.R), g = ToByte(color.G), b = ToByte(color.B);
            foreach (var mesh in meshes)
            {
                if (mesh.Color.R == r && mesh.Color.G == g && mesh.Color.B == b)
                {
                    return mesh;
                }
            }

            return null;
        }

        private static Vec3 Location(PolyfaceMesh mesh, short index)
        {
            var location = mesh.Vertices[Math.Abs(index) - 1].Location;
            return new Vec3(location.X, location.Y, location.Z);
        }

        /// <summary>Outward direction on a Z-axis tube: straight away from the axis.</summary>
        private static Vec3 PipeRadialDirection(Vec3 p) =>
            new Vec3(p.X, p.Y, 0).NormalizedOr(Vec3.UnitZ);

        /// <summary>
        /// Outward direction on the elbow: away from the nearest point of the bend centerline,
        /// which for a torus about Z sits at the same polar angle as the sample point.
        /// </summary>
        private static Vec3 ElbowRadialDirection(Vec3 p)
        {
            var phi = Math.Atan2(p.Y, p.X);
            var center = new Vec3(BendRadius * Math.Cos(phi), BendRadius * Math.Sin(phi), 0);
            return (p - center).NormalizedOr(Vec3.UnitZ);
        }

        /// <summary>
        /// Straight tube along Z, smooth radial normals, barrel only. Winding alternates per
        /// segment so the writer has to normalize it.
        /// </summary>
        private static IReadOnlyList<ExtractedTriangle> BuildPipe()
        {
            var list = new List<ExtractedTriangle>(PipeSegments * 2);
            for (var i = 0; i < PipeSegments; i++)
            {
                var a0 = 2.0 * Math.PI * i / PipeSegments;
                var a1 = 2.0 * Math.PI * (i + 1) / PipeSegments;

                var n0 = new Vec3(Math.Cos(a0), Math.Sin(a0), 0);
                var n1 = new Vec3(Math.Cos(a1), Math.Sin(a1), 0);

                var p00 = new Vec3(TubeRadius * n0.X, TubeRadius * n0.Y, 0);
                var p01 = new Vec3(TubeRadius * n0.X, TubeRadius * n0.Y, 1.0);
                var p10 = new Vec3(TubeRadius * n1.X, TubeRadius * n1.Y, 0);
                var p11 = new Vec3(TubeRadius * n1.X, TubeRadius * n1.Y, 1.0);

                AddQuad(list, p00, n0, p10, n1, p11, n1, p01, n0, PipeColor, flip: i % 2 == 1);
            }

            return list;
        }

        /// <summary>
        /// 90° torus-segment elbow about Z, smooth normals, surface only. Winding alternates per
        /// quad, again to exercise the writer's orientation pass.
        /// </summary>
        private static IReadOnlyList<ExtractedTriangle> BuildElbow()
        {
            var list = new List<ExtractedTriangle>(ElbowSteps * ElbowSegments * 2);

            for (var i = 0; i < ElbowSteps; i++)
            {
                var phi0 = (Math.PI / 2.0) * i / ElbowSteps;
                var phi1 = (Math.PI / 2.0) * (i + 1) / ElbowSteps;

                for (var j = 0; j < ElbowSegments; j++)
                {
                    var t0 = 2.0 * Math.PI * j / ElbowSegments;
                    var t1 = 2.0 * Math.PI * (j + 1) / ElbowSegments;

                    var n00 = TorusNormal(phi0, t0);
                    var n01 = TorusNormal(phi0, t1);
                    var n10 = TorusNormal(phi1, t0);
                    var n11 = TorusNormal(phi1, t1);

                    AddQuad(
                        list,
                        TorusPoint(phi0, t0), n00,
                        TorusPoint(phi1, t0), n10,
                        TorusPoint(phi1, t1), n11,
                        TorusPoint(phi0, t1), n01,
                        ElbowColor,
                        flip: (i + j) % 2 == 1);
                }
            }

            return list;
        }

        private static Vec3 TorusNormal(double phi, double theta)
        {
            var u = new Vec3(Math.Cos(phi), Math.Sin(phi), 0);
            return new Vec3(
                (Math.Cos(theta) * u.X) + 0,
                (Math.Cos(theta) * u.Y) + 0,
                Math.Sin(theta));
        }

        private static Vec3 TorusPoint(double phi, double theta)
        {
            var n = TorusNormal(phi, theta);
            return new Vec3(
                (BendRadius * Math.Cos(phi)) + (TubeRadius * n.X),
                (BendRadius * Math.Sin(phi)) + (TubeRadius * n.Y),
                TubeRadius * n.Z);
        }

        /// <summary>
        /// Axis-aligned unit cube offset clear of the tubes, Z-up (Navisworks / AutoCAD space).
        /// Faces: +Z cyan (top), -Z magenta (bottom), +Y yellow, -Y blue, +X red, -X green.
        /// </summary>
        private static IReadOnlyList<ExtractedTriangle> BuildUnitCube()
        {
            const double Offset = 2.0;
            var p000 = new Vec3(Offset - 0.5, -0.5, -0.5);
            var p001 = new Vec3(Offset - 0.5, -0.5, 0.5);
            var p010 = new Vec3(Offset - 0.5, 0.5, -0.5);
            var p011 = new Vec3(Offset - 0.5, 0.5, 0.5);
            var p100 = new Vec3(Offset + 0.5, -0.5, -0.5);
            var p101 = new Vec3(Offset + 0.5, -0.5, 0.5);
            var p110 = new Vec3(Offset + 0.5, 0.5, -0.5);
            var p111 = new Vec3(Offset + 0.5, 0.5, 0.5);

            var red = new Rgba(1f, 0.1f, 0.1f);
            var green = new Rgba(0.1f, 0.8f, 0.1f);
            var blue = new Rgba(0.2f, 0.4f, 1f);
            var yellow = new Rgba(1f, 0.9f, 0.1f);
            var cyan = new Rgba(0.1f, 0.9f, 0.9f);
            var magenta = new Rgba(0.9f, 0.2f, 0.9f);

            var list = new List<ExtractedTriangle>(12);

            AddFlatQuad(list, p100, p110, p111, p101, red);
            AddFlatQuad(list, p010, p000, p001, p011, green);
            AddFlatQuad(list, p010, p011, p111, p110, yellow);
            AddFlatQuad(list, p000, p100, p101, p001, blue);
            AddFlatQuad(list, p001, p101, p111, p011, cyan);
            AddFlatQuad(list, p000, p010, p110, p100, magenta);

            return list;
        }

        private static void AddFlatQuad(
            List<ExtractedTriangle> list,
            in Vec3 a, in Vec3 b, in Vec3 c, in Vec3 d,
            in Rgba color)
        {
            var n = Vec3.Cross(b - a, c - a).NormalizedOr(Vec3.UnitZ);
            AddQuad(list, a, n, b, n, c, n, d, n, color, flip: false);
        }

        private static void AddQuad(
            List<ExtractedTriangle> list,
            in Vec3 a, in Vec3 na,
            in Vec3 b, in Vec3 nb,
            in Vec3 c, in Vec3 nc,
            in Vec3 d, in Vec3 nd,
            in Rgba color,
            bool flip)
        {
            if (flip)
            {
                list.Add(new ExtractedTriangle(a, c, b, na, nc, nb, color, color, color));
                list.Add(new ExtractedTriangle(a, d, c, na, nd, nc, color, color, color));
            }
            else
            {
                list.Add(new ExtractedTriangle(a, b, c, na, nb, nc, color, color, color));
                list.Add(new ExtractedTriangle(a, c, d, na, nc, nd, color, color, color));
            }
        }

        private static double Dot(in Vec3 a, in Vec3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

        /// <summary>Mirrors the writer's linear-to-sRGB encoding, including its double precision.</summary>
        private static byte ToByte(float linear)
        {
            if (linear <= 0f)
            {
                return 0;
            }

            if (linear >= 1f)
            {
                return 255;
            }

            var v = (double)linear;
            var encoded = v <= 0.0031308 ? v * 12.92 : (1.055 * Math.Pow(v, 1.0 / 2.4)) - 0.055;
            return (byte)Math.Round(encoded * 255.0);
        }
    }
}
