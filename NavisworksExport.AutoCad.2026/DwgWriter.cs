using System;
using System.Collections.Generic;
using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using NavisworksExport.Geometry;
using ACadDwgWriter = ACadSharp.IO.DwgWriter;

namespace NavisworksExport.AutoCad2026
{
    /// <summary>
    /// Writes world-space extracted fragments to an AC1032 DWG of colored <see cref="PolyfaceMesh"/> entities.
    /// Coordinates stay Z-up (Navisworks / AutoCAD); no glTF Y-up swap.
    /// </summary>
    /// <remarks>
    /// ACadSharp's DWG writer does not persist <see cref="VertexFaceRecord.Color"/> (faces round-trip as
    /// ByLayer), so color is carried on the <see cref="PolyfaceMesh"/> entity: one mesh per color per
    /// fragment. Welding stays inside a single fragment — merging vertices across objects makes AutoCAD
    /// average shading normals over touching surfaces, which renders them dark. A PolyfaceMesh stores no
    /// normals, so faces are wound to agree with the Navisworks normal and only exactly-coplanar
    /// triangulation seams are hidden; every modelled edge stays visible in 2D wireframe. Curved surfaces
    /// go through <see cref="CurvedSurfaceRefiner"/> first, because flat shading is all AutoCAD has.
    /// </remarks>
    public static class DwgWriter
    {
        /// <summary>
        /// PolyfaceMesh face indices are 16-bit; stay under short.MaxValue vertices per entity.
        /// </summary>
        private const int MaxVerticesPerMesh = 32_000;

        /// <summary>
        /// Quantize world positions when welding so nearly-identical COM vertices share a slot.
        /// Shared vertices let AutoCAD shade across triangles instead of faceting every face.
        /// </summary>
        private const double WeldEpsilon = 1e-6;

        /// <summary>
        /// Only edges between faces this close to coplanar are treated as triangulation seams and
        /// hidden. It has to stay tight: a pipe tessellated into 24 segments has ~15° between
        /// adjacent facets, and hiding those erases the whole barrel in 2D wireframe.
        /// </summary>
        private const double CreaseAngleDegrees = 1.0;

        public static void WriteDwg(
            IReadOnlyList<ExtractedFragment> fragments,
            string filePath,
            Action<string>? log = null)
        {
            if (fragments is null)
            {
                throw new ArgumentNullException(nameof(fragments));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A non-empty file path is required.", nameof(filePath));
            }

            var triangleCount = 0;
            foreach (var fragment in fragments)
            {
                triangleCount += fragment?.Triangles?.Count ?? 0;
            }

            if (triangleCount == 0)
            {
                throw new InvalidOperationException(
                    "Cannot write a DWG with no mesh triangles.");
            }

            fragments = CurvedSurfaceRefiner.Refine(fragments, log);

            var doc = new CadDocument();
            doc.Header.Version = ACadVersion.AC1032;

            var byColor = new Dictionary<(byte R, byte G, byte B), List<ExtractedTriangle>>();
            var palette = new Dictionary<(byte R, byte G, byte B), Rgba>();
            foreach (var fragment in fragments)
            {
                if (fragment?.Triangles == null || fragment.Triangles.Count == 0)
                {
                    continue;
                }

                // A fragment normally carries one color; group anyway so mixed fragments stay correct.
                byColor.Clear();
                foreach (var tri in fragment.Triangles)
                {
                    var key = AverageFaceRgb(tri.C0, tri.C1, tri.C2);
                    if (!byColor.TryGetValue(key, out var list))
                    {
                        list = new List<ExtractedTriangle>();
                        byColor[key] = list;
                    }

                    list.Add(tri);
                }

                foreach (var pair in byColor)
                {
                    if (!palette.ContainsKey(pair.Key))
                    {
                        palette[pair.Key] = pair.Value[0].C0;
                    }

                    AppendColoredMeshes(doc, pair.Value, new Color(pair.Key.R, pair.Key.G, pair.Key.B));
                }
            }

            LogPalette(palette, log);
            SetActiveViewportShaded(doc);

            using (var writer = new ACadDwgWriter(filePath, doc))
            {
                writer.Write();
            }
        }

