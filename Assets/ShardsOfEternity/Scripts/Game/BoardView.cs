using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShardsOfEternity.Core;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Draws the board and units as painted miniatures (see ModelLibrary) and animates battle events.
    /// Tile (x, y) sits at world (x, 0, -y), so row 0 is the far edge of the board. Every tile has a rock hanging
    /// under it, so the room reads as a broken island over the abyss. Walls between the camera and the floor drop
    /// to stubs so they never hide the action. If a model is missing, the old colored primitives stand in for it.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        const float StepTime = 0.09f;
        const float LowWall = 0.28f;

        Battle battle;
        Transform tileRoot;
        Transform unitRoot;
        Renderer[,] floors;
        Renderer[,] overlays;
        Transform activeRing;
        Transform lootRoot;
        Transform exitView;
        bool exitShownOpen;
        string themeKey = "crypt";
        readonly List<(Transform beam, float width)> beams = new List<(Transform beam, float width)>();
        readonly List<Transform> lootShards = new List<Transform>();
        readonly Dictionary<Unit, Transform> units = new Dictionary<Unit, Transform>();
        readonly Dictionary<Unit, float> facing = new Dictionary<Unit, float>();
        readonly HashSet<Transform> modelBodies = new HashSet<Transform>();
        readonly List<WallView> walls = new List<WallView>();
        readonly List<(Light light, float intensity, float seed)> torches = new List<(Light light, float intensity, float seed)>();
        MaterialPropertyBlock block;

        sealed class WallView
        {
            public Point Tile;
            public Transform Body;
            public Transform Prop;
            public float Height = 1f;
            public float Scale = 1f;
        }

        public BattleHud Hud { get; set; }
        public Atmosphere Atmosphere { get; set; }

        /// <summary>The theme's key in model names: crypt, hellforge, cathedral or wastes.</summary>
        public static string ThemeKey(Theme theme)
        {
            switch (theme?.Name)
            {
                case "Hellforge": return "hellforge";
                case "Shadow Cathedral": return "cathedral";
                case "Howling Wastes": return "wastes";
                default: return "crypt";
            }
        }

        public static string ModelName(Unit u) =>
            u.IsEnemy ? "enemy_" + u.Def.EnemyKind.ToString().ToLowerInvariant() : "hero_" + u.Def.HeroClass.ToString().ToLowerInvariant();

        /// <summary>World height above a unit's tile for its health bar, so the boss's bar clears his horns.</summary>
        public float BarHeight(Unit u)
        {
            var m = ModelLibrary.Get(ModelName(u));
            if (m == null) return 1.2f;
            float top = m.Anchors.TryGetValue("head", out var head) ? head.y : m.Bounds.max.y;
            return Mathf.Max(0.9f, top + 0.22f);
        }

        static int Hash(int x, int y, int salt = 0)
        {
            unchecked
            {
                int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
                h ^= h >> 13;
                return h & 0x7fffffff;
            }
        }

        public static Vector3 TileToWorld(Point p) => new Vector3(p.X, 0, -p.Y);

        public bool TryGetUnitTransform(Unit u, out Transform t) => units.TryGetValue(u, out t) && t != null;

        /// <summary>The battle on the board, or null between runs.</summary>
        public Battle Battle => battle;

        /// <summary>Removes the board, for the menus between runs.</summary>
        public void Clear()
        {
            battle = null;
            if (unitRoot != null) Destroy(unitRoot.gameObject);
            if (tileRoot != null) Destroy(tileRoot.gameObject);
            units.Clear();
            facing.Clear();
            modelBodies.Clear();
            walls.Clear();
            torches.Clear();
            if (lootRoot != null) Destroy(lootRoot.gameObject);
            beams.Clear();
            lootShards.Clear();
            if (Atmosphere != null) Atmosphere.ShowMenu();
        }

        public void Build(Battle b)
        {
            battle = b;
            if (unitRoot != null) Destroy(unitRoot.gameObject);
            units.Clear();
            facing.Clear();
            modelBodies.Clear();
            unitRoot = new GameObject("Units").transform;
            unitRoot.SetParent(transform, false);

            activeRing = ModelLibrary.Spawn("active_ring", unitRoot, "Active ring");
            if (activeRing != null) SetTint(activeRing.GetComponent<Renderer>(), Palette.ActiveRing, 0f, 1.4f);
            else
            {
                activeRing = Primitive(PrimitiveType.Cylinder, unitRoot, "Active ring", Palette.ActiveRing);
                activeRing.localScale = new Vector3(0.9f, 0.015f, 0.9f);
            }

            themeKey = ThemeKey(b.Layout.Theme);
            if (Atmosphere != null) Atmosphere.ShowBoard(b, themeKey);
            BuildTiles();
            SyncUnits();
            RefreshLoot();
        }

        void BuildTiles()
        {
            if (tileRoot != null) Destroy(tileRoot.gameObject);
            tileRoot = new GameObject("Tiles").transform;
            tileRoot.SetParent(transform, false);
            walls.Clear();
            torches.Clear();
            exitView = null;
            var map = battle.Map;
            var theme = battle.Layout.Theme;
            floors = new Renderer[map.Width, map.Height];
            overlays = new Renderer[map.Width, map.Height];
            bool art = ModelLibrary.Has($"floor_{themeKey}_0");
            int lights = 0;

            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var p = new Point(x, y);
                    var type = map[x, y];
                    var pos = TileToWorld(p);
                    int h = Hash(x, y);
                    bool edge = x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1;
                    if (art)
                    {
                        // The island rock under every tile; edges hang deeper.
                        var rock = ModelLibrary.Spawn($"rock_{themeKey}_{(edge && (h & 1) == 0 ? 1 : 0)}", tileRoot, "Rock");
                        if (rock != null)
                        {
                            rock.localPosition = pos;
                            rock.localRotation = Quaternion.Euler(0, 90 * (h % 4), 0);
                        }
                    }

                    if (type == TileType.Wall)
                    {
                        if (art)
                        {
                            int v = (h >> 3) & 1;
                            var wall = ModelLibrary.Spawn($"wall_{themeKey}_{v}", tileRoot, $"Wall {x},{y}");
                            wall.localPosition = pos;
                            // Turn the wall so its decorated face looks into the room.
                            wall.localRotation = Quaternion.Euler(0, FaceIntoRoom(p), 0);
                            var view = new WallView { Tile = p, Body = wall, Height = v == 0 ? 1f : 0.82f };
                            if (lights < 6 && (h % 5) == 0 && TouchesFloor(p))
                            {
                                view.Prop = ModelLibrary.Spawn($"light_{themeKey}", tileRoot, "Light");
                                if (view.Prop != null)
                                {
                                    view.Prop.localPosition = pos + Vector3.up * view.Height;
                                    AddTorch(view.Prop, h);
                                    lights++;
                                }
                            }
                            walls.Add(view);
                        }
                        else
                        {
                            var wall = Primitive(PrimitiveType.Cube, tileRoot, $"Wall {x},{y}", Palette.Hex(theme.WallColor));
                            wall.localPosition = pos + Vector3.up * 0.5f;
                        }
                        continue;
                    }

                    if (art)
                    {
                        var floor = ModelLibrary.Spawn($"floor_{themeKey}_{h % 4}", tileRoot, $"Tile {x},{y}");
                        floor.localPosition = pos;
                        floor.localRotation = Quaternion.Euler(0, 90 * ((h >> 4) % 4), 0);
                        overlays[x, y] = MakeOverlay(pos);
                        switch (type)
                        {
                            case TileType.Pillar:
                                Place("pillar_" + themeKey, pos, (h >> 6) % 4 * 90);
                                break;
                            case TileType.Spikes:
                                Place("spikes", pos, 0);
                                break;
                            case TileType.Spawn:
                                Place("spawn_crack", pos, h % 360);
                                break;
                            case TileType.Exit:
                                exitShownOpen = battle.ExitOpen;
                                exitView = Place(battle.ExitOpen ? "exit_open" : "exit_closed", pos, 0);
                                break;
                        }
                        continue;
                    }

                    var cube = Primitive(PrimitiveType.Cube, tileRoot, $"Tile {x},{y}", Color.black);
                    cube.localPosition = pos + Vector3.down * 0.05f;
                    cube.localScale = new Vector3(0.96f, 0.1f, 0.96f);
                    floors[x, y] = cube.GetComponent<Renderer>();

                    if (type == TileType.Pillar)
                    {
                        var pillar = Primitive(PrimitiveType.Cylinder, tileRoot, "Pillar", Palette.Pillar);
                        pillar.localPosition = pos + Vector3.up * 0.6f;
                        pillar.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                    }
                    else if (type == TileType.Spikes)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var spike = Primitive(PrimitiveType.Cube, tileRoot, "Spike", Palette.Hex(0xB8B8C0));
                            spike.localPosition = pos + new Vector3(i % 2 == 0 ? -0.2f : 0.2f, 0.08f, i < 2 ? -0.2f : 0.2f);
                            spike.localScale = new Vector3(0.1f, 0.16f, 0.1f);
                            spike.localRotation = Quaternion.Euler(0, 45, 0);
                        }
                    }
                }
            UpdateWalls(true);
            RefreshTiles(null, null);
        }

        Transform Place(string model, Vector3 pos, float yaw)
        {
            var t = ModelLibrary.Spawn(model, tileRoot);
            if (t == null) return null;
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            return t;
        }

        bool Walkable(Point p) => battle.Map.InBounds(p) && battle.Map[p.X, p.Y] != TileType.Wall;

        bool TouchesFloor(Point p) =>
            Walkable(new Point(p.X + 1, p.Y)) || Walkable(new Point(p.X - 1, p.Y)) ||
            Walkable(new Point(p.X, p.Y + 1)) || Walkable(new Point(p.X, p.Y - 1));

        /// <summary>Yaw that turns a wall's front (model -Z) toward the open floor next to it.</summary>
        float FaceIntoRoom(Point p)
        {
            // Row y + 1 is nearer the camera (world -Z), which is the model's front at yaw 0.
            if (Walkable(new Point(p.X, p.Y + 1))) return 0;
            if (Walkable(new Point(p.X, p.Y - 1))) return 180;
            if (Walkable(new Point(p.X + 1, p.Y))) return -90;
            if (Walkable(new Point(p.X - 1, p.Y))) return 90;
            return 90 * (Hash(p.X, p.Y, 3) % 4);
        }

        void AddTorch(Transform prop, int h)
        {
            var m = ModelLibrary.Get($"light_{themeKey}");
            var light = new GameObject("Torch").AddComponent<Light>();
            light.transform.SetParent(prop, false);
            light.transform.localPosition = m != null && m.Anchors.TryGetValue("light", out var a) ? a + Vector3.up * 0.15f : Vector3.up * 0.4f;
            light.type = LightType.Point;
            light.color = Atmosphere.TorchColor(themeKey);
            light.range = 4.5f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            torches.Add((light, light.intensity, h % 100));
        }

        /// <summary>The highlight drawn over a tile: a soft-edged square that is hidden until the tile is lit up.</summary>
        Renderer MakeOverlay(Vector3 pos)
        {
            var go = new GameObject("Highlight");
            go.transform.SetParent(tileRoot, false);
            go.transform.localPosition = pos + Vector3.up * 0.03f;
            go.AddComponent<MeshFilter>().sharedMesh = OverlayMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return r;
        }

        static Mesh overlayMesh;

        /// <summary>A square with a bright rim and a fainter fill, all in vertex alpha.</summary>
        static Mesh OverlayMesh
        {
            get
            {
                if (overlayMesh != null) return overlayMesh;
                float o = 0.47f, i = 0.38f;
                var v = new[]
                {
                    new Vector3(-o, 0, -o), new Vector3(o, 0, -o), new Vector3(o, 0, o), new Vector3(-o, 0, o),
                    new Vector3(-i, 0, -i), new Vector3(i, 0, -i), new Vector3(i, 0, i), new Vector3(-i, 0, i),
                };
                var rim = new Color(1, 1, 1, 1f);
                var fill = new Color(1, 1, 1, 0.55f);
                var c = new[] { rim, rim, rim, rim, fill, fill, fill, fill };
                var tris = new List<int> { 4, 6, 5, 4, 7, 6 };
                for (int k = 0; k < 4; k++)
                {
                    int a = k, b = (k + 1) % 4;
                    tris.AddRange(new[] { a, b + 4, b, a, a + 4, b + 4 });
                }
                overlayMesh = new Mesh { name = "Tile highlight" };
                overlayMesh.SetVertices(v);
                overlayMesh.SetColors(c);
                overlayMesh.SetTriangles(tris, 0);
                overlayMesh.RecalculateNormals();
                overlayMesh.RecalculateBounds();
                return overlayMesh;
            }
        }

        void SetTint(Renderer r, Color tint, float desaturate, float glow = 1.6f)
        {
            if (r == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            r.GetPropertyBlock(block);
            block.SetColor("_Tint", tint);
            block.SetFloat("_Desaturate", desaturate);
            block.SetFloat("_Glow", glow);
            r.SetPropertyBlock(block);
        }

        /// <summary>
        /// Walls that stand between the camera and open floor shrink to stubs, so the room can be read from any angle,
        /// the way tabletop games cut away the near walls.
        /// </summary>
        static readonly float[] Probes = { 1f, 1.6f };

        void UpdateWalls(bool snap)
        {
            if (walls.Count == 0) return;
            var cam = Camera.main;
            if (cam == null) return;
            var f = cam.transform.forward;
            var flat = new Vector2(f.x, f.z);
            if (flat.sqrMagnitude < 1e-4f) flat = Vector2.up;
            flat.Normalize();
            foreach (var w in walls)
            {
                if (w.Body == null) continue;
                var world = TileToWorld(w.Tile);
                bool hides = false;
                foreach (var d in Probes)
                {
                    var probe = new Point(Mathf.RoundToInt(world.x + flat.x * d), Mathf.RoundToInt(-(world.z + flat.y * d)));
                    if (probe != w.Tile && Walkable(probe)) hides = true;
                }
                float target = hides ? LowWall : 1f;
                w.Scale = snap ? target : Mathf.MoveTowards(w.Scale, target, Time.unscaledDeltaTime * 3f);
                w.Body.localScale = new Vector3(1, w.Scale, 1);
                if (w.Prop != null)
                {
                    w.Prop.localPosition = world + Vector3.up * (w.Height * w.Scale);
                    w.Prop.gameObject.SetActive(w.Scale > 0.6f);
                }
            }
        }

        Transform Primitive(PrimitiveType type, Transform parent, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Palette.Mat(color);
            return go.transform;
        }

        Color BaseTileColor(int x, int y)
        {
            var theme = battle.Layout.Theme;
            var floor = Palette.Hex(theme.FloorColor);
            if (((x + y) & 1) == 1) floor = Color.Lerp(floor, Color.white, 0.06f);
            switch (battle.Map[x, y])
            {
                case TileType.Spikes: return Palette.Spikes;
                case TileType.Exit: return battle.ExitOpen ? Palette.ExitOpen : Palette.ExitClosed;
                case TileType.Spawn: return Color.Lerp(floor, Palette.Hex(theme.AccentColor), 0.45f);
                default: return floor;
            }
        }

        /// <summary>Recolors tiles to show move, attack and ability options, the hovered tile and any telegraphed wall shift.</summary>
        public void RefreshTiles(HeroOptions options, Point? focus)
        {
            if (floors == null) return;
            var shift = battle.PendingShift;
            var moves = new Dictionary<Point, MoveKind>();
            var abilityTiles = new HashSet<Point>();
            var attackTiles = new HashSet<Point>();
            if (options != null)
            {
                foreach (var m in options.Moves) moves[m.Target] = m.Kind;
                foreach (var p in options.AbilityTargets) abilityTiles.Add(p);
                foreach (var t in options.Attacks) attackTiles.Add(t.Position);
            }

            for (int y = 0; y < battle.Height; y++)
                for (int x = 0; x < battle.Width; x++)
                {
                    var p = new Point(x, y);
                    Color? c = null;
                    void Layer(Color color, float k) => c = c.HasValue ? Color.Lerp(c.Value, color, k) : color;
                    if (moves.TryGetValue(p, out var kind))
                        Layer(kind == MoveKind.Charge ? Palette.Charge : kind == MoveKind.Swap ? Palette.Swap : Palette.Move, 0.6f);
                    if (attackTiles.Contains(p)) Layer(Palette.AttackTarget, 0.8f);
                    if (abilityTiles.Contains(p)) Layer(Palette.AbilityTarget, 0.7f);
                    if (options != null && options.Area.Contains(p)) Layer(Palette.Area, 0.75f);
                    if (shift != null && (shift.Add.Contains(p) || shift.Remove.Contains(p)))
                        Layer(Palette.Hex(battle.Layout.Theme.AccentColor), shift.Add.Contains(p) ? 0.8f : 0.5f);
                    bool focused = focus.HasValue && focus.Value == p && options != null;

                    var o = overlays[x, y];
                    if (o != null)
                    {
                        if (focused && !c.HasValue) c = Color.white;
                        o.enabled = c.HasValue;
                        if (c.HasValue)
                        {
                            var col = c.Value;
                            col.a = focused ? 0.75f : 0.45f;
                            if (focused) col = Color.Lerp(col, Color.white, 0.3f);
                            o.sharedMaterial = Palette.Overlay(col);
                        }
                        continue;
                    }

                    var r = floors[x, y];
                    if (r == null) continue;
                    var b = BaseTileColor(x, y);
                    if (c.HasValue) b = Color.Lerp(b, c.Value, 0.5f);
                    if (focused) b = Color.Lerp(b, Color.white, 0.25f);
                    r.sharedMaterial = Palette.Mat(b);
                }

            // The rift door lights up when the floor is cleared.
            if (exitView != null && exitShownOpen != battle.ExitOpen)
            {
                exitShownOpen = battle.ExitOpen;
                var m = ModelLibrary.Get(exitShownOpen ? "exit_open" : "exit_closed");
                if (m != null) exitView.GetComponent<MeshFilter>().sharedMesh = m.Mesh;
            }
        }

        /// <summary>Creates views for new units, hides dead ones and snaps everything to the battle state.</summary>
        public void SyncUnits()
        {
            EnsureUnitViews();
            foreach (var u in battle.Units)
            {
                if (!units.TryGetValue(u, out var t) || t == null) continue;
                // Fallen heroes stay on the board, knocked over and grey, so a Priest can find them.
                t.gameObject.SetActive(u.Alive || u.IsHero);
                t.localPosition = TileToWorld(u.Position);
                ApplyPose(u, t);
            }
            RecolorUnits();

            var cur = battle.Current;
            bool showRing = cur != null && cur.Alive && !battle.IsOver;
            activeRing.gameObject.SetActive(showRing);
            if (showRing) activeRing.localPosition = TileToWorld(cur.Position) + Vector3.up * 0.01f;
        }

        void EnsureUnitViews()
        {
            foreach (var u in battle.Units)
            {
                if ((!u.Alive && !u.IsHero) || (units.TryGetValue(u, out var t) && t != null)) continue;
                t = CreateUnit(u);
                t.localPosition = TileToWorld(u.Position);
                units[u] = t;
                FaceNearestFoe(u, true);
            }
        }

        bool IsModel(Transform root) => root.childCount > 0 && modelBodies.Contains(root.GetChild(0));

        /// <summary>
        /// Standing, or for a fallen hero lying on its back like a knocked-over miniature. The primitive fallback
        /// flattens instead.
        /// </summary>
        void ApplyPose(Unit u, Transform t)
        {
            if (!IsModel(t))
            {
                t.localScale = u.Alive ? Vector3.one : FallenScale;
                return;
            }
            t.localScale = Vector3.one;
            var body = t.GetChild(0);
            body.localRotation = u.Alive ? Quaternion.identity : FallenTilt;
            body.localPosition = u.Alive ? Vector3.zero : FallenOffset;
        }

        static readonly Quaternion FallenTilt = Quaternion.Euler(-82, 0, 0);
        static readonly Vector3 FallenOffset = new Vector3(0, 0.3f, 0.42f);

        /// <summary>Turns a unit to look at a world position. Models face -Z at yaw 0.</summary>
        void Face(Unit u, Vector3 target, bool snap = false)
        {
            if (!TryGetUnitTransform(u, out var t)) return;
            var d = target - t.localPosition;
            d.y = 0;
            if (d.sqrMagnitude < 1e-4f) return;
            float yaw = Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg;
            facing[u] = yaw;
            if (snap) t.localRotation = Quaternion.Euler(0, yaw, 0);
        }

        void FaceNearestFoe(Unit u, bool snap)
        {
            Unit best = null;
            float bestD = float.MaxValue;
            foreach (var o in battle.Units)
            {
                if (!o.Alive || o.IsHero == u.IsHero) continue;
                float d = (o.Position.X - u.Position.X) * (o.Position.X - u.Position.X) + (o.Position.Y - u.Position.Y) * (o.Position.Y - u.Position.Y);
                if (d < bestD) { bestD = d; best = o; }
            }
            if (best != null) Face(u, TileToWorld(best.Position), snap);
        }

        static readonly Vector3 FallenScale = new Vector3(1.1f, 0.18f, 1.1f);

        /// <summary>Sleeping enemies are drawn darker, fallen heroes grey.</summary>
        void RecolorUnits()
        {
            foreach (var pair in units)
            {
                if (pair.Value == null) continue;
                var u = pair.Key;
                if (IsModel(pair.Value))
                {
                    var r = pair.Value.GetChild(0).GetComponent<Renderer>();
                    if (!u.Alive) SetTint(r, new Color(0.7f, 0.7f, 0.75f), 0.85f, 0.2f);
                    else if (u.IsEnemy && !u.Awake) SetTint(r, new Color(0.5f, 0.5f, 0.58f), 0.25f, 0.6f);
                    else SetTint(r, Color.white, 0f);
                    continue;
                }
                var color = Palette.ForUnit(u);
                pair.Value.GetChild(0).GetComponent<Renderer>().sharedMaterial =
                    Palette.Mat(!u.Alive ? Color.Lerp(color, Palette.Fallen, 0.75f)
                        : u.IsEnemy && !u.Awake ? Color.Lerp(color, Color.black, 0.45f) : color);
            }
        }

        Transform CreateUnit(Unit u)
        {
            var root = new GameObject(u.Name).transform;
            root.SetParent(unitRoot, false);
            var model = ModelLibrary.Spawn(ModelName(u), root, "Body");
            if (model != null)
            {
                modelBodies.Add(model);
                return root;
            }
            PrimitiveType shape;
            Vector3 scale;
            if (u.IsHero)
            {
                shape = PrimitiveType.Capsule;
                scale = new Vector3(0.55f, 0.45f, 0.55f);
            }
            else
            {
                switch (u.Def.EnemyKind)
                {
                    case EnemyKind.Ghoul: shape = PrimitiveType.Cube; scale = new Vector3(0.55f, 0.7f, 0.55f); break;
                    case EnemyKind.Imp: shape = PrimitiveType.Sphere; scale = new Vector3(0.55f, 0.55f, 0.55f); break;
                    case EnemyKind.Cultist: shape = PrimitiveType.Cylinder; scale = new Vector3(0.45f, 0.4f, 0.45f); break;
                    case EnemyKind.Hellhound: shape = PrimitiveType.Cube; scale = new Vector3(0.45f, 0.4f, 0.75f); break;
                    case EnemyKind.RiftCaller: shape = PrimitiveType.Cylinder; scale = new Vector3(0.6f, 0.5f, 0.6f); break;
                    case EnemyKind.RiftLord: shape = PrimitiveType.Capsule; scale = new Vector3(0.95f, 0.85f, 0.95f); break;
                    default: shape = PrimitiveType.Capsule; scale = new Vector3(0.75f, 0.6f, 0.75f); break;
                }
            }
            var body = Primitive(shape, root, "Body", Palette.ForUnit(u));
            body.localScale = scale;
            body.localPosition = Vector3.up * (shape == PrimitiveType.Capsule || shape == PrimitiveType.Cylinder ? scale.y : scale.y / 2);
            return root;
        }

        // ---------- animation ----------

        // ---------- loot beams ----------

        /// <summary>
        /// Diablo-style pillars of light over every pile of loot, colored by the best item's rarity:
        /// white, blue, yellow, orange. Better items get taller, thicker beams; legendaries also light the floor.
        /// </summary>
        public void RefreshLoot()
        {
            if (lootRoot != null) Destroy(lootRoot.gameObject);
            beams.Clear();
            lootShards.Clear();
            lootRoot = new GameObject("Loot").transform;
            lootRoot.SetParent(transform, false);
            foreach (var pile in battle.GroundLoot)
            {
                var best = pile.Value.Max(i => i.Rarity);
                var color = Palette.ForRarity(best);
                float height = best == Rarity.Legendary ? 9f : best == Rarity.Rare ? 6f : best == Rarity.Magic ? 4f : 2.5f;
                float width = best == Rarity.Legendary ? 0.16f : best == Rarity.Rare ? 0.12f : best == Rarity.Magic ? 0.09f : 0.07f;

                var root = new GameObject($"Loot {pile.Key}").transform;
                root.SetParent(lootRoot, false);
                root.localPosition = TileToWorld(pile.Key);

                var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder).transform;
                beam.name = "Beam";
                Destroy(beam.GetComponent<Collider>());
                beam.GetComponent<Renderer>().sharedMaterial = Palette.Glow(color);
                beam.SetParent(root, false);
                beam.localScale = new Vector3(width, height / 2, width);
                beam.localPosition = Vector3.up * height / 2;
                beams.Add((beam, width));

                var shard = ModelLibrary.Spawn("loot_shard", root, "Item");
                if (shard != null)
                {
                    SetTint(shard.GetComponent<Renderer>(), color, 0f, 2.2f);
                    lootShards.Add(shard);
                }
                else
                {
                    var gem = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                    gem.name = "Item";
                    Destroy(gem.GetComponent<Collider>());
                    gem.GetComponent<Renderer>().sharedMaterial = Palette.Glow(color);
                    gem.SetParent(root, false);
                    gem.localScale = Vector3.one * 0.22f;
                    gem.localPosition = new Vector3(0.25f, 0.15f, 0.25f);
                    gem.localRotation = Quaternion.Euler(45, 0, 45);
                }

                if (best >= Rarity.Rare)
                {
                    var light = new GameObject("Glow").AddComponent<Light>();
                    light.transform.SetParent(root, false);
                    light.transform.localPosition = Vector3.up * 0.6f;
                    light.type = LightType.Point;
                    light.color = color;
                    light.range = best == Rarity.Legendary ? 3.5f : 2f;
                    light.intensity = best == Rarity.Legendary ? 3f : 1.5f;
                }
            }
        }

        void Update()
        {
            // Beams shimmer so they read as light rather than solid poles.
            float pulse = 1f + 0.18f * Mathf.Sin(Time.time * 4f);
            foreach (var (beam, width) in beams)
            {
                if (beam == null) continue;
                beam.localScale = new Vector3(width * pulse, beam.localScale.y, width * pulse);
            }
            foreach (var shard in lootShards)
            {
                if (shard == null) continue;
                shard.localRotation = Quaternion.Euler(0, Time.time * 70f, 0);
                shard.localPosition = new Vector3(0.22f, 0.32f + 0.05f * Mathf.Sin(Time.time * 2.4f), 0.22f);
            }
            if (battle == null) return;

            // Units turn smoothly, breathe, and the floating ones bob.
            float dt = Time.deltaTime;
            foreach (var pair in units)
            {
                var t = pair.Value;
                if (t == null || !t.gameObject.activeSelf) continue;
                var u = pair.Key;
                if (facing.TryGetValue(u, out var yaw))
                    t.localRotation = Quaternion.RotateTowards(t.localRotation, Quaternion.Euler(0, yaw, 0), 720f * dt);
                if (!u.Alive || !IsModel(t)) continue;
                var body = t.GetChild(0);
                float phase = u.Id * 1.37f;
                float breathe = 1f + 0.018f * Mathf.Sin(Time.time * 2.2f + phase);
                body.localScale = new Vector3(1, breathe, 1);
                if (u.IsEnemy && u.Def.EnemyKind == EnemyKind.RiftCaller)
                    body.localPosition = Vector3.up * (0.03f + 0.025f * Mathf.Sin(Time.time * 1.8f + phase));
            }

            if (activeRing != null && activeRing.gameObject.activeSelf)
                activeRing.localRotation = Quaternion.Euler(0, Time.time * 30f, 0);

            foreach (var (light, intensity, seed) in torches)
            {
                if (light == null) continue;
                float n = Mathf.PerlinNoise(Time.time * 3.5f, seed);
                light.intensity = intensity * (0.75f + 0.5f * n);
            }
            UpdateWalls(false);
        }

        public IEnumerator Play(BattleEvent e)
        {
            Transform t = null;
            if (e.Unit != null) TryGetUnitTransform(e.Unit, out t);
            switch (e.Type)
            {
                case BattleEventType.Moved:
                    if (t == null) break;
                    foreach (var p in e.Path)
                    {
                        Face(e.Unit, TileToWorld(p));
                        yield return Hop(t, TileToWorld(p), StepTime);
                    }
                    break;
                case BattleEventType.KnockedBack:
                    if (t != null) yield return Slide(t, TileToWorld(e.To), StepTime + 0.06f);
                    break;
                case BattleEventType.Teleported:
                    if (t == null) break;
                    yield return Scale(t, 0.1f, 0.09f);
                    t.localPosition = TileToWorld(e.To);
                    yield return Scale(t, 1f, 0.09f);
                    break;
                case BattleEventType.Swapped:
                    if (t != null && TryGetUnitTransform(e.Other, out var other))
                    {
                        var a = t.localPosition;
                        t.localPosition = other.localPosition;
                        other.localPosition = a;
                    }
                    yield return new WaitForSeconds(0.15f);
                    break;
                case BattleEventType.Attacked:
                    if (t != null && TryGetUnitTransform(e.Other, out var target))
                    {
                        Face(e.Unit, target.localPosition, true);
                        Face(e.Other, t.localPosition);
                        var home = t.localPosition;
                        var lunge = Vector3.Lerp(home, target.localPosition, 0.3f);
                        yield return Slide(t, lunge, 0.08f);
                        yield return Slide(t, home, 0.08f);
                    }
                    break;
                case BattleEventType.AbilityUsed:
                    if (t != null && Hud != null) Hud.AddFloater(t.position, e.Text, Color.white);
                    yield return new WaitForSeconds(0.15f);
                    break;
                case BattleEventType.Damaged:
                    if (t != null) StartCoroutine(Flinch(t));
                    if (t != null && Hud != null) Hud.AddFloater(t.position, "-" + e.Amount, e.Crit ? Palette.Crit : Color.white, e.Crit);
                    yield return new WaitForSeconds(0.2f);
                    break;
                case BattleEventType.Healed:
                    if (t != null && Hud != null) Hud.AddFloater(t.position, "+" + e.Amount, Palette.Heal);
                    yield return new WaitForSeconds(0.12f);
                    break;
                case BattleEventType.Died:
                    if (t == null) break;
                    if (e.Unit.IsHero)
                    {
                        if (IsModel(t)) yield return TipOver(t.GetChild(0), 0.3f);
                        else yield return Scale(t, 0.6f, 0.2f);
                        ApplyPose(e.Unit, t);
                        RecolorUnits();
                        break;
                    }
                    yield return Scale(t, 0.05f, 0.3f);
                    t.gameObject.SetActive(false);
                    break;
                case BattleEventType.Revived:
                    if (t == null) break;
                    t.gameObject.SetActive(true);
                    t.localPosition = TileToWorld(e.To);
                    ApplyPose(e.Unit, t);
                    t.localScale = Vector3.one * 0.3f;
                    RecolorUnits();
                    if (Hud != null) Hud.AddFloater(t.position, "Revived", Palette.Heal, true);
                    yield return Scale(t, 1f, 0.3f);
                    break;
                case BattleEventType.Spawned:
                    EnsureUnitViews();
                    if (TryGetUnitTransform(e.Unit, out t))
                    {
                        FaceNearestFoe(e.Unit, true);
                        t.localScale = Vector3.one * 0.1f;
                        yield return Scale(t, 1f, 0.2f);
                    }
                    break;
                case BattleEventType.Shifted:
                    BuildTiles();
                    yield return new WaitForSeconds(0.3f);
                    break;
                case BattleEventType.LootDropped:
                    RefreshLoot();
                    if (Hud != null) Hud.AddFloater(TileToWorld(e.To), e.Item.Name, Palette.ForRarity(e.Item.Rarity), e.Item.Rarity >= Rarity.Rare);
                    yield return new WaitForSeconds(e.Item.Rarity >= Rarity.Rare ? 0.45f : 0.25f);
                    break;
                case BattleEventType.LootPickedUp:
                    RefreshLoot();
                    if (Hud != null) Hud.AddFloater(TileToWorld(e.To), "+ " + e.Item.Name, Palette.ForRarity(e.Item.Rarity));
                    yield return new WaitForSeconds(0.15f);
                    break;
                case BattleEventType.RoomWoke:
                    RecolorUnits();
                    break;
            }
        }

        static IEnumerator Slide(Transform t, Vector3 to, float duration)
        {
            var from = t.localPosition;
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                t.localPosition = Vector3.Lerp(from, to, time / duration);
                yield return null;
            }
            t.localPosition = to;
        }

        static IEnumerator Scale(Transform t, float to, float duration)
        {
            var from = t.localScale;
            var target = Vector3.one * to;
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                t.localScale = Vector3.Lerp(from, target, time / duration);
                yield return null;
            }
            t.localScale = target;
        }

        /// <summary>A step with a little hop, like a miniature being moved across the board.</summary>
        static IEnumerator Hop(Transform t, Vector3 to, float duration)
        {
            var from = t.localPosition;
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                float k = time / duration;
                t.localPosition = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.06f);
                yield return null;
            }
            t.localPosition = to;
        }

        /// <summary>A quick rock backwards when hit.</summary>
        IEnumerator Flinch(Transform t)
        {
            if (!IsModel(t)) yield break;
            var body = t.GetChild(0);
            for (float time = 0; time < 0.18f; time += Time.deltaTime)
            {
                if (body == null) yield break;
                body.localRotation = Quaternion.Euler(-14f * Mathf.Sin(time / 0.18f * Mathf.PI), 0, 0);
                yield return null;
            }
            if (body != null && body.localPosition.z == 0) body.localRotation = Quaternion.identity;
        }

        static IEnumerator TipOver(Transform body, float duration)
        {
            var from = body.localRotation;
            for (float time = 0; time < duration; time += Time.deltaTime)
            {
                float k = time / duration;
                k *= k;
                body.localRotation = Quaternion.Slerp(from, FallenTilt, k);
                body.localPosition = Vector3.Lerp(Vector3.zero, FallenOffset, k);
                yield return null;
            }
        }

        // ---------- camera and picking ----------

        /// <summary>The tile under a screen position, if it is on the board.</summary>
        public Point? ScreenToTile(Camera cam, Vector2 screen)
        {
            if (battle == null || !cam.pixelRect.Contains(screen)) return null;
            var ray = cam.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter)) return null;
            var hit = ray.GetPoint(enter);
            var p = new Point(Mathf.RoundToInt(hit.x), Mathf.RoundToInt(-hit.z));
            return battle.Map.InBounds(p) ? p : (Point?)null;
        }
    }
}
