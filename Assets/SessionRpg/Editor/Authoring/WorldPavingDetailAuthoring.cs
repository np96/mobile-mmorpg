using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>Flat, broken processional paving. No new elevation or collision.</summary>
    internal static class WorldPavingDetailAuthoring
    {
        internal static void Build(Transform visuals, AuthoringAssets assets, int seed)
        {
            var vertices = new List<Vector3>(2048);
            var normals = new List<Vector3>(2048);
            var uv = new List<Vector2>(2048);
            var indices = new[] { new List<int>(3072), new List<int>(3072), new List<int>(3072) };
            uint random = unchecked((uint)seed) ^ 0x93e726cbu;

            // Two worn shoulders establish constructed scale, while leaving the
            // central combat floor quiet. Missing blocks break the repeated rail.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int tile = 0; tile < 27; tile++)
                {
                    if ((tile + (side > 0 ? 2 : 0)) % 7 == 3) { continue; }
                    float x = -26f + tile * 2f;
                    float z = side * (7.05f + Next(ref random) * 0.12f);
                    float length = 1.82f + Next(ref random) * 0.1f;
                    float width = 0.35f + Next(ref random) * 0.08f;
                    float angle = (Next(ref random) - 0.5f) * 6f;
                    AddStone(new Vector2(x, z), length, width, angle,
                        tile % 4 == 0 ? 1 : 0, ref random, vertices, normals, uv, indices);
                }
            }

            // A few displaced edge slabs tell the same story as the broken
            // masonry. Keep the middle quiet: paired bright interior bands read
            // as modern road markings rather than weathered processional paving.
            for (int piece = 0; piece < 14; piece++)
            {
                float x = -25f + Next(ref random) * 50f;
                float z = (piece % 2 == 0 ? -1f : 1f) * (6.15f + Next(ref random) * 0.45f);
                AddStone(new Vector2(x, z), 0.65f + Next(ref random) * 0.6f,
                    0.32f + Next(ref random) * 0.18f, (Next(ref random) - 0.5f) * 44f,
                    piece % 2, ref random, vertices, normals, uv, indices);
            }

            Mesh authored = new Mesh { name = "CausewayPavingDetails_" + seed };
            authored.SetVertices(vertices);
            authored.SetNormals(normals);
            authored.SetUVs(0, uv);
            authored.subMeshCount = 3;
            for (int index = 0; index < 3; index++) { authored.SetTriangles(indices[index], index); }
            authored.RecalculateBounds();
            authored.RecalculateTangents();
            string path = AuthoringAssets.ArtFolder + "/Environment/" + authored.name + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = authored;
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.CopySerialized(authored, mesh);
                Object.DestroyImmediate(authored);
                EditorUtility.SetDirty(mesh);
            }

            GameObject root = new GameObject("CausewayPavingDetails", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(visuals, false);
            root.layer = 2;
            root.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                assets.AshenCausewayPaving, assets.AshenCausewayPaving, assets.AshenCausewayGrout,
            };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
        }

        private static void AddStone(Vector2 center, float length, float width, float angle, int material,
            ref uint random, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int>[] indices)
        {
            float cut = 0.07f + Next(ref random) * 0.09f;
            AddPatch(center, length + 0.05f, width + 0.045f, cut, angle, 0.034f,
                vertices, normals, uv, indices[2]);
            AddPatch(center, length, width, cut, angle, 0.041f,
                vertices, normals, uv, indices[material]);
        }

        private static void AddPatch(Vector2 center, float length, float width, float cut, float angle,
            float height, List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> indices)
        {
            float x = length * 0.5f;
            float z = width * 0.5f;
            Vector2[] corners =
            {
                new Vector2(-x + cut, -z), new Vector2(-x, -z + cut),
                new Vector2(-x, z - cut), new Vector2(-x + cut, z),
                new Vector2(x - cut * 1.7f, z), new Vector2(x, z - cut),
                new Vector2(x, -z + cut), new Vector2(x - cut, -z),
            };
            int start = vertices.Count;
            vertices.Add(new Vector3(center.x, height, center.y));
            normals.Add(Vector3.up);
            uv.Add(center * 0.4f);
            Quaternion rotation = Quaternion.Euler(0f, angle, 0f);
            for (int corner = 0; corner < corners.Length; corner++)
            {
                Vector3 offset = rotation * new Vector3(corners[corner].x, 0f, corners[corner].y);
                Vector3 position = new Vector3(center.x + offset.x, height, center.y + offset.z);
                vertices.Add(position);
                normals.Add(Vector3.up);
                uv.Add(new Vector2(position.x, position.z) * 0.4f);
                indices.Add(start);
                indices.Add(start + corner + 1);
                indices.Add(start + ((corner + 1) % corners.Length) + 1);
            }
        }

        private static float Next(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return (state >> 8) * (1f / 16777216f);
        }
    }
}
