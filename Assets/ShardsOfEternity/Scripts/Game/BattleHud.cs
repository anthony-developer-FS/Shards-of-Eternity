using System.Collections.Generic;
using System.Linq;
using ShardsOfEternity.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// The game's UI, built with UI Toolkit in code and styled by Resources/UI/Shards.uss: the title and squad menus,
    /// the battle HUD (turn order, unit card, abilities, preview, log), health bars and damage numbers over units,
    /// and the pause, settings, camp and end-of-run screens. The layout switches to a bottom panel in portrait.
    /// </summary>
    public sealed partial class BattleHud : MonoBehaviour
    {
        public enum Menu
        {
            Title,
            Squad,
            /// <summary>Hero levels, equipped gear and the vault, between runs.</summary>
            Armory,
            /// <summary>No menu: the battle HUD is showing.</summary>
            None,
        }

        public BattleController Controller;

        /// <summary>The panel the UI draws into. Tests point its target texture at a render texture for screenshots.</summary>
        public PanelSettings PanelSettings { get; private set; }
        public Menu CurrentMenu { get; private set; } = Menu.Title;
        public bool SettingsOpen { get; private set; }

        UIDocument document;
        VisualElement root, worldLayer, battleLayer, overlayLayer, menuLayer;
        bool built;

        Battle Battle => Controller != null ? Controller.Battle : null;
        bool InBattle => CurrentMenu == Menu.None && Battle != null;

        // ---------- settings ----------

        const string ScaleKey = "soe.uiScale", BarsKey = "soe.showBars";
        public static readonly float[] UiScales = { 0.85f, 1f, 1.15f, 1.3f };
        public float UiScale { get; private set; } = 1f;
        public bool ShowUnitBars { get; private set; } = true;

        public void SetUiScale(float value)
        {
            UiScale = value;
            PlayerPrefs.SetFloat(ScaleKey, value);
            if (PanelSettings != null) PanelSettings.scale = value;
        }

        public void SetShowUnitBars(bool value)
        {
            ShowUnitBars = value;
            PlayerPrefs.SetInt(BarsKey, value ? 1 : 0);
        }

        // ---------- setup ----------

        void Start() => EnsureBuilt();

        public void EnsureBuilt()
        {
            if (built) return;
            built = true;
            UiScale = PlayerPrefs.GetFloat(ScaleKey, 1f);
            ShowUnitBars = PlayerPrefs.GetInt(BarsKey, 1) == 1;

            PanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            PanelSettings.name = "Shards UI";
            PanelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("UI/ShardsTheme");
            PanelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            PanelSettings.referenceResolution = new Vector2Int(1280, 720);
            PanelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            PanelSettings.match = 0.5f;
            PanelSettings.scale = UiScale;

            // The document builds its tree when enabled, so give it the panel settings first.
            var go = new GameObject("UI");
            go.SetActive(false);
            go.transform.SetParent(transform, false);
            document = go.AddComponent<UIDocument>();
            document.panelSettings = PanelSettings;
            go.SetActive(true);

            root = document.rootVisualElement;
            var sheet = Resources.Load<StyleSheet>("UI/Shards");
            if (sheet != null) root.styleSheets.Add(sheet);
            else Debug.LogWarning("Shards of Eternity: Resources/UI/Shards.uss is missing, the UI will be unstyled.");
            root.With("soe", "landscape");
            root.Ignore();

            worldLayer = Ui.Box("layer").Into(root);
            worldLayer.Ignore();
            battleLayer = Ui.Box("layer").Into(root);
            battleLayer.Ignore();
            overlayLayer = Ui.Box("layer").Into(root);
            overlayLayer.Ignore();
            menuLayer = Ui.Box("layer").Into(root);
            menuLayer.Ignore();

            BuildBattleHud();
            ShowTitle();
        }

        public void ShowTitle()
        {
            EnsureBuilt();
            CurrentMenu = Menu.Title;
            SettingsOpen = false;
            BuildTitle();
        }

        public void ShowSquad()
        {
            EnsureBuilt();
            CurrentMenu = Menu.Squad;
            SettingsOpen = false;
            BuildSquad();
        }

        public void ShowArmory()
        {
            EnsureBuilt();
            CurrentMenu = Menu.Armory;
            SettingsOpen = false;
            BuildArmory();
        }

        /// <summary>Starts a run with the squad and rules picked on the squad screen.</summary>
        public void StartRun()
        {
            if (!Controller.SquadReady) return;
            CurrentMenu = Menu.None;
            SettingsOpen = false;
            menuLayer.Clear();
            Controller.NewRun();
            modalDirty = true;
        }

        public void OpenSettings(bool open)
        {
            SettingsOpen = open;
            modalDirty = true;
            if (CurrentMenu != Menu.None) RebuildMenu();
        }

        void RebuildMenu()
        {
            if (CurrentMenu == Menu.Title) BuildTitle();
            else if (CurrentMenu == Menu.Squad) BuildSquad();
            else if (CurrentMenu == Menu.Armory) BuildArmory();
        }

        /// <summary>True if a screen position (bottom-left origin, like Input.mousePosition) is over a UI element.</summary>
        public bool IsOverHud(Vector2 screen)
        {
            if (root?.panel == null) return false;
            var p = ScreenToPanel(screen, Screen.width, Screen.height);
            var picked = root.panel.Pick(p);
            return picked != null && picked != root;
        }

        bool Portrait => root != null && root.layout.height > root.layout.width;

        void Update()
        {
            if (!built || Controller == null) return;
            bool portrait = Portrait;
            root.EnableInClassList("portrait", portrait);
            root.EnableInClassList("landscape", !portrait);

            battleLayer.Show(InBattle);
            worldLayer.Show(InBattle);
            menuLayer.Show(CurrentMenu != Menu.None);
            menuLayer.pickingMode = CurrentMenu != Menu.None ? PickingMode.Position : PickingMode.Ignore;

            FitCamera(portrait);
            if (InBattle)
            {
                UpdateTopBar();
                UpdateSidePanel();
                UpdateWorldOverlays();
            }
            // Settings opened from the pause menu or camp close when play resumes.
            if (CurrentMenu == Menu.None && SettingsOpen && !Controller.Paused && !Controller.InCamp) SettingsOpen = false;
            UpdateModal();
        }

        /// <summary>Keeps the board clear of the HUD by shrinking the camera viewport to the area the panels leave free.</summary>
        void FitCamera(bool portrait)
        {
            var cam = Controller.Camera;
            if (cam == null) return;
            float w = root.layout.width, h = root.layout.height;
            if (!InBattle || float.IsNaN(w) || w <= 0 || h <= 0)
            {
                cam.rect = new Rect(0, 0, 1, 1);
                return;
            }
            float top = topBar.layout.height / h;
            if (float.IsNaN(top)) top = 0;
            if (portrait)
            {
                float panel = side.layout.height / h;
                if (float.IsNaN(panel)) panel = 0.46f;
                cam.rect = new Rect(0, panel, 1, Mathf.Max(0.1f, 1 - panel - top));
            }
            else
            {
                float panel = side.layout.width / w;
                if (float.IsNaN(panel)) panel = 0.3f;
                cam.rect = new Rect(0, 0, Mathf.Max(0.1f, 1 - panel), Mathf.Max(0.1f, 1 - top));
            }
        }

        // ---------- world overlays: unit tags and floating numbers ----------

        sealed class UnitTag
        {
            public VisualElement Root, Fill;
            public Label Text;
        }

        sealed class Floater
        {
            public Label Label;
            public Vector3 World;
            public float Born;
            public Color Color;
        }

        readonly Dictionary<Unit, UnitTag> tags = new Dictionary<Unit, UnitTag>();
        readonly List<Floater> floaters = new List<Floater>();
        Battle taggedBattle;
        const float FloaterLife = 0.95f;

        public void AddFloater(Vector3 world, string text, Color color, bool big = false)
        {
            if (!built) return;
            var label = Ui.Text(text, "floater").Into(worldLayer);
            label.EnableInClassList("floater--big", big);
            label.style.color = color;
            label.Ignore();
            floaters.Add(new Floater { Label = label, World = world + Vector3.up * 1.1f, Born = Time.unscaledTime, Color = color });
        }

        void UpdateWorldOverlays()
        {
            var cam = Controller.Camera;
            if (taggedBattle != Battle)
            {
                foreach (var t in tags.Values) t.Root.RemoveFromHierarchy();
                tags.Clear();
                taggedBattle = Battle;
            }

            foreach (var u in Battle.Units)
            {
                bool visible = ShowUnitBars && u.Alive && Controller.Board.TryGetUnitTransform(u, out var tr);
                if (!tags.TryGetValue(u, out var tag))
                {
                    if (!visible) continue;
                    tag = new UnitTag { Root = Ui.Box("unit-tag").Into(worldLayer) };
                    tag.Root.Ignore();
                    tag.Text = Ui.Text("", "unit-tag-text").Into(tag.Root);
                    tag.Text.Ignore();
                    var bar = Ui.Bar(out tag.Fill).Into(tag.Root);
                    bar.Ignore();
                    tag.Fill.Ignore();
                    tags[u] = tag;
                }
                tag.Root.Show(visible);
                if (!visible) continue;
                Controller.Board.TryGetUnitTransform(u, out tr);
                var screen = cam.WorldToScreenPoint(tr.position + Vector3.up * Controller.Board.BarHeight(u));
                if (screen.z < 0)
                {
                    tag.Root.Show(false);
                    continue;
                }
                var p = ToPanel(screen);
                tag.Root.style.left = p.x;
                tag.Root.style.top = p.y - 24;
                var status = new[]
                {
                    u.Rooted ? "PIN" : null, u.Poison > 0 ? "PSN" : null, u.Marked > 0 ? "MRK" : null, u.ArmorBuff > 0 ? "SHD" : null,
                    u.IsEnemy && !u.Awake ? "zz" : null,
                }.Where(x => x != null);
                tag.Text.SetText(string.Join(" ", new[] { u.Glyph }.Concat(status)));
                tag.Text.style.color = u.IsEnemy ? new Color(1f, .78f, .78f) : Color.white;
                tag.Fill.SetFill((float)u.Hp / u.MaxHp, u.IsEnemy ? Palette.AttackTarget : Palette.Heal);
            }

            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                float age = Time.unscaledTime - f.Born;
                if (age > FloaterLife || Battle == null)
                {
                    f.Label.RemoveFromHierarchy();
                    floaters.RemoveAt(i);
                    continue;
                }
                var screen = cam.WorldToScreenPoint(f.World + Vector3.up * age * 0.8f);
                f.Label.Show(screen.z > 0);
                var p = ToPanel(screen);
                f.Label.style.left = p.x;
                f.Label.style.top = p.y - 16;
                f.Label.style.opacity = 1 - age / FloaterLife;
            }
        }

        /// <summary>Camera pixel coordinates (bottom-left origin) to panel coordinates.</summary>
        Vector2 ToPanel(Vector3 screen)
        {
            var target = Controller.Camera.targetTexture;
            return target != null ? ScreenToPanel(screen, target.width, target.height) : ScreenToPanel(screen, Screen.width, Screen.height);
        }

        /// <summary>
        /// Pixels (bottom-left origin) on a surface of the given size to panel coordinates (top-left origin). The panel
        /// covers the whole surface, so this is a flip and a scale by the panel's own size.
        /// </summary>
        Vector2 ScreenToPanel(Vector2 pixels, float surfaceWidth, float surfaceHeight)
        {
            float w = root.layout.width, h = root.layout.height;
            if (float.IsNaN(w) || w <= 0 || surfaceWidth <= 0) return new Vector2(pixels.x, surfaceHeight - pixels.y);
            return new Vector2(pixels.x * w / surfaceWidth, (surfaceHeight - pixels.y) * h / surfaceHeight);
        }

        void ClearWorldOverlays()
        {
            foreach (var t in tags.Values) t.Root.RemoveFromHierarchy();
            tags.Clear();
            foreach (var f in floaters) f.Label.RemoveFromHierarchy();
            floaters.Clear();
            taggedBattle = null;
        }

        // ---------- shared text helpers ----------

        /// <summary>An item's name in its rarity color.</summary>
        static string RarityName(GearItem item) => Ui.Colored(item.Name, Palette.ForRarity(item.Rarity));

        /// <summary>A hero as they'd start the next floor: base stats plus level bonuses plus gear.</summary>
        static Unit PreviewHero(UnitDef def, int level, Loadout loadout)
        {
            var u = new Unit(-1, def, default) { Level = level };
            Progression.ApplyTo(u, level);
            Gear.ApplyTo(u, loadout);
            return u;
        }

        static string StatsText(Unit u) =>
            $"Health {u.MaxHp} · Damage {u.Damage} · Armor {u.Armor} · Speed {u.Speed} · Move {u.Move} · " +
            $"Crit {Mathf.RoundToInt((float)u.Crit * 100)}% +{Mathf.RoundToInt((float)u.CritDamage * 100)}%";

        static string StatLine(GearItem item) => string.Join(", ", item.Stats.Select(s => s.ToString()));

        static string Clock(float seconds)
        {
            int secs = Mathf.CeilToInt(seconds);
            return $"{secs / 60}:{secs % 60:00}";
        }
    }
}
