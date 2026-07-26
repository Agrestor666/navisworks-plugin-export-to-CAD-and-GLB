using System;
using System.Collections.Generic;
using NavisworksExport.Geometry;

using Edge = System.ValueTuple<System.ValueTuple<long, long, long>, System.ValueTuple<long, long, long>>;

namespace NavisworksExport.AutoCad2026
{
    /// <summary>
    /// Splits triangles across curved surfaces so a normal-less PolyfaceMesh still reads as round.
    /// </summary>
    /// <remarks>
    /// GLB can forward Navisworks' coarse tessellation untouched because glTF carries per-vertex
    /// normals and the viewer shades between them. A PolyfaceMesh stores no normals, so AutoCAD
    /// flat-shades every facet and an elbow tessellated into a handful of steps renders as a prism.
    /// The only lever left is geometry: split each edge whose endpoint normals disagree and lift the
    /// new vertex onto the surface those normals describe (Phong tessellation). Edges on flat surfaces
    /// have matching normals and are never split, which is what keeps the growth affordable.
    /// </remarks>
    internal static class CurvedSurfaceRefiner
    {
        /// <summary>
        /// Split an edge once its endpoint normals diverge by more than this. Each pass halves the
        /// angle, so the loop terminates on its own — a coarse elbow is refined several times while
        /// an already-fine tube is refined once or not at all.
        /// </summary>
        private const double SplitAngleDegrees = 10.0;

        private const int MaxPasses = 4;

        /// <summary>
        /// Ceiling on triangles handed to the writer. A pass that would cross it is discarded whole
        /// rather than truncated, so the mesh never ends up refined on one side of an object only.
        /// </summary>
        private const int TriangleBudget = 600_000;

        /// <summary>
        /// How much of the tangent-plane projection to apply. Phong tessellation's full projection
        /// (1.0) bulges past a circular arc by about as much as the flat midpoint falls short of it;
        /// half of it lands on the arc to within a few parts in ten thousand at the facet angles
        /// Navisworks emits.
        /// </summary>
        private const double ProjectionWeight = 0.5;

        /// <summary>Position quantum for recognising that two triangles share an edge.</summary>
        private const double WeldEpsilon = 1e-6;

        public static IReadOnlyList<ExtractedFragment> Refine(
            IReadOnlyList<ExtractedFragment> fragments,
            Action<string>? log)
        {
            var current = fragments;
            var count = CountTriangles(current);
            var before = count;
            var passes = 0;

            while (passes < MaxPasses && count < TriangleBudget)
            {
                var next = RefineOnce(current, out var splitEdges);
                if (splitEdges == 0)
                {
                    break;
                }

                var nextCount = CountTriangles(next);
                if (nextCount > TriangleBudget)
                {
                    break;
                }

                current = next;
                count = nextCount;
                passes++;
            }

            log?.Invoke($"curvature refinement: {passes} pass(es), {before} -> {count} triangles");
            return current;
        }

        private static IReadOnlyList<ExtractedFragment> RefineOnce(
            IReadOnlyList<ExtractedFragment> fragments,
            out int splitEdges)
        {
            var refined = new List<ExtractedFragment>(fragments.Count);
            splitEdges = 0;

            foreach (var fragment in fragments)
            {
                if (fragment == null)
                {
                    continue;
                }

                var triangles = fragment.Triangles;
                if (triangles == null || triangles.Count == 0)
                {
                    refined.Add(fragment);
                    continue;
                }

                var plan = PlanSplits(triangles, out var splitsHere);
                splitEdges += splitsHere;
                refined.Add(splitsHere == 0 ? fragment : new ExtractedFragment(Subdivide(triangles, plan)));
            }

            return refined;
        }

