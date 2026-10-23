using System.Collections.Generic;
using System.Linq;
using ShardsOfEternity.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShardsOfEternity.Game
{
    /// <summary>Full-screen menus (title, squad) and the modal screens (pause, settings, camp, end of run).</summary>
    public sealed partial class BattleHud
    {
        // ---------- title ----------

        void BuildTitle()
        {
            menuLayer.Clear();
            var screen = Ui.Box("layer", "menu-screen").Into(menuLayer);
            Decorate(screen);
            if (SettingsOpen)
            {
                BuildSettings(screen);
                return;
            }
            var column = Ui.Box("menu-column").Into(screen);
            Ui.Text("SHARDS OF ETERNITY", "title-xl").Into(column);
            Ui.Text("Lead four heroes down through the rift. Seal it before it swallows the world.", "tagline").Into(column);
            Ui.Btn("New run", ShowSquad, "btn-primary", "btn-wide").Into(column);
            Ui.Btn("Settings", () => OpenSettings(true), "btn-wide").Into(column);
            Ui.Btn("Quit", Application.Quit, "btn-ghost", "btn-wide").Into(column);
            Ui.Text("Combat prototype", "tiny", "footer").Into(screen);
        }

        /// <summary>Faint rotated diamonds behind the menus: the shards of the title.</summary>
        static void Decorate(VisualElement screen)
        {
            var shards = new (float x, float y, float size, bool gold)[]
            {
                (8, 12, 120, false), (78, 8, 70, true), (88, 62, 150, false), (14, 70, 60, true), (52, 84, 40, false), (40, 6, 28, true),
            };
            foreach (var (x, y, size, gold) in shards)
            {
                var s = Ui.Box("shard").Into(screen);
                s.EnableInClassList("shard--gold", gold);
                s.style.left = Length.Percent(x);
                s.style.top = Length.Percent(y);
                s.style.width = size;
                s.style.height = size;
                s.Ignore();
            }
        }

        // ---------- squad ----------

        void BuildSquad()
        {
            menuLayer.Clear();
            var screen = Ui.Box("layer", "menu-screen").Into(menuLayer);
            Decorate(screen);
            if (SettingsOpen)
            {
                BuildSettings(screen);
                return;
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical).With("screen-scroll").Into(screen);
            var body = Ui.Box("squad-body").Into(scroll.contentContainer);

            var header = Ui.Row("wrap").Into(body);
            var titles = Ui.Box("grow").Into(header);
            Ui.Text("Assemble your squad", "h1").Into(titles);
            Ui.Text($"Pick 4 of the {Roster.Heroes.Count} classes. {Controller.Squad.Count} / 4 chosen.", "body", "muted").Into(titles);

            var grid = Ui.Box("hero-grid").Into(body);
            grid.style.marginTop = 12;
            foreach (var def in Roster.Heroes) HeroCard(def).Into(grid);

            var options = Ui.Box("row", "wrap").Into(body);
            options.style.alignItems = Align.FlexStart;
            options.style.marginTop = 14;

            var runBox = Ui.Box("panel").Into(options);
            runBox.style.flexGrow = 1;
            runBox.style.minWidth = 300;
            runBox.style.marginRight = 10;
            runBox.style.marginBottom = 10;
            Ui.Text("RUN", "h3").Into(runBox);
            var seg = Ui.Box("seg").Into(runBox);
            Segment(seg, "Dungeon · 3-4 floors", Controller.RunKind == RunKind.Dungeon, () => Controller.RunKind = RunKind.Dungeon);
            Segment(seg, $"Raid · {DungeonRun.RaidFloors} floors", Controller.RunKind == RunKind.Raid, () => Controller.RunKind = RunKind.Raid);
            Ui.Text(Controller.RunKind == RunKind.Raid
                ? "A long descent through every rift theme, 3 floors each. Enemies get tougher as you go."
                : "A short descent in one rift theme. The Rift Lord waits on the last floor.", "small", "muted").Into(runBox).style.marginTop = 8;

            var rules = Ui.Box("panel").Into(options);
            rules.style.flexGrow = 1;
            rules.style.minWidth = 300;
            rules.style.marginBottom = 10;
            Ui.Text("RULES", "h3").Into(rules);
            Ui.Check("Action shards (experimental): 2 per turn, spend them on any mix of moves and actions", Controller.UseActionShards,
                v => { Controller.UseActionShards = v; BuildSquad(); }).Into(rules);
            Ui.Check("Bank unspent shards, up to 4", Controller.BankShards, v => { Controller.BankShards = v; BuildSquad(); },
                Controller.UseActionShards).Into(rules).style.marginLeft = 24;
            Ui.Check("Fast reinforcement timer (testing): every 2 minutes", Controller.FastTestTimer,
                v => { Controller.FastTestTimer = v; BuildSquad(); }).Into(rules);
            Ui.Check("Floor 1 is the browser prototype's room", Controller.HandmadeFirstFloor,
                v => { Controller.HandmadeFirstFloor = v; BuildSquad(); }).Into(rules);

            var actions = Ui.Row("wrap").Into(body);
            actions.style.justifyContent = Justify.FlexEnd;
            actions.style.marginTop = 6;
            Ui.Btn("Back", ShowTitle, "btn-ghost").Into(actions).style.minWidth = 120;
            var go = Ui.Btn(Controller.SquadReady ? "Enter the rift" : $"Pick {4 - Controller.Squad.Count} more", StartRun, "btn-primary").Into(actions);
            go.style.minWidth = 220;
            go.SetEnabled(Controller.SquadReady);
        }

        VisualElement HeroCard(UnitDef def)
        {
            int slot = Controller.Squad.IndexOf(def.Id);
            var card = new Button(() =>
            {
                if (slot >= 0) Controller.Squad.RemoveAt(slot);
                else if (Controller.Squad.Count < 4) Controller.Squad.Add(def.Id);
                BuildSquad();
            }) { focusable = false }.With("hero-card");
            card.EnableInClassList("hero-card--picked", slot >= 0);
            Ui.Box("hero-band").Into(card).style.backgroundColor = Palette.ForUnit(new Unit(-1, def, default));
            var inner = Ui.Box("hero-inner").Into(card);
            var top = Ui.Row().Into(inner);
            var names = Ui.Box("grow").Into(top);
            Ui.Text(def.Name, "h2").Into(names);
            Ui.Text(def.HeroClass.ToString(), "small", "muted").Into(names);
            if (slot >= 0) Ui.Text((slot + 1).ToString(), "badge").Into(top);
            var stats = Ui.Box("stat-row").Into(inner);
            foreach (var s in new[] { $"HP {def.Hp}", $"DMG {def.Damage}", $"ARM {def.Armor}", $"SPD {def.Speed}", $"MOV {def.Move}", $"RNG {def.Range}" })
                Ui.Text(s, "stat").Into(stats);
            Ui.Text(def.Rule, "small", "muted").Into(inner);
            var abilities = AbilityLibrary.For(def.HeroClass);
            Ui.Text(string.Join(" · ", abilities.Select(a => a.IsUltimate ? $"<color=#E8C26A>{a.Name}</color>" : a.Name)), "tiny").Into(inner).style.marginTop = 6;
            return card;
        }

        void Segment(VisualElement seg, string text, bool on, System.Action pick)
        {
            var b = Ui.Btn(text, () => { pick(); BuildSquad(); }).Into(seg);
            b.EnableInClassList("seg--on", on);
        }

        // ---------- settings ----------

        void BuildSettings(VisualElement parent)
        {
            var scrim = parent.ClassListContains("menu-screen") ? parent : Ui.Box("layer", "scrim").Into(parent);
            if (scrim != parent) scrim.pickingMode = PickingMode.Position;
            var modal = Ui.Box("panel", "modal").Into(scrim);
            modal.style.width = 560;
            var scroll = new ScrollView(ScrollViewMode.Vertical).Into(modal);
            var c = scroll.contentContainer;
            Ui.Text("Settings", "h1").Into(c);

            Ui.Text("INTERFACE SIZE", "h3").Into(c);
            var seg = Ui.Box("seg").Into(c);
            foreach (var s in UiScales)
            {
                var b = Ui.Btn($"{Mathf.RoundToInt(s * 100)}%", () => { SetUiScale(s); RefreshSettings(); }).Into(seg);
                b.EnableInClassList("seg--on", Mathf.Approximately(UiScale, s));
            }
            Ui.Text("DISPLAY", "h3").Into(c);
            Ui.Check("Health bars and status tags over units", ShowUnitBars, v => { SetShowUnitBars(v); RefreshSettings(); }).Into(c);

            Ui.Text("CONTROLS", "h3").Into(c);
            foreach (var (key, what) in new[]
            {
                ("Left click / tap", "Move, attack, or confirm a target"),
                ("1 - 4", "Pick an ability, press again to use or cancel"),
                ("E", "End turn"),
                ("P / Esc", "Pause menu (Esc cancels an ability first)"),
                ("Right drag · Z / X", "Rotate the camera"),
                ("Middle drag · WASD", "Pan the camera"),
                ("Scroll wheel", "Zoom"),
                ("F", "Re-center the camera"),
                ("Two fingers", "Pan, pinch to zoom, twist to rotate"),
            })
            {
                var row = Ui.Box("controls-row").Into(c);
                Ui.Text(key, "controls-key").Into(row);
                Ui.Text(what, "small", "grow").Into(row);
            }

            var actions = Ui.Box("modal-actions").Into(c);
            Ui.Btn("Back", () => OpenSettings(false), "btn-primary").Into(actions).style.minWidth = 160;
        }

        void RefreshSettings()
        {
            modalDirty = true;
            if (CurrentMenu != Menu.None) RebuildMenu();
        }

        // ---------- modal screens over the battle ----------

        string modalKey;
        bool modalDirty;
        VisualElement modalRoot;
        int campHero;

        string CurrentModal()
        {
            if (CurrentMenu != Menu.None || Battle == null) return null;
            if (Battle.Outcome == BattleOutcome.Victory || Battle.Outcome == BattleOutcome.Defeat) return "gameover";
            if (Controller.InCamp) return SettingsOpen ? "settings" : "camp";
            if (Battle.Outcome == BattleOutcome.FloorComplete) return "descend";
            if (Controller.Paused) return SettingsOpen ? "settings" : "pause";
            return null;
        }

        void UpdateModal()
        {
            string key = CurrentModal();
            if (key == modalKey && !modalDirty) return;
            modalKey = key;
            modalDirty = false;
            overlayLayer.Clear();
            modalRoot = null;
            if (key == null) return;
            if (key == "descend")
            {
                var banner = Ui.Box("banner").Into(overlayLayer);
                banner.Ignore();
                Ui.Text($"Descending to floor {Controller.Run.Floor + 1}...", "banner-text").Into(banner);
                return;
            }
            var scrim = Ui.Box("layer", "scrim").Into(overlayLayer);
            scrim.pickingMode = PickingMode.Position;
            modalRoot = scrim;
            switch (key)
            {
                case "pause": BuildPause(scrim); break;
                case "settings": BuildSettings(scrim); break;
                case "camp": BuildCamp(scrim); break;
                case "gameover": BuildGameOver(scrim); break;
            }
        }

        void BuildPause(VisualElement scrim)
        {
            var modal = Ui.Box("panel", "modal").Into(scrim);
            modal.style.width = 380;
            Ui.Text("Paused", "h1").Into(modal);
            Ui.Text("The battle and the reinforcement timer are stopped.", "body", "muted").Into(modal).style.marginBottom = 12;
            Ui.Btn("Resume", () => Controller.SetPaused(false), "btn-primary").Into(modal);
            Ui.Btn("Settings", () => OpenSettings(true)).Into(modal);
            Ui.Btn("Restart run", StartRun).Into(modal);
            Ui.Btn("Abandon run", AbandonRun, "btn-danger").Into(modal);
        }

        /// <summary>Ends the current run and goes back to the title screen.</summary>
        public void AbandonRun()
        {
            Controller.EndRun();
            ClearWorldOverlays();
            ShowTitle();
            modalDirty = true;
        }

        void BuildGameOver(VisualElement scrim)
        {
            bool win = Battle.Outcome == BattleOutcome.Victory;
            var run = Controller.Run;
            run.CollectLoot();
            var modal = Ui.Box("panel", "modal").Into(scrim);
            modal.style.width = 440;
            var title = Ui.Text(win ? "Rift sealed" : "Squad defeated", "h1").Into(modal);
            title.style.color = win ? Palette.Crit : Palette.AttackTarget;
            Ui.Text(win
                ? $"The Rift Lord is dead and all {run.FloorCount} floors are cleared. {Battle.Heroes.Count()} of 4 heroes are still standing."
                : $"The rift claimed your squad on floor {run.Floor} of {run.FloorCount}.", "body").Into(modal);
            var best = run.Found.OrderByDescending(i => i.Rarity).ThenByDescending(i => i.ItemLevel).FirstOrDefault();
            Ui.Text($"Loot found: {run.Found.Count} item{(run.Found.Count == 1 ? "" : "s")}" +
                (best != null ? $". Best: {RarityName(best)}" : "."), "body", "muted").Into(modal).style.marginTop = 8;
            var actions = Ui.Box("modal-actions").Into(modal);
            Ui.Btn("New run", StartRun, "btn-primary").Into(actions).style.flexGrow = 1;
            Ui.Btn("Change squad", () => { Controller.EndRun(); ClearWorldOverlays(); ShowSquad(); }).Into(actions).style.flexGrow = 1;
            Ui.Btn("Main menu", AbandonRun, "btn-ghost").Into(actions).style.flexGrow = 1;
        }

        // ---------- camp ----------

        void BuildCamp(VisualElement scrim)
        {
            var run = Controller.Run;
            var modal = Ui.Box("panel", "modal", "camp").Into(scrim);
            var header = Ui.Row().Into(modal);
            var titles = Ui.Box("grow").Into(header);
            Ui.Text($"Camp · floor {run.Floor} of {run.FloorCount} cleared", "h1").Into(titles);
            Ui.Text("Equip what you found before going deeper. Health lost stays lost; new Health from gear is added on top.", "small", "muted").Into(titles);
            Ui.Btn("Settings", () => OpenSettings(true), "btn-small", "btn-ghost").Into(header);

            var heroes = run.Battle.Units.Where(u => u.IsHero).ToList();
            campHero = Mathf.Clamp(campHero, 0, heroes.Count - 1);
            var tabs = Ui.Box("tabs").Into(modal);
            for (int i = 0; i < heroes.Count; i++)
            {
                int index = i;
                var hu = heroes[i];
                var tab = Ui.Btn($"{hu.Name}{(hu.Alive ? "" : " (fallen)")}", () => { campHero = index; modalDirty = true; }, "tab").Into(tabs);
                tab.EnableInClassList("tab--on", i == campHero);
                tab.style.borderBottomColor = Palette.ForUnit(hu);
                tab.style.borderBottomWidth = 3;
            }

            var hero = heroes[campHero];
            string id = hero.Def.Id;
            var loadout = run.LoadoutFor(id);
            var preview = new Unit(-1, hero.Def, default);
            Gear.ApplyTo(preview, loadout);
            Ui.Text($"Next floor: Damage {preview.Damage} · Armor {preview.Armor} · Health {preview.MaxHp} · Speed {preview.Speed} · " +
                $"Move {preview.Move} · Crit {Mathf.RoundToInt((float)preview.Crit * 100)}% +{Mathf.RoundToInt((float)preview.CritDamage * 100)}%", "small").Into(modal);

            var columns = Ui.Box("columns").Into(modal);
            columns.style.marginTop = 8;
            bool narrow = root.layout.width < 760;
            columns.style.flexDirection = narrow ? FlexDirection.Column : FlexDirection.Row;

            var equipped = Ui.Box("column").Into(columns);
            Ui.Text("EQUIPPED", "h3").Into(equipped);
            var equippedScroll = new ScrollView(ScrollViewMode.Vertical).With("grow").Into(equipped);
            foreach (GearSlot slot in System.Enum.GetValues(typeof(GearSlot)))
            {
                var item = loadout[slot];
                var row = Ui.Box("slot-row").Into(equippedScroll.contentContainer);
                Ui.Text(Gear.SlotName(slot), "slot-name").Into(row);
                if (item == null)
                {
                    Ui.Text("empty", "tiny").Into(row);
                    continue;
                }
                var text = Ui.Box("grow").Into(row);
                Ui.Text(RarityName(item), "small").Into(text);
                Ui.Text(StatLine(item), "tiny").Into(text);
                var s = slot;
                Ui.Btn("Remove", () => { run.Unequip(id, s); modalDirty = true; }, "btn-small", "btn-ghost").Into(row);
            }

            var stash = Ui.Box("column").Into(columns);
            Ui.Text($"STASH ({run.Stash.Count})", "h3").Into(stash);
            var stashScroll = new ScrollView(ScrollViewMode.Vertical).With("grow").Into(stash);
            if (run.Stash.Count == 0)
                Ui.Text("Nothing here yet. Kill enemies and walk over their loot beams to pick items up.", "small", "muted").Into(stashScroll.contentContainer);
            foreach (var item in run.Stash.OrderByDescending(i => i.Rarity).ThenBy(i => i.Slot).ToList())
            {
                var card = Ui.Box("card", "row").Into(stashScroll.contentContainer);
                Ui.Box("item-rarity").Into(card).style.backgroundColor = Palette.ForRarity(item.Rarity);
                var text = Ui.Box("grow").Into(card);
                Ui.Text(RarityName(item), "small").Into(text).style.unityFontStyleAndWeight = FontStyle.Bold;
                Ui.Text($"{item.Rarity} {Gear.SlotName(item.Slot)} · level {item.ItemLevel}", "tiny").Into(text);
                Ui.Text(StatLine(item), "small").Into(text);
                Ui.Text(Compare(item, loadout.WouldReplace(item)), "tiny").Into(text);
                Ui.Btn("Equip", () => { run.Equip(id, item); modalDirty = true; }, "btn-small").Into(card);
            }

            var actions = Ui.Box("modal-actions").Into(modal);
            var next = run.Floor + 1;
            var go = Ui.Btn($"Descend to floor {next}{(next == run.FloorCount ? "  ·  the boss waits here" : "")}",
                () => Controller.LeaveCamp(), "btn-primary").Into(actions);
            go.style.flexGrow = 1;
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
    }
}
