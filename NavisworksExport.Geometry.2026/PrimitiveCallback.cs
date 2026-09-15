using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;

namespace NavisworksExport.Geometry
{
    /// <summary>
    /// COM callback that accumulates local-space triangles from GenerateSimplePrimitives.
    /// Lines, points, and snap points are ignored (mesh-only MVP).
    /// Must be public so Navisworks can marshal the COM callable wrapper (CCW).
    /// </summary>
    [ComVisible(true)]
    public class PrimitiveCallback : ComApi.InwSimplePrimitivesCB
    {
        private readonly List<LocalTriangle> _triangles = new List<LocalTriangle>();
        private Rgba _fallbackColor = Rgba.Gray;

        internal IReadOnlyList<LocalTriangle> Triangles => _triangles;

        internal void Reset(Rgba fallbackColor)
        {
            _triangles.Clear();
            _fallbackColor = fallbackColor;
        }

        public void Triangle(ComApi.InwSimpleVertex v1, ComApi.InwSimpleVertex v2, ComApi.InwSimpleVertex v3)
        {
            var p0 = ReadCoord(v1);
            var p1 = ReadCoord(v2);
            var p2 = ReadCoord(v3);

            // Flat normal of this triangle, used wherever the host supplies no usable vertex normal.
            var faceNormal = Vec3.Cross(p1 - p0, p2 - p0).NormalizedOr(Vec3.UnitZ);

            // InwSimpleVertex.color comes back zeroed even with eCOLOR requested, so the fragment's
            // own colour is the only usable source.
            _triangles.Add(new LocalTriangle(
                p0, p1, p2,
                ReadNormal(v1, faceNormal), ReadNormal(v2, faceNormal), ReadNormal(v3, faceNormal),
                _fallbackColor, _fallbackColor, _fallbackColor));
        }

        public void Line(ComApi.InwSimpleVertex v1, ComApi.InwSimpleVertex v2)
        {
        }

        public void Point(ComApi.InwSimpleVertex v1)
        {
        }

        public void SnapPoint(ComApi.InwSimpleVertex v1)
        {
        }

        private static Vec3 ReadCoord(ComApi.InwSimpleVertex vertex)
        {
            var coords = (Array)vertex.coord;
            return new Vec3(
                Convert.ToDouble(coords.GetValue(coords.GetLowerBound(0))),
                Convert.ToDouble(coords.GetValue(coords.GetLowerBound(0) + 1)),
                Convert.ToDouble(coords.GetValue(coords.GetLowerBound(0) + 2)));
        }

        private static Vec3 ReadNormal(ComApi.InwSimpleVertex vertex, in Vec3 fallback)
        {
            try
            {
                if (vertex.normal is Array normal && normal.Length >= 3)
                {
                    var lo = normal.GetLowerBound(0);
                    return new Vec3(
                        Convert.ToDouble(normal.GetValue(lo)),
                        Convert.ToDouble(normal.GetValue(lo + 1)),
                        Convert.ToDouble(normal.GetValue(lo + 2))).NormalizedOr(fallback);
                }
            }
            catch
            {
                // Fall through to the flat face normal.
            }

            return fallback;
        }
    }

    internal readonly struct LocalTriangle
    {
        public LocalTriangle(
            in Vec3 v0, in Vec3 v1, in Vec3 v2,
            in Vec3 n0, in Vec3 n1, in Vec3 n2,
            in Rgba c0, in Rgba c1, in Rgba c2)
        {
            V0 = v0;
            V1 = v1;
            V2 = v2;
            N0 = n0;
            N1 = n1;
            N2 = n2;
            C0 = c0;
            C1 = c1;
            C2 = c2;
        }

        public Vec3 V0 { get; }
        public Vec3 V1 { get; }
        public Vec3 V2 { get; }
        public Vec3 N0 { get; }
        public Vec3 N1 { get; }
        public Vec3 N2 { get; }
        public Rgba C0 { get; }
        public Rgba C1 { get; }
        public Rgba C2 { get; }
    }
}
