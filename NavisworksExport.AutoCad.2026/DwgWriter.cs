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
    public static class DwgWriter
    {
        /// <summary>
        /// PolyfaceMesh face indices are 16-bit; stay under short.MaxValue vertices per entity.
        /// </summary>
        private const int MaxVerticesPerMesh = 32_767;

        public static void WriteDwg(IReadOnlyList<ExtractedFragment> fragments, string filePath)
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
                if (fragment?.Triangles != null)
                {
                    triangleCount += fragment.Triangles.Count;
                }
            }

            if (triangleCount == 0)
            {
                throw new InvalidOperationException(
                    "Cannot write a DWG with no mesh triangles.");
            }

            var doc = new CadDocument();
            doc.Header.Version = ACadVersion.AC1032;

            foreach (var fragment in fragments)
            {
                if (fragment?.Triangles == null || fragment.Triangles.Count == 0)
                {
                    continue;
                }

                AppendFragmentMeshes(doc, fragment.Triangles);
            }

            SetActiveViewportShaded(doc);

            using (var writer = new ACadDwgWriter(filePath, doc))
            {
                writer.Write();
            }
        }

        private static void AppendFragmentMeshes(CadDocument doc, IReadOnlyList<ExtractedTriangle> triangles)
        {
            PolyfaceMesh? mesh = null;
            var vertexCount = 0;

            foreach (var tri in triangles)
            {
                if (mesh == null || vertexCount + 3 > MaxVerticesPerMesh)
                {
                    if (mesh != null)
                    {
                        doc.Entities.Add(mesh);
                    }

                    mesh = new PolyfaceMesh();
                    vertexCount = 0;
                }

                var i0 = (short)(vertexCount + 1);
                var i1 = (short)(vertexCount + 2);
                var i2 = (short)(vertexCount + 3);

                mesh.Vertices.Add(new VertexFaceMesh(ToXyz(tri.V0)));
                mesh.Vertices.Add(new VertexFaceMesh(ToXyz(tri.V1)));
                mesh.Vertices.Add(new VertexFaceMesh(ToXyz(tri.V2)));
                vertexCount += 3;

                var face = new VertexFaceRecord
                {
                    Index1 = i0,
                    Index2 = i1,
                    Index3 = i2,
                    Index4 = 0,
                    Color = AverageFaceColor(tri.C0, tri.C1, tri.C2),
                };
                mesh.Faces.Add(face);
            }

            if (mesh != null && vertexCount > 0)
            {
                doc.Entities.Add(mesh);
            }
        }

        private static void SetActiveViewportShaded(CadDocument doc)
        {
            foreach (VPort vport in doc.VPorts)
            {
                vport.RenderMode = RenderMode.GouraudShaded;
            }
        }

        private static XYZ ToXyz(in Vec3 v) => new XYZ(v.X, v.Y, v.Z);

        private static Color AverageFaceColor(in Rgba c0, in Rgba c1, in Rgba c2)
        {
            var r = ToByte((c0.R + c1.R + c2.R) / 3f);
            var g = ToByte((c0.G + c1.G + c2.G) / 3f);
            var b = ToByte((c0.B + c1.B + c2.B) / 3f);
            return new Color(r, g, b);
        }

        private static byte ToByte(float channel01)
        {
            if (channel01 <= 0f)
            {
                return 0;
            }

            if (channel01 >= 1f)
            {
                return 255;
            }

            return (byte)Math.Round(channel01 * 255f);
        }
    }
}
