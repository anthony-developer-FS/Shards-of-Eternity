using System.Collections.Generic;
using System.Linq;
using ShardsOfEternity.Core;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Temporary immediate-mode HUD: timeline, unit card, abilities, previews, rift pressure, log and the squad picker.
    /// It scales with screen size and moves the panel below the board in portrait. It is meant to be replaced by a
    /// proper UI Toolkit or uGUI layout once the rules settle.
    /// </summary>
    public sealed class BattleHud : MonoBehaviour
    {
        const float TopBarHeight = 58f;
        const float PanelWidth = 380f;

        public BattleController Controller;

        readonly List<Rect> hudRects = new List<Rect>();
        readonly List<Floater> floaters = new List<Floater>();
        float scale = 1f;
        Vector2 panelScroll;
        GUIStyle label, small, title, chip, button, box, floater, unitTag, bigFloater;

        struct Floater
        {
            public Vector3 World;
            public string Text;
            public Color Color;
            public float Born;
            public bool Big;
        }

        Battle Battle => Controller.Battle;

        public void AddFloater(Vector3 world, string text, Color color, bool big = false) =>
            floaters.Add(new Floater { World = world + Vector3.up * 1.1f, Text = text, Color = color, Born = Time.time, Big = big });

        /// <summary>True if a screen position (bottom-left origin, like Input.mousePosition) is over the HUD.</summary>
        public bool IsOverHud(Vector2 screen)
        {
            var guiPoint = new Vector2(screen.x / scale, (Screen.height - screen.y) / scale);
            return hudRects.Any(r => r.Contains(guiPoint));
        }

        bool Portrait => Screen.height > Screen.width;

        void Update()
        {
            scale = Mathf.Max(1f, Mathf.Min(Screen.width, Screen.height) / 720f);
            float vw = Screen.width / scale, vh = Screen.height / scale;
            if (Controller.Camera == null) return;
            // Keep the board clear of the HUD by shrinking the camera viewport.
            if (Portrait)
            {
                float panel = PortraitPanelHeight(vh) / vh;
                Controller.Camera.rect = new Rect(0, panel, 1, 1 - panel - TopBarHeight / vh);
            }
            else
            {
                Controller.Camera.rect = new Rect(0, 0, 1 - PanelWidth / vw, 1 - TopBarHeight / vh);
            }
        }

        static float PortraitPanelHeight(float vh) => Mathf.Round(vh * 0.46f);

        void Styles()
        {
            if (label != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true, richText = true };
            small = new GUIStyle(label) { fontSize = 12 };
            title = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };
            chip = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.MiddleCenter, richText = true };
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, richText = true, wordWrap = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 6, 6) };
            box = new GUIStyle(GUI.skin.box) { fontSize = 14, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
            floater = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            bigFloater = new GUIStyle(floater) { fontSize = 26 };
            unitTag = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
        }

        void OnGUI()
        {
            Styles();
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float vw = Screen.width / scale, vh = Screen.height / scale;
            hudRects.Clear();

            Rect top = new Rect(0, 0, vw, TopBarHeight);
            Rect panel = Portrait
                ? new Rect(0, vh - PortraitPanelHeight(vh), vw, PortraitPanelHeight(vh))
                : new Rect(vw - PanelWidth, 0, PanelWidth, vh);
            if (!Portrait) top.width = vw - PanelWidth;
            hudRects.Add(top);
            hudRects.Add(panel);

            if (Battle != null)
            {
                DrawUnitLabels();
                DrawFloaters();
            }

            GUI.Box(top, GUIContent.none);
            GUI.Box(panel, GUIContent.none);
            if (Battle != null) DrawTimeline(top);

            GUILayout.BeginArea(new Rect(panel.x + 10, panel.y + 10, panel.width - 20, panel.height - 20));
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            if (!Controller.SquadReady || Battle == null) DrawSquadPicker();
            else DrawBattlePanel();
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (Battle != null && (Battle.Outcome == BattleOutcome.Victory || Battle.Outcome == BattleOutcome.Defeat)) DrawGameOver(vw, vh);
            else if (Controller.InCamp) DrawCamp(vw, vh);
            else if (Battle != null && Battle.Outcome == BattleOutcome.FloorComplete) DrawBanner(vw, vh, $"Descending to floor {Controller.Run.Floor + 1}...");
            else if (Controller.Paused) DrawPaused(vw, vh);

            // Changes that add or remove controls run after drawing, so IMGUI's layout pass stays consistent.
            var action = deferred;
            deferred = null;
            action?.Invoke();
        }

        System.Action deferred;

        Rect CenterRect(float vw, float vh, float w, float h)
        {
            var rect = new Rect(vw / 2 - w / 2, vh / 2 - h / 2, w, h);
            if (!Portrait) rect.x = (vw - PanelWidth) / 2 - w / 2;
            return rect;
        }

        void DrawBanner(float vw, float vh, string text)
        {
            var rect = CenterRect(vw, vh, 320, 60);
            GUI.Box(rect, GUIContent.none);
            GUI.Label(rect, $"<b>{text}</b>", new GUIStyle(title) { alignment = TextAnchor.MiddleCenter });
        }

        void DrawPaused(float vw, float vh)
        {
            var rect = CenterRect(vw, vh, 300, 130);
            hudRects.Add(rect);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 12, rect.width - 32, rect.height - 24));
            GUILayout.Label("Paused", title);
            GUILayout.Label("The reinforcement timer is stopped.", label);
            if (GUILayout.Button("Resume [P]", GUILayout.MinHeight(36))) Controller.SetPaused(false);
            GUILayout.EndArea();
        }

        // ---------- board overlays ----------

        void DrawUnitLabels()
        {
            var cam = Controller.Camera;
            foreach (var u in Battle.Units)
            {
                if (!u.Alive || !Controller.Board.TryGetUnitTransform(u, out var t)) continue;
                var s = cam.WorldToScreenPoint(t.position + Vector3.up * 1.15f);
                if (s.z < 0) continue;
                var gui = new Vector2(s.x / scale, (Screen.height - s.y) / scale);
                var rect = new Rect(gui.x - 40, gui.y - 22, 80, 18);
                var status = string.Join(" ", new[]
                {
                    u.Rooted ? "PIN" : null, u.Poison > 0 ? "PSN" : null, u.Marked > 0 ? "MRK" : null, u.ArmorBuff > 0 ? "SHD" : null,
                    u.IsEnemy && !u.Awake ? "z z" : null,
                }.Where(x => x != null));
                GUI.color = u.IsEnemy ? new Color(1f, .8f, .8f) : Color.white;
                GUI.Label(rect, $"<b>{u.Glyph}</b> {status}", unitTag);
                GUI.color = Color.white;
                Bar(new Rect(gui.x - 20, gui.y - 4, 40, 5), (float)u.Hp / u.MaxHp, u.IsEnemy ? Palette.AttackTarget : Palette.Heal);
            }
        }

        void DrawFloaters()
        {
            var cam = Controller.Camera;
            floaters.RemoveAll(f => Time.time - f.Born > 0.95f);
            foreach (var f in floaters)
            {
                float age = Time.time - f.Born;
                var s = cam.WorldToScreenPoint(f.World + Vector3.up * age * 0.8f);
                if (s.z < 0) continue;
                var gui = new Vector2(s.x / scale, (Screen.height - s.y) / scale);
                var c = f.Color;
                c.a = 1 - age / 0.95f;
                GUI.color = c;
                GUI.Label(new Rect(gui.x - 80, gui.y - 16, 160, 32), f.Text, f.Big ? bigFloater : floater);
            }
            GUI.color = Color.white;
        }

        static void Bar(Rect r, float fraction, Color color)
        {
            GUI.color = new Color(0, 0, 0, .7f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(fraction), r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        // ---------- top bar ----------

        void DrawTimeline(Rect top)
        {
            const float statusWidth = 250f;
            var status = new Rect(top.xMax - statusWidth, top.y, statusWidth, top.height);
            DrawRunStatus(status);
            top.width -= statusWidth;

            var items = Battle.Forecast(10);
            float x = top.x + 8, w = Mathf.Min(86, (top.width - 16) / Mathf.Max(1, items.Count) - 4);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                string when = i == 0 ? "now" : "+" + Mathf.RoundToInt((float)(it.At - Battle.Clock));
                GUI.color = it.Unit.IsEnemy ? new Color(1f, .7f, .7f) : (i == 0 ? Palette.ActiveRing : Color.white);
                GUI.Box(new Rect(x, top.y + 8, w, top.height - 16), $"<b>{it.Unit.Glyph}</b> {it.Unit.Name}\n{when}", chip);
                x += w + 4;
            }
            GUI.color = Color.white;
        }

        /// <summary>Floor counter, the reinforcement countdown and the pause button.</summary>
        void DrawRunStatus(Rect r)
        {
            var run = Controller.Run;
            string timer;
            if (Battle.FloorCleared) timer = "Room clear";
            else if (!Battle.InCombat) timer = "Reinforcements: room asleep";
            else
            {
                int secs = Mathf.CeilToInt(Controller.ReinforcementCountdown);
                string clock = $"{secs / 60}:{secs % 60:00}";
                timer = secs <= 15 ? $"<color=#FF7A6E>Reinforcements {clock}</color>" : $"Reinforcements {clock}";
            }
            GUI.Label(new Rect(r.x, r.y + 6, r.width - 70, 22), $"<b>{(run.Kind == RunKind.Raid ? "Raid" : "Dungeon")} · floor {run.Floor}/{run.FloorCount}</b>", label);
            GUI.Label(new Rect(r.x, r.y + 28, r.width - 70, 22), timer, label);
            if (GUI.Button(new Rect(r.xMax - 66, r.y + 10, 58, r.height - 20), Controller.Paused ? "Resume" : "Pause"))
                Controller.SetPaused(!Controller.Paused);
        }

        // ---------- side panel ----------

        void DrawSquadPicker()
        {
            GUILayout.Label("Pick your squad", title);
            GUILayout.Label($"Choose 4 of the {Roster.Heroes.Count} human classes. {Controller.Squad.Count}/4 picked.", label);
            foreach (var def in Roster.Heroes)
            {
                int slot = Controller.Squad.IndexOf(def.Id);
                string text = $"<b>{def.HeroClass}</b>  {def.Name} · Move {def.Move} · Speed {def.Speed} · Range {def.Range}" +
                    (slot >= 0 ? $"   [{slot + 1}]" : "");
                GUI.color = slot >= 0 ? Palette.ActiveRing : Color.white;
                if (GUILayout.Button(text, button, GUILayout.MinHeight(36)))
                {
                    if (slot >= 0) Controller.Squad.RemoveAt(slot);
                    else if (Controller.Squad.Count < 4) Controller.Squad.Add(def.Id);
                }
                GUI.color = Color.white;
            }
            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(Controller.RunKind == RunKind.Dungeon, " Dungeon (3-4 floors)")) Controller.RunKind = RunKind.Dungeon;
            if (GUILayout.Toggle(Controller.RunKind == RunKind.Raid, $" Raid ({DungeonRun.RaidFloors} floors)")) Controller.RunKind = RunKind.Raid;
            GUILayout.EndHorizontal();
            Controller.HandmadeFirstFloor = GUILayout.Toggle(Controller.HandmadeFirstFloor, " Floor 1 is the browser prototype's room");
            Controller.UseActionShards = GUILayout.Toggle(Controller.UseActionShards, " Action shards (experimental): 2 per turn, spend on any mix of moves and actions");
            GUI.enabled = Controller.UseActionShards;
            Controller.BankShards = GUILayout.Toggle(Controller.BankShards, "     Bank unspent shards, up to 4");
            GUI.enabled = true;
            Controller.FastTestTimer = GUILayout.Toggle(Controller.FastTestTimer, " Fast 2-min timer (testing): skip the 10 and 5 minute waits");
            GUI.enabled = Controller.SquadReady;
            if (GUILayout.Button("<b>Enter the rift</b>", button, GUILayout.MinHeight(44))) deferred = Controller.NewRun;
            GUI.enabled = true;
        }

        void DrawBattlePanel()
        {
            var u = Battle.Current;
            if (u == null) return;
            bool hero = u.IsHero;
            bool canAct = hero && !Controller.Busy && !Battle.IsOver;

            GUILayout.Label($"{u.Name}  <size=13>{u.Def.Subtitle}</size>", title);
            var hpRect = GUILayoutUtility.GetRect(10, 16, GUILayout.ExpandWidth(true));
            Bar(new Rect(hpRect.x, hpRect.y + 4, hpRect.width - 70, 10), (float)u.Hp / u.MaxHp, hero ? Palette.Heal : Palette.AttackTarget);
            GUI.Label(new Rect(hpRect.xMax - 64, hpRect.y - 2, 64, 20), $"{u.Hp}/{u.MaxHp}", small);
            GUILayout.Label($"Damage {u.Damage} · Armor {u.EffectiveArmor} · Atk speed {u.Speed} · Move {u.Move} · Range {u.Range} · " +
                $"Crit {Mathf.RoundToInt((float)u.Crit * 100)}% +{Mathf.RoundToInt((float)u.CritDamage * 100)}%", small);
            GUILayout.Label($"<i>{u.Def.Rule}</i>", small);

            if (hero)
            {
                if (Battle.Rules.UseActionShards)
                    GUILayout.Label($"Action shards <b><color=#B07CF0>{new string('◆', u.Shards)}</color>{new string('◇', Mathf.Max(0, Battle.Rules.ShardCap - u.Shards))}</b>  <size=12>each move, attack or ability costs 1</size>", label);
                else
                    GUILayout.Label($"Move <b>{(u.Moved ? "used" : "ready")}</b> · Action <b>{(u.Acted ? "used" : "ready")}</b>", label);
                for (int i = 0; i < u.Abilities.Length; i++) AbilityButton(u, i, canAct);
                GUI.enabled = canAct;
                if (GUILayout.Button($"<b>[E] End turn</b>  <size=12>next turn in {Mathf.RoundToInt((float)u.TurnInterval + u.Recovery)}</size>", button, GUILayout.MinHeight(36)))
                    Controller.EndTurn();
                GUI.enabled = true;
            }

            GUILayout.Space(4);
            GUILayout.Label(PreviewText(), box);

            GUILayout.Space(6);
            int p = Battle.Pressure;
            GUILayout.Label($"Rift pressure <b>{(Battle.InCombat ? p.ToString() : "dormant")}</b> · {Battle.NextPressureEvent()}", small);
            var pips = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true));
            float pw = pips.width / 12f;
            for (int i = 0; i < 12; i++)
            {
                bool marked = i + 1 == 2 || (i + 1 >= Battle.FirstShift && (i + 1 - Battle.FirstShift) % 4 == 0);
                Bar(new Rect(pips.x + i * pw + 1, pips.y, pw - 2, 8), i < Mathf.Min(p, 12) ? 1 : 0,
                    marked ? Palette.Hex(Battle.Layout.Theme.AccentColor) : Palette.AbilityTarget);
            }

            GUILayout.Space(6);
            foreach (var line in Battle.Log.Take(8)) GUILayout.Label(line, small);

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New run")) deferred = Controller.NewRun;
            if (GUILayout.Button("Change squad / rules"))
            {
                deferred = () => { Controller.Squad.Clear(); Controller.NewRun(); };
            }
            GUILayout.EndHorizontal();
            string room = Battle.Layout.Seed >= 0 ? $"{Battle.Layout.Theme.Name}, seed {Battle.Layout.Seed}" : "Prototype room";
            GUILayout.Label($"Room: {room}", small);
        }

        void AbilityButton(Unit u, int i, bool canAct)
        {
            var a = u.Abilities[i];
            bool selected = Controller.AbilityIndex == i;
            bool hasTargets = Battle.AbilityTargets(u, i).Count > 0;
            string note = selected ? (a.Target == TargetKind.Self ? "press again to use" : "tap to cancel")
                : a.IsUltimate ? (u.Ultimate >= Battle.UltimateMax ? (hasTargets ? "ultimate ready" : "no target") : $"ultimate {u.Ultimate}%")
                : u.Cooldowns[i] > 0 ? $"ready in {u.Cooldowns[i]} turn{(u.Cooldowns[i] > 1 ? "s" : "")}"
                : u.Acted ? "action used" : hasTargets ? "ready" : "no target";
            GUI.enabled = canAct && (hasTargets || selected);
            GUI.color = selected ? Palette.AbilityTarget : a.IsUltimate ? Palette.Crit : Color.white;
            if (GUILayout.Button(new GUIContent($"<b>[{i + 1}] {a.Name}</b>  <size=12>{note}</size>", a.Description), button, GUILayout.MinHeight(34)))
                Controller.ToggleAbility(i);
            GUI.color = Color.white;
            GUI.enabled = true;
        }

        string PreviewText()
        {
            var u = Battle.Current;
            if (Battle.IsOver || u == null) return "";
            if (u.IsEnemy) return $"{u.Name} is acting.";
            var options = Controller.Options;
            if (options == null) return "Resolving.";
            var focus = Controller.Pending ?? Controller.Hover;
            string confirm = Controller.Pending.HasValue ? " Tap again to confirm." : "";

            if (Controller.AbilityIndex >= 0)
            {
                var a = u.Abilities[Controller.AbilityIndex];
                if (a.Target == TargetKind.Self)
                    return $"<b>{a.Name}:</b> {Battle.AbilityPreview(u, Controller.AbilityIndex, u.Position)} Press it again, or tap {u.Name}, to use it.";
                if (!focus.HasValue || !options.AbilityTargets.Contains(focus.Value))
                    return $"<b>{a.Name}:</b> {a.Description} Pick a highlighted target.";
                return $"<b>{a.Name}:</b> {Battle.AbilityPreview(u, Controller.AbilityIndex, focus.Value)}{confirm}";
            }

            if (focus.HasValue)
            {
                var t = Battle.UnitAt(focus.Value);
                if (t != null && options.Attacks.Contains(t))
                {
                    var i = Battle.GetDamageInfo(u, t);
                    return $"Attack {t.Name} ({t.Hp} HP): {i.Damage} damage, {Mathf.RoundToInt((float)i.CritChance * 100)}% chance of a " +
                        $"{i.CritDamage} critical{(i.Flanked ? ", flanking bonus included" : "")}.{(i.Damage >= t.Hp ? " This kills it." : "")}" +
                        (Controller.Pending.HasValue ? " Tap again to strike." : "");
                }
                var m = options.Moves.FirstOrDefault(o => o.Target == focus.Value);
                if (m != null)
                {
                    string s = m.Kind == MoveKind.Charge ? $"Charge {m.Distance} tiles in a straight line."
                        : m.Kind == MoveKind.Swap ? $"Swap places with {m.Ally.Name}."
                        : $"Walk {m.Distance} tile{(m.Distance > 1 ? "s" : "")}.";
                    var free = Battle.FreeAttackers(u, m);
                    if (free.Count > 0) s += $" Careful: {string.Join(" and ", free.Select(e => e.Name))} will get a free attack.";
                    if (Battle.CrossesSpikes(u, m)) s += $" Spikes deal {Battle.SpikeDamage} damage.";
                    var loot = m.Path.Where(p => Battle.GroundLoot.ContainsKey(p)).SelectMany(p => Battle.GroundLoot[p]).ToList();
                    if (loot.Count > 0) s += $" Picks up {string.Join(", ", loot.Select(RarityName))}.";
                    if (Battle.Map[m.Target] == TileType.Exit)
                    {
                        s += " Leads the squad to the next floor.";
                        int left = Battle.GroundLootCount - loot.Count;
                        if (left > 0) s += $" <color=#FF7A6E>{left} item{(left > 1 ? "s are" : " is")} still on the floor and will be left behind.</color>";
                    }
                    return s;
                }
                if (Battle.GroundLoot.TryGetValue(focus.Value, out var pile))
                    return "Loot: " + string.Join(", ", pile.Select(RarityName)) + ". Walk a hero over it to pick it up.";
                if (t != null && t.IsEnemy)
                {
                    string s = $"{t.Name}: {t.Hp}/{t.MaxHp} HP, Damage {t.Damage}, Armor {t.Armor}, Attack Speed {t.Speed}, Move {t.Move}. {t.Def.Rule}";
                    if (t.Rooted) s += " Pinned: it cannot move next turn.";
                    if (t.Poison > 0) s += $" Poisoned for {t.Poison} more turn{(t.Poison > 1 ? "s" : "")}.";
                    if (t.Marked > 0) s += " Marked: +3 damage from every hit.";
                    return s;
                }
            }
            if (Battle.CurrentTurnSpent) return "Turn complete.";
            if (Battle.FloorCleared) return "Room clear. Walk any hero onto the green rift door to go to the next floor.";
            if (!Battle.InCombat) return "Explore the room. The enemies wake when a hero comes within 5 tiles in sight.";
            return "Click a highlighted tile to move, or a red enemy to attack. Keys 1-4 pick abilities, E ends the turn.";
        }

        static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

        /// <summary>An item's name in its rarity color.</summary>
        static string RarityName(GearItem item) => $"<color=#{Hex(Palette.ForRarity(item.Rarity))}>{item.Name}</color>";

        static string StatLine(GearItem item) => string.Join(", ", item.Stats.Select(s => s.ToString()));

        // ---------- camp (between floors) ----------

        int campHero;
        Vector2 campEquippedScroll, campStashScroll;

        /// <summary>Equip screen between floors: pick a hero, see their gear and stats, equip from the stash.</summary>
        void DrawCamp(float vw, float vh)
        {
            var run = Controller.Run;
            float w = Mathf.Min(vw - 24, 860), h = Mathf.Min(vh - 24, 580);
            var rect = new Rect((vw - w) / 2, (vh - h) / 2, w, h);
            hudRects.Add(rect);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 14, rect.y + 10, rect.width - 28, rect.height - 20));

            GUILayout.Label($"Camp · floor {run.Floor} of {run.FloorCount} cleared", title);
            GUILayout.Label($"Equip what you found before going deeper. Health lost stays lost; new Health from gear is added on top.", small);

            var heroes = run.Battle.Units.Where(u => u.IsHero).ToList();
            campHero = Mathf.Clamp(campHero, 0, heroes.Count - 1);
            GUILayout.BeginHorizontal();
            for (int i = 0; i < heroes.Count; i++)
            {
                var hu = heroes[i];
                GUI.color = i == campHero ? Palette.ActiveRing : Color.white;
                if (GUILayout.Button($"<b>{hu.Name}</b> {hu.Def.HeroClass}{(hu.Alive ? "" : " (fallen)")}", button, GUILayout.MinHeight(32))) campHero = i;
            }
            GUI.color = Color.white;
            GUILayout.EndHorizontal();

            var hero = heroes[campHero];
            string id = hero.Def.Id;
            var loadout = run.LoadoutFor(id);
            var preview = new Unit(-1, hero.Def, default);
            Gear.ApplyTo(preview, loadout);
            GUILayout.Label($"Next floor: Damage {preview.Damage} · Armor {preview.Armor} · Health {preview.MaxHp} · Attack Speed {preview.Speed} · " +
                $"Move {preview.Move} · Crit {Mathf.RoundToInt((float)preview.Crit * 100)}% +{Mathf.RoundToInt((float)preview.CritDamage * 100)}%", label);

            bool wide = w >= 640;
            if (wide) GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(wide ? w * 0.45f : w - 28));
            GUILayout.Label("<b>Equipped</b>", label);
            campEquippedScroll = GUILayout.BeginScrollView(campEquippedScroll, GUILayout.MinHeight(wide ? h - 210 : (h - 230) / 2));
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                var item = loadout[slot];
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{Gear.SlotName(slot)}:", small, GUILayout.Width(72));
                if (item == null) GUILayout.Label("<color=#777777>empty</color>", small);
                else
                {
                    GUILayout.Label($"{RarityName(item)}  <size=11>{StatLine(item)}</size>", small);
                    if (GUILayout.Button("Remove", GUILayout.Width(66))) deferred = () => run.Unequip(id, slot);
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label($"<b>Stash</b> ({run.Stash.Count})", label);
            campStashScroll = GUILayout.BeginScrollView(campStashScroll, GUILayout.MinHeight(wide ? h - 210 : (h - 230) / 2));
            if (run.Stash.Count == 0) GUILayout.Label("Nothing here yet. Kill enemies and walk over their loot beams to pick items up.", small);
            foreach (var item in run.Stash.OrderByDescending(i => i.Rarity).ThenBy(i => i.Slot).ToList())
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.BeginVertical();
                GUILayout.Label($"{RarityName(item)}  <size=11>{item.Rarity} {Gear.SlotName(item.Slot)} · level {item.ItemLevel}</size>", small);
                GUILayout.Label($"<size=11>{StatLine(item)}</size>", small);
                GUILayout.Label($"<size=11>{Compare(item, loadout.WouldReplace(item))}</size>", small);
                GUILayout.EndVertical();
                if (GUILayout.Button("Equip", GUILayout.Width(60), GUILayout.MinHeight(30))) deferred = () => run.Equip(id, item);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            if (wide) GUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            if (GUILayout.Button($"<b>Descend to floor {run.Floor + 1}</b>{(run.Floor + 1 == run.FloorCount ? "  <size=12>(the boss waits here)</size>" : "")}",
                    button, GUILayout.MinHeight(40)))
                deferred = Controller.LeaveCamp;
            GUILayout.EndArea();
        }

        /// <summary>Stat changes if this item replaced the equipped one: green for gains, red for losses.</summary>
        static string Compare(GearItem item, GearItem current)
        {
            var parts = new List<string>();
            foreach (StatType stat in System.Enum.GetValues(typeof(StatType)))
            {
                double delta = item.Total(stat) - (current?.Total(stat) ?? 0);
                if (System.Math.Abs(delta) < 1e-6) continue;
                bool pct = stat == StatType.CritChance || stat == StatType.CritDamage;
                string amount = pct ? $"{System.Math.Round(delta * 100):+0;-0}%" : $"{delta:+0;-0}";
                parts.Add($"<color={(delta > 0 ? "#6EE7A0" : "#FF7A6E")}>{amount} {Gear.StatName(stat)}</color>");
            }
            string vs = current == null ? "vs empty slot" : $"vs {current.Name}";
            return parts.Count == 0 ? $"No change {vs}" : $"{string.Join(", ", parts)} {vs}";
        }

        void DrawGameOver(float vw, float vh)
        {
            bool win = Battle.Outcome == BattleOutcome.Victory;
            var rect = CenterRect(vw, vh, 360, 210);
            hudRects.Add(rect);
            GUI.Box(rect, GUIContent.none);
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 16, rect.y + 12, rect.width - 32, rect.height - 24));
            var run = Controller.Run;
            GUILayout.Label(win ? "Rift sealed" : "Squad defeated", title);
            GUILayout.Label(win
                ? $"The Rift Lord is dead and all {run.FloorCount} floors are cleared. {Battle.Heroes.Count()} of 4 heroes standing."
                : $"The rift claimed your squad on floor {run.Floor} of {run.FloorCount}.", label);
            run.CollectLoot();
            var best = run.Found.OrderByDescending(i => i.Rarity).ThenByDescending(i => i.ItemLevel).FirstOrDefault();
            GUILayout.Label($"Loot found: {run.Found.Count} item{(run.Found.Count == 1 ? "" : "s")}" +
                (best != null ? $". Best: {RarityName(best)}" : ""), label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("New run", GUILayout.MinHeight(36))) deferred = Controller.NewRun;
            if (GUILayout.Button("Change squad / rules", GUILayout.MinHeight(36)))
            {
                deferred = () => { Controller.Squad.Clear(); Controller.NewRun(); };
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }
    }
}
