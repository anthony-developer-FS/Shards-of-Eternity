using System.Linq;
using ShardsOfEternity.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShardsOfEternity.Game
{
    /// <summary>The battle HUD: turn order and run status along the top, the current unit's card on the side.</summary>
    public sealed partial class BattleHud
    {
        const int TimelineLength = 10, LogLines = 8, PressurePips = 12;

        VisualElement topBar, side;
        Label runLabel, timerLabel;
        Button pauseButton;
        readonly VisualElement[] chips = new VisualElement[TimelineLength];
        readonly VisualElement[] chipBands = new VisualElement[TimelineLength];
        readonly Label[] chipNames = new Label[TimelineLength], chipWhen = new Label[TimelineLength];

        Label unitName, unitSubtitle, hpText, statsLabel, ruleLabel, turnState, previewLabel, pressureLabel, roomLabel;
        VisualElement hpFill, shardRow, abilityList, heroControls;
        readonly VisualElement[] shardPips = new VisualElement[4];
        readonly VisualElement[] pressurePips = new VisualElement[PressurePips];
        readonly Button[] abilityButtons = new Button[4];
        readonly Label[] abilityKeys = new Label[4], abilityNames = new Label[4], abilityNotes = new Label[4];
        readonly Label[] logLines = new Label[LogLines];
        Button endTurnButton;
        int hoveredAbility = -1;

        void BuildBattleHud()
        {
            topBar = Ui.Box("topbar").Into(battleLayer);
            var timeline = Ui.Box("timeline").Into(topBar);
            for (int i = 0; i < TimelineLength; i++)
            {
                var chip = Ui.Box("chip").Into(timeline);
                chipBands[i] = Ui.Box("chip-band").Into(chip);
                var text = Ui.Box("chip-text").Into(chip);
                chipNames[i] = Ui.Text("", "chip-name").Into(text);
                chipWhen[i] = Ui.Text("", "chip-when").Into(text);
                chips[i] = chip;
            }
            var status = Ui.Box("run-status").Into(topBar);
            var statusText = Ui.Box().Into(status);
            statusText.style.marginRight = 8;
            runLabel = Ui.Text("", "small").Into(statusText);
            runLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            timerLabel = Ui.Text("", "timer").Into(statusText);
            pauseButton = Ui.Btn("Pause", () => Controller.SetPaused(true), "btn-small").Into(status);

            side = Ui.Box("side").Into(battleLayer);
            var scroll = new ScrollView(ScrollViewMode.Vertical).With("grow").Into(side);
            var c = scroll.contentContainer;

            var header = Ui.Row().Into(c);
            unitName = Ui.Text("", "h1").Into(header);
            unitSubtitle = Ui.Text("", "small", "muted").Into(header);
            unitSubtitle.style.marginLeft = 10;
            var hpRow = Ui.Box("hp-row").Into(c);
            Ui.Bar(out hpFill).Into(hpRow);
            hpText = Ui.Text("", "hp-text").Into(hpRow);
            statsLabel = Ui.Text("", "small").Into(c);
            ruleLabel = Ui.Text("", "small", "muted", "italic").Into(c);
            ruleLabel.style.marginTop = 4;

            heroControls = Ui.Box().Into(c);
            heroControls.style.marginTop = 8;
            turnState = Ui.Text("", "small").Into(heroControls);
            shardRow = Ui.Row("pips").Into(heroControls);
            for (int i = 0; i < shardPips.Length; i++) shardPips[i] = Ui.Box("pip").Into(shardRow);
            Ui.Text("Each move, attack or ability costs 1 shard.", "tiny").Into(shardRow).style.marginLeft = 6;

            abilityList = Ui.Box().Into(heroControls);
            abilityList.style.marginTop = 6;
            for (int i = 0; i < abilityButtons.Length; i++)
            {
                int index = i;
                var b = new Button(() => Controller.ToggleAbility(index)) { focusable = false }.With("ability").Into(abilityList);
                abilityKeys[i] = Ui.Text((i + 1).ToString(), "key").Into(b);
                var text = Ui.Box("ability-text").Into(b);
                abilityNames[i] = Ui.Text("", "ability-name").Into(text);
                abilityNotes[i] = Ui.Text("", "ability-note").Into(text);
                b.RegisterCallback<PointerEnterEvent>(_ => hoveredAbility = index);
                b.RegisterCallback<PointerLeaveEvent>(_ => { if (hoveredAbility == index) hoveredAbility = -1; });
                abilityButtons[i] = b;
            }
            endTurnButton = Ui.Btn("", () => Controller.EndTurn(), "btn-primary").Into(heroControls);
            endTurnButton.style.marginLeft = 0;
            endTurnButton.style.marginRight = 0;

            previewLabel = Ui.Text("", "body").With("preview").Into(c);

            Ui.Text("RIFT PRESSURE", "h3").Into(c);
            pressureLabel = Ui.Text("", "small").Into(c);
            var pressure = Ui.Box("pressure").Into(c);
            for (int i = 0; i < PressurePips; i++) pressurePips[i] = Ui.Box("pressure-pip").Into(pressure);

            Ui.Text("LOG", "h3").Into(c);
            for (int i = 0; i < LogLines; i++) logLines[i] = Ui.Text("", "log-line").Into(c);

            var footer = Ui.Row("wrap").Into(c);
            footer.style.marginTop = 10;
            Ui.Btn("Menu", () => Controller.SetPaused(true), "btn-small").Into(footer);
            roomLabel = Ui.Text("", "tiny", "grow").Into(footer);
            roomLabel.style.marginLeft = 8;
        }

        void UpdateTopBar()
        {
            var items = Battle.Forecast(TimelineLength);
            for (int i = 0; i < TimelineLength; i++)
            {
                bool has = i < items.Count;
                chips[i].Show(has);
                if (!has) continue;
                var it = items[i];
                chips[i].EnableInClassList("chip--now", i == 0);
                chips[i].EnableInClassList("chip--enemy", it.Unit.IsEnemy);
                chipBands[i].style.backgroundColor = Palette.ForUnit(it.Unit);
                chipNames[i].SetText(it.Unit.Name);
                chipWhen[i].SetText(i == 0 ? "now" : "+" + Mathf.RoundToInt((float)(it.At - Battle.Clock)));
            }

            var run = Controller.Run;
            runLabel.SetText($"{(run.Kind == RunKind.Raid ? "Raid" : "Dungeon")} · Floor {run.Floor}/{run.FloorCount}");
            bool urgent = false;
            string timer;
            if (Battle.FloorCleared) timer = "Room clear";
            else if (!Battle.InCombat) timer = "Room asleep";
            else
            {
                timer = "Reinforcements " + Clock(Controller.ReinforcementCountdown);
                urgent = Controller.ReinforcementCountdown <= 15;
            }
            timerLabel.SetText(timer);
            timerLabel.EnableInClassList("timer--urgent", urgent);
        }

        void UpdateSidePanel()
        {
            var u = Battle.Current;
            if (u == null) return;
            bool hero = u.IsHero;
            bool canAct = hero && !Controller.Busy && !Controller.Paused && !Battle.IsOver;

            unitName.SetText(u.Name);
            unitSubtitle.SetText(u.Def.Subtitle);
            hpFill.SetFill((float)u.Hp / u.MaxHp, hero ? Palette.Heal : Palette.AttackTarget);
            hpText.SetText($"{u.Hp} / {u.MaxHp}");
            statsLabel.SetText($"Damage {u.Damage} · Armor {u.EffectiveArmor} · Speed {u.Speed} · Move {u.Move} · Range {u.Range} · " +
                $"Crit {Mathf.RoundToInt((float)u.Crit * 100)}% +{Mathf.RoundToInt((float)u.CritDamage * 100)}%");
            ruleLabel.SetText(u.Def.Rule);

            heroControls.Show(hero);
            if (hero)
            {
                bool shards = Battle.Rules.UseActionShards;
                shardRow.Show(shards);
                turnState.Show(!shards);
                if (shards)
                    for (int i = 0; i < shardPips.Length; i++)
                    {
                        shardPips[i].Show(i < Battle.Rules.ShardCap);
                        shardPips[i].EnableInClassList("pip--full", i < u.Shards);
                    }
                else
                    turnState.SetText($"Move <b>{(u.Moved ? "used" : "ready")}</b>   ·   Action <b>{(u.Acted ? "used" : "ready")}</b>");

                for (int i = 0; i < abilityButtons.Length; i++) UpdateAbility(u, i, canAct);
                endTurnButton.text = $"End turn  (E)   ·   next turn in {Mathf.RoundToInt((float)u.TurnInterval + u.Recovery)}";
                endTurnButton.SetEnabled(canAct);
            }

            previewLabel.SetText(PreviewText());

            int p = Battle.Pressure;
            pressureLabel.SetText($"{(Battle.InCombat ? p.ToString() : "Dormant")} · {Battle.NextPressureEvent()}");
            var accent = Palette.Hex(Battle.Layout.Theme.AccentColor);
            for (int i = 0; i < PressurePips; i++)
            {
                bool marked = i + 1 == 2 || (i + 1 >= Battle.FirstShift && (i + 1 - Battle.FirstShift) % 4 == 0);
                bool filled = i < Mathf.Min(p, PressurePips);
                var color = marked ? accent : Palette.AbilityTarget;
                pressurePips[i].style.backgroundColor = filled ? color : new Color(color.r, color.g, color.b, 0.18f);
            }

            var log = Battle.Log;
            for (int i = 0; i < LogLines; i++)
            {
                logLines[i].Show(i < log.Count);
                if (i < log.Count) logLines[i].SetText(log[i]);
                logLines[i].EnableInClassList("log-line--latest", i == 0);
            }
            string room = Battle.Layout.Seed >= 0 ? $"{Battle.Layout.Theme.Name} · seed {Battle.Layout.Seed}" : "Prototype room";
            roomLabel.SetText(room);
        }

        void UpdateAbility(Unit u, int i, bool canAct)
        {
            var b = abilityButtons[i];
            b.Show(i < u.Abilities.Length);
            if (i >= u.Abilities.Length) return;
            var a = u.Abilities[i];
            bool selected = Controller.AbilityIndex == i;
            bool hasTargets = Battle.AbilityTargets(u, i).Count > 0;
            string note = selected ? (a.Target == TargetKind.Self ? "Press again to use" : "Pick a target, or press again to cancel")
                : a.IsUltimate ? (u.Ultimate >= Battle.UltimateMax ? (hasTargets ? "Ultimate ready" : "No target") : $"Ultimate {u.Ultimate}%")
                : u.Cooldowns[i] > 0 ? $"Ready in {u.Cooldowns[i]} turn{(u.Cooldowns[i] > 1 ? "s" : "")}"
                : !Battle.CanAct(u) ? "Action used" : hasTargets ? "Ready" : "No target";
            abilityNames[i].SetText(a.Name);
            abilityNotes[i].SetText(note);
            b.EnableInClassList("ability--selected", selected);
            b.EnableInClassList("ability--ultimate", a.IsUltimate);
            b.EnableInClassList("ability--ready", canAct && hasTargets);
            b.SetEnabled(canAct && (hasTargets || selected));
        }

        string PreviewText()
        {
            var u = Battle.Current;
            if (Battle.IsOver || u == null) return "";
            if (u.IsEnemy) return $"{u.Name} is acting.";
            if (hoveredAbility >= 0 && hoveredAbility < u.Abilities.Length && Controller.AbilityIndex < 0)
            {
                var h = u.Abilities[hoveredAbility];
                return $"<b>{h.Name}</b>{(h.IsUltimate ? " (ultimate)" : h.Cooldown > 0 ? $" · cooldown {h.Cooldown}" : "")}: {h.Description}";
            }
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
                    return $"Attack {t.Name} ({t.Hp} HP): <b>{i.Damage}</b> damage, {Mathf.RoundToInt((float)i.CritChance * 100)}% chance of a " +
                        $"{i.CritDamage} critical{(i.Flanked ? ", flanking bonus included" : "")}.{(i.Damage >= t.Hp ? " <b>This kills it.</b>" : "")}" +
                        (Controller.Pending.HasValue ? " Tap again to strike." : "");
                }
                var m = options.Moves.FirstOrDefault(o => o.Target == focus.Value);
                if (m != null)
                {
                    string s = m.Kind == MoveKind.Charge ? $"Charge {m.Distance} tiles in a straight line."
                        : m.Kind == MoveKind.Swap ? $"Swap places with {m.Ally.Name}."
                        : $"Walk {m.Distance} tile{(m.Distance > 1 ? "s" : "")}.";
                    var free = Battle.FreeAttackers(u, m);
                    if (free.Count > 0) s += $" {Ui.Colored("Careful:", Palette.AttackTarget)} {string.Join(" and ", free.Select(e => e.Name))} will get a free attack.";
                    if (Battle.CrossesSpikes(u, m)) s += $" Spikes deal {Battle.SpikeDamage} damage.";
                    var loot = m.Path.Where(p => Battle.GroundLoot.ContainsKey(p)).SelectMany(p => Battle.GroundLoot[p]).ToList();
                    if (loot.Count > 0) s += $" Picks up {string.Join(", ", loot.Select(RarityName))}.";
                    if (Battle.Map[m.Target] == TileType.Exit)
                    {
                        s += " Leads the squad to the next floor.";
                        int left = Battle.GroundLootCount - loot.Count;
                        if (left > 0) s += " " + Ui.Colored($"{left} item{(left > 1 ? "s are" : " is")} still on the floor and will be left behind.", new Color(1f, .48f, .43f));
                    }
                    return s;
                }
                if (Battle.GroundLoot.TryGetValue(focus.Value, out var pile))
                    return "Loot: " + string.Join(", ", pile.Select(RarityName)) + ". Walk a hero over it to pick it up.";
                if (t != null && t.IsEnemy)
                {
                    string s = $"<b>{t.Name}</b>: {t.Hp}/{t.MaxHp} HP, Damage {t.Damage}, Armor {t.Armor}, Speed {t.Speed}, Move {t.Move}. {t.Def.Rule}";
                    if (t.Rooted) s += " Pinned: it cannot move next turn.";
                    if (t.Poison > 0) s += $" Poisoned for {t.Poison} more turn{(t.Poison > 1 ? "s" : "")}.";
                    if (t.Marked > 0) s += " Marked: +3 damage from every hit.";
                    return s;
                }
            }
            if (Battle.CurrentTurnSpent) return "Turn complete.";
            if (Battle.FloorCleared) return "Room clear. Walk any hero onto the green rift door to go to the next floor.";
            if (!Battle.InCombat) return "Explore the room. The enemies wake when a hero comes within 5 tiles in sight.";
            return "Click a blue tile to move or a red enemy to attack. Keys 1-4 pick abilities, E ends the turn.";
        }
    }
}
