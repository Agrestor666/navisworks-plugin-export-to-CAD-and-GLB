using System;
using System.Collections.Generic;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;

namespace NavisworksExport.Geometry
{
    /// <summary>
    /// COM callback that accumulates local-space triangles from GenerateSimplePrimitives.
    /// Lines, points, and snap points are ignored (mesh-only MVP).
    /// </summary>
    internal sealed class PrimitiveCallback : ComApi.InwSimplePrimitivesCB
    {
        private readonly List<LocalTriangle> _triangles = new List<LocalTriangle>();
        private Rgba _fallbackColor = Rgba.Gray;

        public IReadOnlyList<LocalTriangle> Triangles => _triangles;

        public void Reset(Rgba fallbackColor)
        {
            _triangles.Clear();
            _fallbackColor = fallbackColor;
        }

        public void Triangle(ComApi.InwSimpleVertex v1, ComApi.InwSimpleVertex v2, ComApi.InwSimpleVertex v3)
        {
            var p0 = ReadCoord(v1);
            var p1 = ReadCoord(v2);
            var p2 = ReadCoord(v3);
            var c0 = ReadColor(v1, _fallbackColor);
            var c1 = ReadColor(v2, _fallbackColor);
            var c2 = ReadColor(v3, _fallbackColor);
            _triangles.Add(new LocalTriangle(p0, p1, p2, c0, c1, c2));
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

        private static Rgba ReadColor(ComApi.InwSimpleVertex vertex, Rgba fallback)
        {
            try
            {
                if (vertex.color is Array colors && colors.Length >= 3)
                {
                    var lo = colors.GetLowerBound(0);
                    var r = Convert.ToSingle(colors.GetValue(lo));
                    var g = Convert.ToSingle(colors.GetValue(lo + 1));
                    var b = Convert.ToSingle(colors.GetValue(lo + 2));
                    var a = colors.Length >= 4 ? Convert.ToSingle(colors.GetValue(lo + 3)) : 1f;
                    return new Rgba(r, g, b, a);
                }
            }
            catch
            {
                // Fall through to fragment Appearance / gray.
            }

            return fallback;
        }
    }

    internal readonly struct LocalTriangle
    {
        public LocalTriangle(in Vec3 v0, in Vec3 v1, in Vec3 v2, in Rgba c0, in Rgba c1, in Rgba c2)
        {
            V0 = v0;
            V1 = v1;
            V2 = v2;
            C0 = c0;
            C1 = c1;
            C2 = c2;
        }

        public Vec3 V0 { get; }
        public Vec3 V1 { get; }
        public Vec3 V2 { get; }
        public Rgba C0 { get; }
        public Rgba C1 { get; }
        public Rgba C2 { get; }
    }
}
