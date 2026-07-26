using System;

namespace NavisworksExport.Geometry
{
    /// <summary>
    /// One triangle in world space (document units), with per-vertex unit normal and RGBA color.
    /// Format-agnostic — no glTF/DWG types.
    /// </summary>
    public readonly struct ExtractedTriangle
    {
        public ExtractedTriangle(
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

    public readonly struct Vec3
    {
        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public static Vec3 UnitZ { get; } = new Vec3(0, 0, 1);

        public static Vec3 operator -(in Vec3 a, in Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec3 Cross(in Vec3 a, in Vec3 b) => new Vec3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        /// <summary>
        /// Unit-length copy, or <paramref name="fallback"/> when this vector has no usable direction.
        /// </summary>
        public Vec3 NormalizedOr(in Vec3 fallback)
        {
            var length = Math.Sqrt((X * X) + (Y * Y) + (Z * Z));
            return length > 1e-12
                ? new Vec3(X / length, Y / length, Z / length)
                : fallback;
        }
    }

    public readonly struct Rgba
    {
        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public float R { get; }
        public float G { get; }
        public float B { get; }
        public float A { get; }

        public static Rgba Gray { get; } = new Rgba(0.7f, 0.7f, 0.7f, 1f);
    }
}
