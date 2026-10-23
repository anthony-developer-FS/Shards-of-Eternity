using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShardsOfEternity.Core;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Draws the board and units with primitives and animates battle events.
    /// Tile (x, y) sits at world (x, 0, -y), so row 0 is the far edge of the board.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        const float StepTime = 0.09f;

        Battle battle;
        Transform tileRoot;
        Transform unitRoot;
        Renderer[,] floors;
        Transform activeRing;
        Transform lootRoot;
        readonly List<(Transform beam, float width)> beams = new List<(Transform beam, float width)>();
        readonly Dictionary<Unit, Transform> units = new Dictionary<Unit, Transform>();

        public BattleHud Hud { get; set; }

        public static Vector3 TileToWorld(Point p) => new Vector3(p.X, 0, -p.Y);

        public bool TryGetUnitTransform(Unit u, out Transform t) => units.TryGetValue(u, out t) && t != null;

        public void Build(Battle b)
        {
            battle = b;
            if (unitRoot != null) Destroy(unitRoot.gameObject);
            units.Clear();
            unitRoot = new GameObject("Units").transform;
            unitRoot.SetParent(transform, false);

            activeRing = Primitive(PrimitiveType.Cylinder, unitRoot, "Active ring", Palette.ActiveRing);
            activeRing.localScale = new Vector3(0.9f, 0.015f, 0.9f);

            BuildTiles();
            SyncUnits();
            RefreshLoot();
        }

        void BuildTiles()
        {
            if (tileRoot != null) Destroy(tileRoot.gameObject);
            tileRoot = new GameObject("Tiles").transform;
            tileRoot.SetParent(transform, false);
            var map = battle.Map;
            var theme = battle.Layout.Theme;
            floors = new Renderer[map.Width, map.Height];

            for (int y = 0; y < map.Height; y++)
                for (int x = 0; x < map.Width; x++)
                {
                    var p = new Point(x, y);
                    var type = map[x, y];
                    var pos = TileToWorld(p);
                    if (type == TileType.Wall)
                    {
                        var wall = Primitive(PrimitiveType.Cube, tileRoot, $"Wall {x},{y}", Palette.Hex(theme.WallColor));
                        wall.localPosition = pos + Vector3.up * 0.5f;
                        continue;
                    }

                    var floor = Primitive(PrimitiveType.Cube, tileRoot, $"Tile {x},{y}", Color.black);
                    floor.localPosition = pos + Vector3.down * 0.05f;
                    floor.localScale = new Vector3(0.96f, 0.1f, 0.96f);
                    floors[x, y] = floor.GetComponent<Renderer>();

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
            RefreshTiles(null, null);
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
                    var r = floors[x, y];
                    if (r == null) continue;
                    var p = new Point(x, y);
                    var c = BaseTileColor(x, y);
                    if (moves.TryGetValue(p, out var kind))
                        c = Color.Lerp(c, kind == MoveKind.Charge ? Palette.Charge : kind == MoveKind.Swap ? Palette.Swap : Palette.Move, 0.45f);
                    if (attackTiles.Contains(p)) c = Color.Lerp(c, Palette.AttackTarget, 0.55f);
                    if (abilityTiles.Contains(p)) c = Color.Lerp(c, Palette.AbilityTarget, 0.45f);
                    if (options != null && options.Area.Contains(p)) c = Color.Lerp(c, Palette.Area, 0.55f);
                    if (shift != null && (shift.Add.Contains(p) || shift.Remove.Contains(p)))
                        c = Color.Lerp(c, Palette.Hex(battle.Layout.Theme.AccentColor), shift.Add.Contains(p) ? 0.6f : 0.3f);
                    if (focus.HasValue && focus.Value == p && options != null) c = Color.Lerp(c, Color.white, 0.25f);
                    r.sharedMaterial = Palette.Mat(c);
                }
        }

        /// <summary>Creates views for new units, hides dead ones and snaps everything to the battle state.</summary>
        public void SyncUnits()
        {
            EnsureUnitViews();
            foreach (var u in battle.Units)
            {
                if (!units.TryGetValue(u, out var t) || t == null) continue;
                // Fallen heroes stay on the board as a flat grey body, so a Priest can find them.
                t.gameObject.SetActive(u.Alive || u.IsHero);
                t.localPosition = TileToWorld(u.Position);
                t.localScale = u.Alive ? Vector3.one : FallenScale;
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
            }
        }

        static readonly Vector3 FallenScale = new Vector3(1.1f, 0.18f, 1.1f);

        /// <summary>Sleeping enemies are drawn darker, fallen heroes grey.</summary>
        void RecolorUnits()
        {
            foreach (var pair in units)
            {
                if (pair.Value == null) continue;
                var u = pair.Key;
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
                    case EnemyKind.RiftLord: shape = PrimitiveType.Capsule; scale = new Vector3(0.95f, 0.85f, 0.95f); break;
                    default: shape = PrimitiveType.Capsule; scale = new Vector3(0.75f, 0.6f, 0.75f); break;
                }
            }
            var body = Primitive(shape, root, "Body", Palette.ForUnit(u));
            body.localScale = scale;
            body.localPosition = Vector3.up * (shape == PrimitiveType.Capsule ? scale.y : scale.y / 2);
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

                var gem = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                gem.name = "Item";
                Destroy(gem.GetComponent<Collider>());
                gem.GetComponent<Renderer>().sharedMaterial = Palette.Glow(color);
                gem.SetParent(root, false);
                gem.localScale = Vector3.one * 0.22f;
                gem.localPosition = new Vector3(0.25f, 0.15f, 0.25f);
                gem.localRotation = Quaternion.Euler(45, 0, 45);

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
        }

        public IEnumerator Play(BattleEvent e)
        {
            Transform t = null;
            if (e.Unit != null) TryGetUnitTransform(e.Unit, out t);
            switch (e.Type)
            {
                case BattleEventType.Moved:
                    if (t == null) break;
                    foreach (var p in e.Path) yield return Slide(t, TileToWorld(p), StepTime);
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
                        yield return Scale(t, 0.6f, 0.2f);
                        t.localScale = FallenScale;
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
                    t.localScale = Vector3.one * 0.3f;
                    RecolorUnits();
                    if (Hud != null) Hud.AddFloater(t.position, "Revived", Palette.Heal, true);
                    yield return Scale(t, 1f, 0.3f);
                    break;
                case BattleEventType.Spawned:
                    EnsureUnitViews();
                    if (TryGetUnitTransform(e.Unit, out t))
                    {
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

        // ---------- camera and picking ----------

        /// <summary>Angles the camera down at the board and pulls back until the whole room fits its viewport.</summary>
        public void FrameCamera(Camera cam)
        {
            if (battle == null) return;
            const float pitch = 58f;
            cam.fieldOfView = 35f;
            var center = new Vector3((battle.Width - 1) / 2f, 0, -(battle.Height - 1) / 2f);
            float halfW = battle.Width / 2f + 0.3f;
            float halfH = (battle.Height / 2f + 0.3f) * Mathf.Sin(pitch * Mathf.Deg2Rad) + 0.6f;
            float tanV = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanH = tanV * Mathf.Max(0.1f, cam.aspect);
            float distance = Mathf.Max(halfW / tanH, halfH / tanV) + 1f;
            var rotation = Quaternion.Euler(pitch, 0, 0);
            cam.transform.SetPositionAndRotation(center - rotation * Vector3.forward * distance, rotation);
        }

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