        /// <summary>
        /// Picks the edges of one fragment to split and works out where each new vertex lands.
        /// </summary>
        /// <remarks>
        /// A curved triangle has all three of its edges split, not just the ones whose normals
        /// disagree. Splitting only the diverging edges refines a tube around its circumference and
        /// leaves it alone along its length, and the long thin triangles that come out of that tilt
        /// far enough to shade as ridges — the refinement then trades one artifact for another.
        /// </remarks>
        private static Dictionary<Edge, Midpoint> PlanSplits(
            IReadOnlyList<ExtractedTriangle> triangles,
            out int splitCount)
        {
            var cosLimit = Math.Cos(SplitAngleDegrees * Math.PI / 180.0);
            var marked = new HashSet<Edge>();

            foreach (var tri in triangles)
            {
                if (Dot(tri.N0, tri.N1) >= cosLimit &&
                    Dot(tri.N1, tri.N2) >= cosLimit &&
                    Dot(tri.N2, tri.N0) >= cosLimit)
                {
                    continue;
                }

                marked.Add(KeyOf(tri.V0, tri.V1));
                marked.Add(KeyOf(tri.V1, tri.V2));
                marked.Add(KeyOf(tri.V2, tri.V0));
            }

            var plan = new Dictionary<Edge, Midpoint>(marked.Count);
            foreach (var tri in triangles)
            {
                Place(tri.V0, tri.N0, tri.V1, tri.N1);
                Place(tri.V1, tri.N1, tri.V2, tri.N2);
                Place(tri.V2, tri.N2, tri.V0, tri.N0);
            }

            splitCount = plan.Count;
            return plan;

            void Place(in Vec3 pa, in Vec3 na, in Vec3 pb, in Vec3 nb)
            {
                var key = KeyOf(pa, pb);

                // Both sides of a shared edge see the same marks, so only the lift can differ; the
                // first triangle to reach the edge fixes it for both. Letting them disagree would
                // leave a T-junction, which shows up as a hairline crack along the seam.
                if (!marked.Contains(key) || plan.ContainsKey(key))
                {
                    return;
                }

                var mid = CurvedMidpoint(pa, na, pb, nb);
                var n = (na + nb).NormalizedOr(na);
                // Store primitives only — Geometry valuetype fields on nested structs make the host's
                // GetTypes scan throw ReflectionTypeLoadException (ModuleInitializer does not run first).
                plan[key] = new Midpoint(mid.X, mid.Y, mid.Z, n.X, n.Y, n.Z);
            }
        }

        /// <summary>
        /// Emits the refined triangles: four for a fully split triangle, three or two when only some
        /// of its edges carry a new vertex.
        /// </summary>
        private static List<ExtractedTriangle> Subdivide(
            IReadOnlyList<ExtractedTriangle> triangles,
            Dictionary<Edge, Midpoint> plan)
        {
            var output = new List<ExtractedTriangle>(triangles.Count * 2);
            var v = new Corner[3];
            var m = new Corner[3];
            var split = new bool[3];

            foreach (var tri in triangles)
            {
                v[0] = ToCorner(tri.V0, tri.N0, tri.C0);
                v[1] = ToCorner(tri.V1, tri.N1, tri.C1);
                v[2] = ToCorner(tri.V2, tri.N2, tri.C2);

                var count = 0;
                for (var i = 0; i < 3; i++)
                {
                    var j = (i + 1) % 3;
                    split[i] = plan.TryGetValue(
                        KeyOf(new Vec3(v[i].PX, v[i].PY, v[i].PZ), new Vec3(v[j].PX, v[j].PY, v[j].PZ)),
                        out var midpoint);
                    if (!split[i])
                    {
                        continue;
                    }

                    var avg = Average(
                        new Rgba(v[i].R, v[i].G, v[i].B, v[i].A),
                        new Rgba(v[j].R, v[j].G, v[j].B, v[j].A));
                    m[i] = new Corner(
                        midpoint.PX, midpoint.PY, midpoint.PZ,
                        midpoint.NX, midpoint.NY, midpoint.NZ,
                        avg.R, avg.G, avg.B, avg.A);
                    count++;
                }

                switch (count)
                {
                    case 0:
                        output.Add(tri);
                        break;

                    case 1:
                    {
                        // Edge i runs from corner i to corner i+1; corner i+2 is the apex opposite it.
                        var i = split[0] ? 0 : split[1] ? 1 : 2;
                        var apex = v[(i + 2) % 3];
                        Add(output, v[i], m[i], apex);
                        Add(output, m[i], v[(i + 1) % 3], apex);
                        break;
                    }

                    case 2:
                    {
                        // Fan the quad left by the one intact edge from the corner it starts at.
                        var intact = !split[0] ? 0 : !split[1] ? 1 : 2;
                        var a = v[intact];
                        var b = v[(intact + 1) % 3];
                        var c = v[(intact + 2) % 3];
                        var bc = m[(intact + 1) % 3];
                        var ca = m[(intact + 2) % 3];
                        Add(output, a, b, bc);
                        Add(output, a, bc, ca);
                        Add(output, ca, bc, c);
                        break;
                    }

                    default:
                        Add(output, v[0], m[0], m[2]);
                        Add(output, m[0], v[1], m[1]);
                        Add(output, m[2], m[1], v[2]);
                        Add(output, m[0], m[1], m[2]);
                        break;
                }
            }

            return output;
        }

