using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Autodesk.Navisworks.Api;
using Autodesk.Navisworks.Api.ComApi;
using ComApi = Autodesk.Navisworks.Api.Interop.ComApi;

namespace NavisworksExport.Geometry
{
    /// <summary>
    /// Reads triangle geometry from a Navisworks selection via COM interop.
    /// Document/selection are never mutated.
    /// </summary>
    public sealed class SelectionGeometryExtractor
    {
        private static readonly IntArrayComparer PathComparer = new IntArrayComparer();

        public IReadOnlyList<ExtractedTriangle> Extract(ModelItemCollection selection, Action<string>? log = null)
        {
            var fragments = ExtractGrouped(selection, log);
            if (fragments.Count == 0)
            {
                return Array.Empty<ExtractedTriangle>();
            }

            var result = new List<ExtractedTriangle>();
            foreach (var fragment in fragments)
            {
                result.AddRange(fragment.Triangles);
            }

            return result;
        }

        /// <summary>
        /// Same extraction pipeline as <see cref="Extract"/>, but preserves fragment boundaries
        /// so writers can emit one mesh (or chunked meshes) per fragment occurrence.
        /// </summary>
        public IReadOnlyList<ExtractedFragment> ExtractGrouped(ModelItemCollection selection, Action<string>? log = null)
        {
            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            var result = new List<ExtractedFragment>();
            if (selection.Count == 0)
            {
                return result;
            }

            // Ensure COM bridge is initialized before crossing the managed/COM boundary.
            _ = ComApiBridge.State;

            var leaves = GeometryLeaves(selection);
            log?.Invoke($"selection {selection.Count} item(s) -> {leaves.Count} geometry leaf/leaves");

            var comSelection = ComApiBridge.ToInwOpSelection(leaves);
            var pathCount = 0;
            var fragmentCount = 0;
            var skippedFragments = 0;
            var triangleCount = 0;
            // Local-space triangles keyed by shared COM Geometry body (reference equality).
            // Fragment path.ArrayData identifies the instance (used for selection filtering);
            // the mesh body is shared across instances and is the correct GenerateSimplePrimitives dedup key.
            var localCache = new Dictionary<object, IReadOnlyList<LocalTriangle>>(new ReferenceEqualityComparer());
            var callback = new PrimitiveCallback();
            // Manage 2026 raises "Not implemented" for InwOaFragment3.Geometry; dedup is then skipped.
            var geometryDedupSupported = true;

            foreach (ComApi.InwOaPath3 path in comSelection.Paths())
            {
                pathCount++;
                var pathKey = ToIntArray(path.ArrayData);
                var itemColor = ReadItemColor(path);

                foreach (ComApi.InwOaFragment3 frag in path.Fragments())
                {
                    // Fragments() can return instances that do not belong to this path.
                    var fragPathKey = ToIntArray(frag.path.ArrayData);
                    if (!PathComparer.Equals(pathKey, fragPathKey))
                    {
                        skippedFragments++;
                        continue;
                    }

                    fragmentCount++;

                    // Resolve colour per path/fragment occurrence — do not bake it into the local
                    // geometry cache, or every instance of a shared body would inherit the first hit.
                    var fragmentColor = itemColor
                        ?? ReadAppearanceColor(frag)
                        ?? Rgba.Gray;

                    object? geometry = null;
                    if (geometryDedupSupported)
                    {
                        try
                        {
                            geometry = frag.Geometry;
                        }
                        catch (COMException)
                        {
                            geometryDedupSupported = false;
                        }
                    }

                    IReadOnlyList<LocalTriangle> localTriangles;

                    if (geometry != null && localCache.TryGetValue(geometry, out var cached))
                    {
                        localTriangles = cached;
                    }
                    else
                    {
                        // Fallback colour inside the callback is unused for export colouring (we
                        // overwrite below) but keeps the DTO populated if a caller reads locals.
                        callback.Reset(fragmentColor);
                        frag.GenerateSimplePrimitives(
                            ComApi.nwEVertexProperty.eNORMAL | ComApi.nwEVertexProperty.eCOLOR,
                            callback);
                        localTriangles = new List<LocalTriangle>(callback.Triangles);
                        if (geometry != null)
                        {
                            localCache[geometry] = localTriangles;
                        }
                    }

                    if (localTriangles.Count == 0)
                    {
                        continue;
                    }

                    var matrix = ReadMatrix(frag.GetLocalToWorldMatrix());
                    var worldTriangles = new List<ExtractedTriangle>(localTriangles.Count);
                    foreach (var local in localTriangles)
                    {
                        worldTriangles.Add(new ExtractedTriangle(
                            Transform(local.V0, matrix),
                            Transform(local.V1, matrix),
                            Transform(local.V2, matrix),
                            TransformNormal(local.N0, matrix),
                            TransformNormal(local.N1, matrix),
                            TransformNormal(local.N2, matrix),
                            fragmentColor,
                            fragmentColor,
                            fragmentColor));
                    }

                    triangleCount += worldTriangles.Count;
                    result.Add(new ExtractedFragment(worldTriangles));
                }
            }

            log?.Invoke(
                $"paths {pathCount}, fragments {fragmentCount} (skipped {skippedFragments}), " +
                $"fragment groups {result.Count}, triangles {triangleCount}");

            return result;
        }

