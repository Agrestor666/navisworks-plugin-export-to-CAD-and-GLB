using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

        public IReadOnlyList<ExtractedTriangle> Extract(ModelItemCollection selection)
        {
            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            var result = new List<ExtractedTriangle>();
            if (selection.Count == 0)
            {
                return result;
            }

            var comSelection = ComApiBridge.ToInwOpSelection(selection);
            // Local-space triangles keyed by shared COM Geometry body (reference equality).
            // Fragment path.ArrayData identifies the instance (used for selection filtering);
            // the mesh body is shared across instances and is the correct GenerateSimplePrimitives dedup key.
            var localCache = new Dictionary<object, IReadOnlyList<LocalTriangle>>(new ReferenceEqualityComparer());
            var callback = new PrimitiveCallback();

            foreach (ComApi.InwOaPath3 path in comSelection.Paths())
            {
                var pathKey = ToIntArray(path.ArrayData);

                foreach (ComApi.InwOaFragment3 frag in path.Fragments())
                {
                    // Fragments() can return instances that do not belong to this path.
                    var fragPathKey = ToIntArray(frag.path.ArrayData);
                    if (!PathComparer.Equals(pathKey, fragPathKey))
                    {
                        continue;
                    }

                    var geometry = frag.Geometry;
                    IReadOnlyList<LocalTriangle> localTriangles;

                    if (geometry != null && localCache.TryGetValue(geometry, out var cached))
                    {
                        localTriangles = cached;
                    }
                    else
                    {
                        var fallback = ReadAppearanceColor(frag);
                        callback.Reset(fallback);
                        frag.GenerateSimplePrimitives(
                            ComApi.nwEVertexProperty.eNORMAL | ComApi.nwEVertexProperty.eCOLOR,
                            callback);
                        localTriangles = new List<LocalTriangle>(callback.Triangles);
                        if (geometry != null)
                        {
                            localCache[geometry] = localTriangles;
                        }
                    }

                    var matrix = ReadMatrix(frag.GetLocalToWorldMatrix());
                    foreach (var local in localTriangles)
                    {
                        result.Add(new ExtractedTriangle(
                            Transform(local.V0, matrix),
                            Transform(local.V1, matrix),
                            Transform(local.V2, matrix),
                            local.C0,
                            local.C1,
                            local.C2));
                    }
                }
            }

            return result;
        }

        private static Rgba ReadAppearanceColor(ComApi.InwOaFragment3 frag)
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
                // Fall through to gray.
            }

            return Rgba.Gray;
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
        /// Applies a row-major 4x4 local-to-world matrix (Navisworks COM layout).
        /// </summary>
        private static Vec3 Transform(in Vec3 point, double[] m)
        {
            return new Vec3(
                m[0] * point.X + m[1] * point.Y + m[2] * point.Z + m[3],
                m[4] * point.X + m[5] * point.Y + m[6] * point.Z + m[7],
                m[8] * point.X + m[9] * point.Y + m[10] * point.Z + m[11]);
        }

        private static int[] ToIntArray(object arrayData)
        {
            var array = (Array)arrayData;
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
