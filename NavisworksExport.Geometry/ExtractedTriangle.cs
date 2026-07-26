namespace NavisworksExport.Geometry
{
    /// <summary>
    /// One triangle in world space (document units), with per-vertex RGBA color.
    /// Format-agnostic — no glTF/DWG types.
    /// </summary>
    public readonly struct ExtractedTriangle
    {
        public ExtractedTriangle(
            in Vec3 v0, in Vec3 v1, in Vec3 v2,
            in Rgba c0, in Rgba c1, in Rgba c2)
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