        /// <summary>
        /// Expands the selection to the geometry-bearing nodes underneath it. A path to a composite
        /// node yields fragments that belong to its children, and those are rejected by the
        /// path-identity filter below — so without this step whole branches export as nothing.
        /// </summary>
        private static ModelItemCollection GeometryLeaves(ModelItemCollection selection)
        {
            var seen = new HashSet<ModelItem>();
            var leaves = new ModelItemCollection();
            foreach (var item in selection.DescendantsAndSelf)
            {
                if (item.HasGeometry && seen.Add(item))
                {
                    leaves.Add(item);
                }
            }

            // Nothing recognisable as geometry: let the original selection speak for itself.
            return leaves.Count > 0 ? leaves : selection;
        }

        /// <summary>
        /// Colour as currently displayed for the item (honours appearance overrides). Preferred over
        /// the COM material because it is what the user sees in the host.
        /// Falls back through PermanentColor → OriginalColor when ActiveColor is missing.
        /// </summary>
        private static Rgba? ReadItemColor(ComApi.InwOaPath3 path)
        {
            try
            {
                var geometry = ComApiBridge.ToModelItem(path)?.Geometry;
                if (geometry == null)
                {
                    return null;
                }

                if (TryFromNwColor(geometry.ActiveColor, geometry.ActiveTransparency, out var active))
                {
                    return active;
                }

                if (TryFromNwColor(geometry.PermanentColor, geometry.PermanentTransparency, out var permanent))
                {
                    return permanent;
                }

                if (TryFromNwColor(geometry.OriginalColor, geometry.OriginalTransparency, out var original))
                {
                    return original;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryFromNwColor(
            Autodesk.Navisworks.Api.Color color,
            double transparency,
            out Rgba rgba)
        {
            rgba = default;
            if (color == null)
            {
                return false;
            }

            // Navisworks Color channels are 0..1 doubles.
            var r = (float)color.R;
            var g = (float)color.G;
            var b = (float)color.B;
            if (float.IsNaN(r) || float.IsNaN(g) || float.IsNaN(b))
            {
                return false;
            }

            rgba = new Rgba(r, g, b, (float)(1.0 - transparency));
            return true;
        }

        private static Rgba? ReadAppearanceColor(ComApi.InwOaFragment3 frag)
        {
            try
            {
                if (frag.Appearance is ComApi.InwOaMaterial material)
                {
                    var diffuse = material.DiffuseColor;
                    var alpha = 1.0 - material.transparency;
                    return new Rgba(
                        (float)diffuse.data1,
                        (float)diffuse.data2,
                        (float)diffuse.data3,
                        (float)alpha);
                }
            }
            catch
            {
                // Fall through to the caller's default.
            }

            return null;
        }

        private static double[] ReadMatrix(ComApi.InwLTransform3f transform)
        {
            var matrix = (Array)transform.Matrix;
            var values = new double[16];
            var lo = matrix.GetLowerBound(0);
            for (var i = 0; i < 16; i++)
            {
                values[i] = Convert.ToDouble(matrix.GetValue(lo + i));
            }

            return values;
        }

        /// <summary>
        /// Applies a 4x4 local-to-world matrix. Navisworks COM stores it column-major, so the
        /// translation lives at indices 12/13/14 and each basis vector is strided by four.
        /// </summary>
        private static Vec3 Transform(in Vec3 point, double[] m)
        {
            return new Vec3(
                m[0] * point.X + m[4] * point.Y + m[8] * point.Z + m[12],
                m[1] * point.X + m[5] * point.Y + m[9] * point.Z + m[13],
                m[2] * point.X + m[6] * point.Y + m[10] * point.Z + m[14]);
        }

        /// <summary>
        /// Rotates a normal into world space: the 3x3 part only, no translation. Fragment transforms
        /// are rigid, so the inverse transpose the general case would need is unnecessary here.
        /// </summary>
        private static Vec3 TransformNormal(in Vec3 normal, double[] m)
        {
            return new Vec3(
                m[0] * normal.X + m[4] * normal.Y + m[8] * normal.Z,
                m[1] * normal.X + m[5] * normal.Y + m[9] * normal.Z,
                m[2] * normal.X + m[6] * normal.Y + m[10] * normal.Z).NormalizedOr(Vec3.UnitZ);
        }

        private static int[] ToIntArray(object? arrayData)
        {
            if (arrayData is not Array array)
            {
                return Array.Empty<int>();
            }

            var lo = array.GetLowerBound(0);
            var hi = array.GetUpperBound(0);
            var result = new int[hi - lo + 1];
            for (var i = lo; i <= hi; i++)
            {
                result[i - lo] = Convert.ToInt32(array.GetValue(i));
            }

            return result;
        }

        private sealed class IntArrayComparer : IEqualityComparer<int[]>
        {
            public bool Equals(int[]? x, int[]? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null || x.Length != y.Length)
                {
                    return false;
                }

                for (var i = 0; i < x.Length; i++)
                {
                    if (x[i] != y[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            public int GetHashCode(int[] obj)
            {
                unchecked
                {
                    var hash = 17;
                    foreach (var value in obj)
                    {
                        hash = hash * 31 + value;
                    }

                    return hash;
                }
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