        private static void AppendColoredMeshes(
            CadDocument doc,
            IReadOnlyList<ExtractedTriangle> triangles,
            Color meshColor)
        {
            var chunk = new MeshChunk();

            foreach (var tri in triangles)
            {
                if (chunk.VertexCount + 3 > MaxVerticesPerMesh)
                {
                    chunk.Emit(doc, meshColor);
                    chunk = new MeshChunk();
                }

                chunk.AddTriangle(tri);
            }

            chunk.Emit(doc, meshColor);
        }

        /// <summary>
        /// Accumulates welded vertices and triangles for one <see cref="PolyfaceMesh"/> entity, then
        /// derives per-edge visibility from the angle between adjacent faces before emitting.
        /// </summary>
        private sealed class MeshChunk
        {
            private readonly Dictionary<(long X, long Y, long Z), int> _weld =
                new Dictionary<(long X, long Y, long Z), int>();

            private readonly List<Vec3> _positions = new List<Vec3>();
            private readonly List<(int I0, int I1, int I2)> _faces = new List<(int, int, int)>();

            public int VertexCount => _positions.Count;

            public void AddTriangle(in ExtractedTriangle tri)
            {
                var i0 = Weld(tri.V0);
                var i1 = Weld(tri.V1);
                var i2 = Weld(tri.V2);

                // Welding can collapse slivers into a line; such faces carry no surface.
                if (i0 == i1 || i1 == i2 || i0 == i2)
                {
                    return;
                }

                _faces.Add(FacesAwayFromNormal(tri) ? (i0, i2, i1) : (i0, i1, i2));
            }

            /// <summary>
            /// True when the winding Navisworks handed us points the derived face normal into the
            /// solid. AutoCAD has nothing but winding order to light a PolyfaceMesh face, so leaving
            /// those faces reversed makes them shade as back faces — the dark/black surfaces.
            /// </summary>
            private static bool FacesAwayFromNormal(in ExtractedTriangle tri)
            {
                var geometric = Vec3.Cross(tri.V1 - tri.V0, tri.V2 - tri.V0);
                var shading = new Vec3(
                    tri.N0.X + tri.N1.X + tri.N2.X,
                    tri.N0.Y + tri.N1.Y + tri.N2.Y,
                    tri.N0.Z + tri.N1.Z + tri.N2.Z);

                return Dot(geometric, shading) < 0.0;
            }

            public void Emit(CadDocument doc, Color meshColor)
            {
                if (_faces.Count == 0)
                {
                    return;
                }

                var mesh = new PolyfaceMesh { Color = meshColor };
                foreach (var position in _positions)
                {
                    mesh.Vertices.Add(new VertexFaceMesh(new XYZ(position.X, position.Y, position.Z)));
                }

                var hidden = HiddenEdges();
                foreach (var face in _faces)
                {
                    mesh.Faces.Add(new VertexFaceRecord
                    {
                        // A negative index hides the edge that starts at that vertex.
                        Index1 = Signed(face.I0, hidden.Contains(EdgeKey(face.I0, face.I1))),
                        Index2 = Signed(face.I1, hidden.Contains(EdgeKey(face.I1, face.I2))),
                        Index3 = Signed(face.I2, hidden.Contains(EdgeKey(face.I2, face.I0))),
                        Index4 = 0,
                    });
                }

                doc.Entities.Add(mesh);
            }