        /// <summary>
        /// Places the new vertex where the two endpoint tangent planes say the surface runs, instead
        /// of on the chord between them — this is what rounds the silhouette rather than just adding
        /// triangles to a flat facet.
        /// </summary>
        private static Vec3 CurvedMidpoint(in Vec3 pa, in Vec3 na, in Vec3 pb, in Vec3 nb)
        {
            var mid = (pa + pb) * 0.5;
            var lift = (na * -Dot(mid - pa, na)) + (nb * -Dot(mid - pb, nb));
            return mid + (lift * (0.5 * ProjectionWeight));
        }

        private static Corner ToCorner(in Vec3 p, in Vec3 n, in Rgba c) =>
            new Corner(p.X, p.Y, p.Z, n.X, n.Y, n.Z, c.R, c.G, c.B, c.A);

        private static void Add(List<ExtractedTriangle> output, in Corner a, in Corner b, in Corner c) =>
            output.Add(new ExtractedTriangle(
                new Vec3(a.PX, a.PY, a.PZ),
                new Vec3(b.PX, b.PY, b.PZ),
                new Vec3(c.PX, c.PY, c.PZ),
                new Vec3(a.NX, a.NY, a.NZ),
                new Vec3(b.NX, b.NY, b.NZ),
                new Vec3(c.NX, c.NY, c.NZ),
                new Rgba(a.R, a.G, a.B, a.A),
                new Rgba(b.R, b.G, b.B, b.A),
                new Rgba(c.R, c.G, c.B, c.A)));

        private static Rgba Average(in Rgba a, in Rgba b) => new Rgba(
            (a.R + b.R) * 0.5f,
            (a.G + b.G) * 0.5f,
            (a.B + b.B) * 0.5f,
            (a.A + b.A) * 0.5f);

        private static int CountTriangles(IReadOnlyList<ExtractedFragment> fragments)
        {
            var total = 0;
            foreach (var fragment in fragments)
            {
                total += fragment?.Triangles?.Count ?? 0;
            }

            return total;
        }

        private static Edge KeyOf(in Vec3 a, in Vec3 b)
        {
            var qa = Quantize(a);
            var qb = Quantize(b);
            return Precedes(qa, qb) ? new Edge(qa, qb) : new Edge(qb, qa);
        }

        private static bool Precedes(in (long X, long Y, long Z) a, in (long X, long Y, long Z) b)
        {
            if (a.X != b.X)
            {
                return a.X < b.X;
            }

            return a.Y != b.Y ? a.Y < b.Y : a.Z < b.Z;
        }

        private static (long X, long Y, long Z) Quantize(in Vec3 v)
        {
            var s = 1.0 / WeldEpsilon;
            return (
                (long)Math.Round(v.X * s),
                (long)Math.Round(v.Y * s),
                (long)Math.Round(v.Z * s));
        }

        private static double Dot(in Vec3 a, in Vec3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

        /// <summary>
        /// Primitive-only layout so GetTypes does not need Geometry.2026 loaded (host LoadFile scan).
        /// </summary>
        private readonly struct Midpoint
        {
            public Midpoint(double px, double py, double pz, double nx, double ny, double nz)
            {
                PX = px;
                PY = py;
                PZ = pz;
                NX = nx;
                NY = ny;
                NZ = nz;
            }

            public readonly double PX;
            public readonly double PY;
            public readonly double PZ;
            public readonly double NX;
            public readonly double NY;
            public readonly double NZ;
        }

        /// <summary>
        /// Primitive-only layout so GetTypes does not need Geometry.2026 loaded (host LoadFile scan).
        /// </summary>
        private readonly struct Corner
        {
            public Corner(
                double px, double py, double pz,
                double nx, double ny, double nz,
                float r, float g, float b, float a)
            {
                PX = px;
                PY = py;
                PZ = pz;
                NX = nx;
                NY = ny;
                NZ = nz;
                R = r;
                G = g;
                B = b;
                A = a;
            }

            public readonly double PX;
            public readonly double PY;
            public readonly double PZ;
            public readonly double NX;
            public readonly double NY;
            public readonly double NZ;
            public readonly float R;
            public readonly float G;
            public readonly float B;
            public readonly float A;
        }
    }
}

