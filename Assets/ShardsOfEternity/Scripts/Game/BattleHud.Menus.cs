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
            Ui.Btn("Armory", ShowArmory, "btn-wide").Into(column);
            Ui.Btn("Settings", () => OpenSettings(true), "btn-wide").Into(column);
            Ui.Btn("Quit", Application.Quit, "btn-ghost", "btn-wide").Into(column);
            var p = Controller.Profile;
            if (p.RunsPlayed > 0)
                Ui.Text($"{p.RunsPlayed} run{(p.RunsPlayed == 1 ? "" : "s")} · {p.RunsWon} won · deepest floor {p.DeepestFloor}", "small", "muted", "center")
                    .Into(column).style.marginTop = 10;
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
            var scroll = Ui.VScroll("screen-scroll").Into(screen);
            var body = Ui.Box("squad-body").Into(scroll.contentContainer);

            var header = Ui.Row("wrap").Into(body);
            var titles = Ui.Box("grow").Into(header);
            Ui.Text("Assemble your squad", "h1").Into(titles);
            Ui.Text($"Pick 4 of the {Roster.Heroes.Count} classes. {Controller.Squad.Count} / 4 chosen.", "body", "muted").Into(titles);

            var grid = Ui.Box("hero-grid").Into(body);
            grid.style.marginTop = 12;
            foreach (var def in Roster.Heroes) HeroCard(def).Into(grid);

            var options = Ui.Box("options").Into(body);

            var runBox = Ui.Box("panel", "option-panel").Into(options);
            Ui.Text("RUN", "h3").Into(runBox);
            var seg = Ui.Box("seg").Into(runBox);
            Segment(seg, "Dungeon · 3-4 floors", Controller.RunKind == RunKind.Dungeon, () => Controller.RunKind = RunKind.Dungeon);
            Segment(seg, $"Raid · {DungeonRun.RaidFloors} floors", Controller.RunKind == RunKind.Raid, () => Controller.RunKind = RunKind.Raid);
            Ui.Text(Controller.RunKind == RunKind.Raid
                ? "A long descent through every rift theme, 3 floors each. Enemies get tougher as you go."
                : "A short descent in one rift theme. The Rift Lord waits on the last floor.", "small", "muted").Into(runBox).style.marginTop = 8;

            var rules = Ui.Box("panel", "option-panel").Into(options);
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
            var progress = Controller.Profile.Hero(def.Id);
            Ui.Text($"Level {progress.Level} {def.HeroClass}", "small", "muted").Into(names);
            if (slot >= 0) Ui.Text((slot + 1).ToString(), "badge").Into(top);
            XpBar(progress).Into(inner).style.marginTop = 4;
            var u = PreviewHero(def, progress.Level, Controller.Profile.LoadoutFor(def.Id));
            var stats = Ui.Box("stat-row").Into(inner);
            foreach (var s in new[] { $"HP {u.MaxHp}", $"DMG {u.Damage}", $"ARM {u.Armor}", $"SPD {u.Speed}", $"MOV {u.Move}", $"RNG {u.Range}" })
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
            var scroll = Ui.VScroll().Into(modal);
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

            if (CurrentMenu != Menu.None)
            {
                Ui.Text("PROGRESS", "h3").Into(c);
                Ui.Text($"Hero levels, gear and run stats are saved on this device and kept between sessions.", "small", "muted").Into(c);
                var reset = Ui.Btn(confirmReset ? "Click again to erase every level and item" : "Reset progress", () =>
                {
                    if (!confirmReset)
                    {
                        confirmReset = true;
                        RefreshSettings();
                        return;
                    }
                    confirmReset = false;
                    Controller.ResetProgress();
                    RefreshSettings();
                }, "btn-danger").Into(c);
                reset.style.alignSelf = Align.FlexStart;
                reset.style.marginTop = 6;
            }

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
            Ui.Btn("Back", () => { confirmReset = false; OpenSettings(false); }, "btn-primary").Into(actions).style.minWidth = 160;
        }

        bool confirmReset;

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
            var result = Controller.FinishRun();
            var modal = Ui.Box("panel", "modal").Into(scrim);
            modal.style.width = 480;
            var title = Ui.Text(win ? "Rift sealed" : "Squad defeated", "h1").Into(modal);
            title.style.color = win ? Palette.Crit : Palette.AttackTarget;
            Ui.Text(win
                ? $"The Rift Lord is dead and all {run.FloorCount} floors are cleared. {Battle.Heroes.Count()} of 4 heroes are still standing."
                : $"The rift claimed your squad on floor {run.Floor} of {run.FloorCount}.", "body").Into(modal);
            var best = run.Found.OrderByDescending(i => i.Rarity).ThenByDescending(i => i.ItemLevel).FirstOrDefault();
            Ui.Text($"Loot found: {run.Found.Count} item{(run.Found.Count == 1 ? "" : "s")}" +
                (best != null ? $". Best: {RarityName(best)}" : "."), "body", "muted").Into(modal).style.marginTop = 8;
            if (result != null)
            {
                if (result.Lost.Count > 0)
                    Ui.Text($"Kept {result.Kept.Count}: gear the heroes had on. Lost {result.Lost.Count} left in the stash when the squad fell.",
                        "small", "muted").Into(modal);
                else if (result.Kept.Count > 0)
                    Ui.Text($"All {result.Kept.Count} finds are yours: equipped, or waiting in the Armory.", "small", "muted").Into(modal);

                Ui.Text($"+{result.Xp} XP FOR EACH HERO", "h3").Into(modal).style.color = Palette.Xp;
                foreach (var h in result.Heroes)
                {
                    var def = Roster.Hero(h.Id);
                    var row = Ui.Row().Into(modal);
                    row.style.alignItems = Align.Center;
                    row.style.marginBottom = 4;
                    var name = Ui.Text(def.Name, "small").Into(row);
                    name.style.width = 70;
                    name.style.color = Palette.ForUnit(new Unit(-1, def, default));
                    var level = Ui.Text(h.LeveledUp ? $"Level {h.LevelBefore} to {h.LevelAfter}" : $"Level {h.LevelAfter}", "small").Into(row);
                    level.style.width = 110;
                    if (h.LeveledUp)
                    {
                        level.style.color = Palette.Crit;
                        level.style.unityFontStyleAndWeight = FontStyle.Bold;
                    }
                    XpBar(Controller.Profile.Hero(h.Id)).Into(row).style.flexGrow = 1;
                }
            }
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
            var preview = PreviewHero(hero.Def, run.LevelOf(id), loadout);
            Ui.Text($"Level {preview.Level} · next floor: {StatsText(preview)}", "small").Into(modal);

            GearColumns(modal, loadout, run.Stash, "STASH",
                "Nothing here yet. Kill enemies and walk over their loot beams to pick items up.",
                item => run.Equip(id, item), slot => run.Unequip(id, slot), null);

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