            /// <summary>
            /// Edges shared by two nearly-coplanar faces — i.e. triangulation seams rather than
            /// modelled creases.
            /// </summary>
            private HashSet<(int, int)> HiddenEdges()
            {
                var normals = new List<Vec3>(_faces.Count);
                foreach (var face in _faces)
                {
                    var a = _positions[face.I0 - 1];
                    var b = _positions[face.I1 - 1];
                    var c = _positions[face.I2 - 1];
                    normals.Add(Vec3.Cross(b - a, c - a).NormalizedOr(Vec3.UnitZ));
                }

                var adjacency = new Dictionary<(int, int), int>();
                var hidden = new HashSet<(int, int)>();
                var cosThreshold = Math.Cos(CreaseAngleDegrees * Math.PI / 180.0);

                for (var f = 0; f < _faces.Count; f++)
                {
                    var face = _faces[f];
                    Visit(EdgeKey(face.I0, face.I1), f);
                    Visit(EdgeKey(face.I1, face.I2), f);
                    Visit(EdgeKey(face.I2, face.I0), f);
                }

                void Visit((int, int) key, int faceIndex)
                {
                    if (!adjacency.TryGetValue(key, out var other))
                    {
                        adjacency[key] = faceIndex;
                        return;
                    }

                    var dot = Dot(normals[other], normals[faceIndex]);
                    if (dot >= cosThreshold)
                    {
                        hidden.Add(key);
                    }
                }

                return hidden;
            }

            private int Weld(in Vec3 position)
            {
                var key = Quantize(position);
                if (_weld.TryGetValue(key, out var existing))
                {
                    return existing;
                }

                _positions.Add(position);
                var index = _positions.Count; // 1-based, as PolyfaceMesh expects
                _weld[key] = index;
                return index;
            }
        }

        private static short Signed(int index, bool hiddenEdge) =>
            (short)(hiddenEdge ? -index : index);

        private static (int, int) EdgeKey(int a, int b) => a < b ? (a, b) : (b, a);

        private static double Dot(in Vec3 a, in Vec3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

        private static (long X, long Y, long Z) Quantize(in Vec3 v)
        {
            var s = 1.0 / WeldEpsilon;
            return (
                (long)Math.Round(v.X * s),
                (long)Math.Round(v.Y * s),
                (long)Math.Round(v.Z * s));
        }

        private static void SetActiveViewportShaded(CadDocument doc)
        {
            foreach (VPort vport in doc.VPorts)
            {
                vport.RenderMode = RenderMode.GouraudShaded;
            }
        }

        /// <summary>
        /// Records what each source intensity became, so a color complaint can be checked against the
        /// item color shown in Navisworks without re-running the export under a debugger.
        /// </summary>
        private static void LogPalette(
            IReadOnlyDictionary<(byte R, byte G, byte B), Rgba> palette,
            Action<string>? log)
        {
            if (log == null)
            {
                return;
            }

            log($"palette: {palette.Count} distinct color(s)");

            var shown = 0;
            foreach (var pair in palette)
            {
                if (shown++ == 16)
                {
                    log("  (further colors omitted)");
                    break;
                }

                var linear = pair.Value;
                log($"  linear {linear.R:0.###},{linear.G:0.###},{linear.B:0.###}" +
                    $" -> RGB {pair.Key.R},{pair.Key.G},{pair.Key.B}");
            }
        }

        /// <summary>
        /// Averaged in double: the three vertex colors are normally identical, and averaging them in
        /// float drifts far enough to shift the encoded byte by one.
        /// </summary>
        private static (byte R, byte G, byte B) AverageFaceRgb(in Rgba c0, in Rgba c1, in Rgba c2)
        {
            return (
                ToByte(((double)c0.R + c1.R + c2.R) / 3.0),
                ToByte(((double)c0.G + c1.G + c2.G) / 3.0),
                ToByte(((double)c0.B + c1.B + c2.B) / 3.0));
        }

        /// <summary>
        /// Navisworks reports channel intensities as linear light. The GLB writer can forward them
        /// untouched because glTF COLOR_0 is defined as linear and the viewer encodes it on display;
        /// a DWG true color is already a display value, so the same sRGB transfer function has to be
        /// applied here or every surface lands about one stop too dark.
        /// </summary>
        private static byte ToByte(double linear)
        {
            if (linear <= 0.0)
            {
                return 0;
            }

            if (linear >= 1.0)
            {
                return 255;
            }

            var encoded = linear <= 0.0031308
                ? linear * 12.92
                : (1.055 * Math.Pow(linear, 1.0 / 2.4)) - 0.055;

            return (byte)Math.Round(encoded * 255.0);
        }
    }
}
