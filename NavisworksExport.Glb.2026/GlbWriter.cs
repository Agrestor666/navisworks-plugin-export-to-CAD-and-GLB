using System;
using System.Collections.Generic;
using System.Numerics;
using NavisworksExport.Geometry;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

namespace NavisworksExport.Glb
{
    /// <summary>
    /// Writes world-space extracted triangles to a single-mesh colored .glb (Y-up, meters).
    /// </summary>
    public static class GlbWriter
    {
        /// <summary>
        /// Converts Navisworks Z-up document-space triangles to glTF Y-up meters and saves a .glb.
        /// Does not enable Draco or any other compression extension.
        /// </summary>
        public static void WriteGlb(
            IReadOnlyList<ExtractedTriangle> triangles,
            double unitsToMetersScale,
            string filePath)
        {
            if (triangles is null)
            {
                throw new ArgumentNullException(nameof(triangles));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("A non-empty file path is required.", nameof(filePath));
            }

            if (unitsToMetersScale <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(unitsToMetersScale),
                    unitsToMetersScale,
                    "Unit scale must be positive.");
            }

            var scale = (float)unitsToMetersScale;
            // BaseColor white so COLOR_0 vertex colors show through (SharpGLTF multiplies them).
            // Metallic must be forced to 0: glTF defaults it to 1, which renders every surface black
            // in viewers without an environment map (PowerPoint included).
            var material = new MaterialBuilder("vertex-color")
                .WithDoubleSide(true)
                .WithMetallicRoughnessShader()
                .WithBaseColor(Vector4.One)
                .WithMetallicRoughness(0f, 0.8f);

            var mesh = new MeshBuilder<VertexPositionNormal, VertexColor1, VertexEmpty>("selection");
            var prim = mesh.UsePrimitive(material);

            foreach (var tri in triangles)
            {
                prim.AddTriangle(
                    ToVertex(tri.V0, tri.N0, tri.C0, scale),
                    ToVertex(tri.V1, tri.N1, tri.C1, scale),
                    ToVertex(tri.V2, tri.N2, tri.C2, scale));
            }

            var scene = new SceneBuilder();
            scene.AddRigidMesh(mesh, Matrix4x4.Identity);
            scene.ToGltf2().SaveGLB(filePath);
        }

        /// <summary>
        /// Navisworks Z-up → glTF Y-up (right-handed): (x, y, z) → (x, z, -y), then scale to meters.
        /// The same swap applies to the normal, which is a rotation and so stays unit length.
        /// </summary>
        private static (VertexPositionNormal, VertexColor1) ToVertex(
            in Vec3 nw, in Vec3 normal, in Rgba color, float scale)
        {
            var geometry = new VertexPositionNormal(
                new Vector3(
                    (float)(nw.X * scale),
                    (float)(nw.Z * scale),
                    (float)(-nw.Y * scale)),
                new Vector3(
                    (float)normal.X,
                    (float)normal.Z,
                    (float)-normal.Y));
            var vertexColor = new VertexColor1(new Vector4(color.R, color.G, color.B, color.A));
            return (geometry, vertexColor);
        }
    }
}
