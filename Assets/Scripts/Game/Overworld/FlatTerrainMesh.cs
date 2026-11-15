using System;
using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HunterSystem.Game
{
    /// <summary>What a triangle of the shown ground is drawn as: one material each.</summary>
    public enum TerrainSurface
    {
        Grass = 0,
        Rock = 1,
        Climbable = 2,
        Sand = 3,
        Road = 4,
    }

    /// <summary>
    /// The overworld as players see it (ADR 0002, ticket 39): a flat-shaded low-poly mesh per tile, built
    /// from the same baked ground the simulation walks on, so the art and the rules cannot drift. Every
    /// triangle has its own three vertices and one face normal, so stock URP shaders draw hard facets.
    /// </summary>
    public static class FlatTerrainMesh
    {
        public static readonly int SurfaceCount = Enum.GetValues(typeof(TerrainSurface)).Length;

        /// <summary>
        /// A mesh for the square from (<paramref name="minX"/>, <paramref name="minZ"/>) of side <paramref name="size"/>,
        /// with quads <paramref name="cellSize"/> wide, in local coordinates from that corner. One submesh per
        /// <see cref="TerrainSurface"/>, chosen per triangle by <paramref name="surfaceAt"/> at its middle.
        /// </summary>
        public static Mesh Build(IGround ground, float minX, float minZ, float size, float cellSize, Func<Vector3, TerrainSurface> surfaceAt)
        {
            if (ground == null) throw new ArgumentNullException(nameof(ground));
            if (cellSize <= 0f || size <= 0f) throw new ArgumentOutOfRangeException(nameof(cellSize));
            var cells = Mathf.Max(1, Mathf.RoundToInt(size / cellSize));
            var step = size / cells;

            var vertices = new List<Vector3>(cells * cells * 6);
            var normals = new List<Vector3>(cells * cells * 6);
            var triangles = new List<int>[SurfaceCount];
            for (var s = 0; s < SurfaceCount; s++) triangles[s] = new List<int>();

            Vector3 Corner(int i, int j)
            {
                var x = minX + i * step;
                var z = minZ + j * step;
                return new Vector3(i * step, ground.HeightAt(new Vec3(x, 0f, z)), j * step);
            }

            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var middle = (a + b + c) / 3f + new Vector3(minX, 0f, minZ);
                var list = triangles[(int)surfaceAt(middle)];
                list.Add(vertices.Count);
                list.Add(vertices.Count + 1);
                list.Add(vertices.Count + 2);
                vertices.Add(a);
                vertices.Add(b);
                vertices.Add(c);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
            }

            for (var j = 0; j < cells; j++)
            for (var i = 0; i < cells; i++)
            {
                var a = Corner(i, j);
                var b = Corner(i + 1, j);
                var c = Corner(i, j + 1);
                var d = Corner(i + 1, j + 1);
                // Clockwise seen from above, so the faces point up in Unity.
                Triangle(a, c, b);
                Triangle(b, c, d);
            }

            var mesh = new Mesh { name = "Flat terrain", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.subMeshCount = SurfaceCount;
            for (var s = 0; s < SurfaceCount; s++) mesh.SetTriangles(triangles[s], s);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// The usual choice of surface: the ground's kind (rock where it cannot be walked, climbable faces their
        /// own), a road within its width, sand just above the sea, grass otherwise.
        /// </summary>
        public static TerrainSurface SurfaceAt(IGround ground, Vector3 at, float seaLevel, float sandAboveSea, Func<float, float, float> distanceToRoad)
        {
            var kind = ground.KindAt(new Vec3(at.x, 0f, at.z));
            if (kind == GroundKind.Climbable) return TerrainSurface.Climbable;
            if (kind == GroundKind.Blocked) return TerrainSurface.Rock;
            if (at.y < seaLevel + sandAboveSea) return TerrainSurface.Sand;
            if (distanceToRoad != null && distanceToRoad(at.x, at.z) <= 0f) return TerrainSurface.Road;
            return TerrainSurface.Grass;
        }
    }
}
