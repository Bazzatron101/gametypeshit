using System.Collections.Generic;
using HunterSystem.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HunterSystem.Game
{
    /// <summary>
    /// Draws the overworld's dressing (ticket 49). Scattered nature is drawn with GPU instancing, one call per
    /// kind and submesh per loaded tile, and only on tiles within the kind's draw distance of the camera, so
    /// grass stays close and trees reach far. It comes and goes with the tiles. Buildings are ordinary scene
    /// objects, made once, and stay in view from afar.
    /// </summary>
    public sealed class EnvironmentView
    {
        private const int MaxPerCall = 1023;

        private readonly EnvironmentAsset _environment;
        private readonly float _tileSize;
        private readonly Dictionary<TileCoord, (Vector3 centre, Matrix4x4[][] props)> _tiles = new Dictionary<TileCoord, (Vector3, Matrix4x4[][])>();
        private readonly RenderParams[][] _params;
        private readonly bool[] _drawable;

        public EnvironmentView(EnvironmentAsset environment, float tileSize)
        {
            _environment = environment;
            _tileSize = tileSize;
            var kinds = environment != null ? environment.props : new EnvironmentAsset.PropKind[0];
            _params = new RenderParams[kinds.Length][];
            _drawable = new bool[kinds.Length];
            for (var k = 0; k < kinds.Length; k++)
            {
                var kind = kinds[k];
                if (kind?.mesh == null || kind.materials == null) { _params[k] = new RenderParams[0]; continue; }
                var count = Mathf.Min(kind.mesh.subMeshCount, kind.materials.Length);
                _params[k] = new RenderParams[count];
                _drawable[k] = count > 0;
                for (var i = 0; i < count; i++)
                {
                    var material = kind.materials[i];
                    if (material == null || !material.enableInstancing)
                    {
                        Debug.LogWarning($"Environment kind '{kind.id}': material {i} is missing or not set for GPU instancing, so it is not drawn.");
                        _drawable[k] = false;
                        continue;
                    }
                    _params[k][i] = new RenderParams(material)
                    {
                        shadowCastingMode = kind.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off,
                        receiveShadows = true,
                    };
                }
            }
        }

        public int TileCount => _tiles.Count;

        /// <summary>How many pieces of nature the loaded tiles hold, all kinds together.</summary>
        public int PieceCount
        {
            get
            {
                var n = 0;
                foreach (var t in _tiles.Values) foreach (var list in t.props) n += list.Length;
                return n;
            }
        }

        public void AddTile(TileCoord tile, Vector3 centre, Matrix4x4[][] props)
        {
            if (props == null) return;
            _tiles[tile] = (centre, props);
        }

        public void RemoveTile(TileCoord tile) => _tiles.Remove(tile);

        /// <summary>Draws this frame's nature for the camera at <paramref name="eye"/>.</summary>
        public void Draw(Vector3 eye)
        {
            if (_environment == null) return;
            var halfDiagonal = _tileSize * 0.7072f;
            var kinds = _environment.props;
            foreach (var (centre, props) in _tiles.Values)
            {
                var distance = Mathf.Max(0f, Vector2.Distance(new Vector2(eye.x, eye.z), new Vector2(centre.x, centre.z)) - halfDiagonal);
                for (var k = 0; k < props.Length && k < kinds.Length; k++)
                {
                    var list = props[k];
                    if (list.Length == 0 || !_drawable[k] || distance > kinds[k].drawDistance) continue;
                    for (var sub = 0; sub < _params[k].Length; sub++)
                        for (var start = 0; start < list.Length; start += MaxPerCall)
                            Graphics.RenderMeshInstanced(_params[k][sub], kinds[k].mesh, sub, list, Mathf.Min(MaxPerCall, list.Length - start), start);
                }
            }
        }

        /// <summary>Puts every building in the overworld into the scene under <paramref name="parent"/>, standing on the ground.</summary>
        public static int PlaceBuildings(EnvironmentAsset environment, OverworldAsset overworld, Transform parent)
        {
            if (environment == null || overworld == null) return 0;
            var root = new GameObject("Buildings");
            root.transform.SetParent(parent, false);
            var made = 0;
            foreach (var b in overworld.Buildings)
            {
                var kind = b != null ? environment.Building(b.kindId) : null;
                if (kind?.mesh == null) continue;
                var go = new GameObject(kind.id);
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(b.position, Quaternion.Euler(0f, b.yaw, 0f));
                go.AddComponent<MeshFilter>().sharedMesh = kind.mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = kind.materials;
                go.isStatic = true;
                made++;
            }
            return made;
        }
    }
}
