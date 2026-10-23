using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Loads the game's models from Resources/Models. They are generated from code by Tools/ArtGen (Blender run as a
    /// Python module) and stored in a small binary format: vertex positions, normals and colors, where the color's
    /// alpha says how much the vertex glows. Meshes are cached and shared by every instance.
    /// </summary>
    public static class ModelLibrary
    {
        public sealed class Model
        {
            public string Name;
            public Mesh Mesh;
            public Bounds Bounds;
            public Dictionary<string, Vector3> Anchors = new Dictionary<string, Vector3>();
        }

        static readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();

        /// <summary>The model with this file name (without extension), or null if it isn't there.</summary>
        public static Model Get(string name)
        {
            if (cache.TryGetValue(name, out var m)) return m;
            var asset = Resources.Load<TextAsset>("Models/" + name);
            m = asset != null ? Parse(name, asset.bytes) : null;
            if (asset == null) Debug.LogWarning($"Shards of Eternity: model {name} is missing from Resources/Models.");
            cache[name] = m;
            return m;
        }

        public static bool Has(string name) => Get(name) != null;

        /// <summary>Reads the format written by Tools/ArtGen/kit.py write_mesh. Returns null if the data is not a model.</summary>
        public static Model Parse(string name, byte[] data)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (data.Length < 48 || Encoding.ASCII.GetString(r.ReadBytes(4)) != "SOEM") return null;
                uint version = r.ReadUInt32();
                if (version != 1) return null;
                int vertexCount = (int)r.ReadUInt32();
                int indexCount = (int)r.ReadUInt32();
                var min = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var max = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                var model = new Model { Name = name };
                model.Bounds.SetMinMax(min, max);
                int anchors = (int)r.ReadUInt32();
                for (int i = 0; i < anchors; i++)
                {
                    var key = Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte()));
                    model.Anchors[key] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                }

                var positions = new Vector3[vertexCount];
                var normals = new Vector3[vertexCount];
                var colors = new Color32[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    positions[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    normals[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    colors[i] = new Color32(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                }
                var indices = new int[indexCount];
                for (int i = 0; i < indexCount; i++) indices[i] = (int)r.ReadUInt32();

                var mesh = new Mesh { name = name };
                if (vertexCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(positions);
                mesh.SetNormals(normals);
                mesh.SetColors(colors);
                mesh.SetTriangles(indices, 0, false);
                mesh.bounds = model.Bounds;
                model.Mesh = mesh;
                return model;
            }
        }

        /// <summary>A child object drawing the model with the miniature material, or null if the model is missing.</summary>
        public static Transform Spawn(string name, Transform parent, string objectName = null)
        {
            var m = Get(name);
            if (m == null) return null;
            var go = new GameObject(objectName ?? name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = m.Mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Palette.Miniature;
            return go.transform;
        }
    }
}
