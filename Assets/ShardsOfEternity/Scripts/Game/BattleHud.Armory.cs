using System;
using System.Collections.Generic;
using System.Linq;
using ShardsOfEternity.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShardsOfEternity.Game
{
    /// <summary>The Armory between runs, plus the gear columns and XP bar it shares with the camp and end screens.</summary>
    public sealed partial class BattleHud
    {
        int armoryHero;
        GearItem confirmDiscard;

        void BuildArmory()
        {
            menuLayer.Clear();
            var screen = Ui.Box("layer", "menu-screen").Into(menuLayer);
            Decorate(screen);
            if (SettingsOpen)
            {
                BuildSettings(screen);
                return;
            }
            var profile = Controller.Profile;
            var modal = Ui.Box("panel", "modal", "camp").Into(screen);

            var header = Ui.Row().Into(modal);
            var titles = Ui.Box("grow").Into(header);
            Ui.Text("Armory", "h1").Into(titles);
            Ui.Text("Heroes level up from every run they join. Gear equipped here goes into the rift with them.", "small", "muted").Into(titles);
            Ui.Btn("Back", ShowTitle, "btn-small", "btn-ghost").Into(header);

            Ui.Text($"Runs {profile.RunsPlayed} · won {profile.RunsWon} · deepest floor {profile.DeepestFloor} · " +
                $"enemies slain {profile.EnemiesSlain} · vault {profile.Vault.Count} / {Profile.VaultCap}", "tiny").Into(modal).style.marginTop = 4;

            var heroes = Roster.Heroes;
            armoryHero = Mathf.Clamp(armoryHero, 0, heroes.Count - 1);
            var tabs = Ui.Box("tabs").Into(modal);
            for (int i = 0; i < heroes.Count; i++)
            {
                int index = i;
                var def = heroes[i];
                var tab = Ui.Btn($"{def.Name} · Lv {profile.Hero(def.Id).Level}", () => { armoryHero = index; confirmDiscard = null; BuildArmory(); }, "tab").Into(tabs);
                tab.EnableInClassList("tab--on", i == armoryHero);
                tab.style.borderBottomColor = Palette.ForUnit(new Unit(-1, def, default));
                tab.style.borderBottomWidth = 3;
            }

            var hero = heroes[armoryHero];
            var progress = profile.Hero(hero.Id);
            var loadout = profile.LoadoutFor(hero.Id);
            var levelRow = Ui.Row().Into(modal);
            levelRow.style.alignItems = Align.Center;
            Ui.Text($"{hero.Name}, level {progress.Level} {hero.HeroClass}", "small").Into(levelRow).style.unityFontStyleAndWeight = FontStyle.Bold;
            var bar = XpBar(progress).Into(levelRow);
            bar.style.flexGrow = 1;
            bar.style.marginLeft = 10;
            Ui.Text($"Level bonus: {Progression.BonusText(progress.Level)}. With gear: {StatsText(PreviewHero(hero, progress.Level, loadout))}",
                "tiny").Into(modal);

            GearColumns(modal, loadout, profile.Vault, "VAULT",
                "The vault is empty. Gear found on runs lands here: everything after a win, and whatever the heroes wore after a loss.",
                item => { profile.Equip(hero.Id, item); confirmDiscard = null; Controller.SaveProfile(); },
                slot => { profile.Unequip(hero.Id, slot); Controller.SaveProfile(); },
                item =>
                {
                    if (confirmDiscard != item)
                    {
                        confirmDiscard = item;
                        BuildArmory();
                        return;
                    }
                    confirmDiscard = null;
                    profile.Discard(item);
                    Controller.SaveProfile();
                    BuildArmory();
                });
        }

        /// <summary>
        /// Equipped slots on the left and spare items on the right (stacked on narrow screens), with equip, remove and
        /// optional discard buttons. Equip and remove rebuild whatever screen hosts the columns; discard handles its own.
        /// </summary>
        void GearColumns(VisualElement parent, Loadout loadout, List<GearItem> spares, string sparesTitle, string emptyText,
            Action<GearItem> equip, Action<GearSlot> unequip, Action<GearItem> discard)
        {
            var columns = Ui.Box("columns").Into(parent);
            columns.style.marginTop = 8;
            bool narrow = root.layout.width < 760;
            columns.style.flexDirection = narrow ? FlexDirection.Column : FlexDirection.Row;

            var equipped = Ui.Box("column").Into(columns);
            Ui.Text("EQUIPPED", "h3").Into(equipped);
            var equippedScroll = Ui.VScroll("grow").Into(equipped);
            foreach (GearSlot slot in Enum.GetValues(typeof(GearSlot)))
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
                Ui.Btn("Remove", () => { unequip(s); modalDirty = true; RebuildMenu(); }, "btn-small", "btn-ghost").Into(row);
            }

            var stash = Ui.Box("column").Into(columns);
            Ui.Text($"{sparesTitle} ({spares.Count})", "h3").Into(stash);
            var stashScroll = Ui.VScroll("grow").Into(stash);
            if (spares.Count == 0) Ui.Text(emptyText, "small", "muted").Into(stashScroll.contentContainer);
            foreach (var item in spares.OrderByDescending(i => i.Rarity).ThenBy(i => i.Slot).ThenByDescending(i => i.ItemLevel).ToList())
            {
                var card = Ui.Box("card", "row").Into(stashScroll.contentContainer);
                Ui.Box("item-rarity").Into(card).style.backgroundColor = Palette.ForRarity(item.Rarity);
                var text = Ui.Box("grow").Into(card);
                Ui.Text(RarityName(item), "small").Into(text).style.unityFontStyleAndWeight = FontStyle.Bold;
                Ui.Text($"{item.Rarity} {Gear.SlotName(item.Slot)} · level {item.ItemLevel}", "tiny").Into(text);
                Ui.Text(StatLine(item), "small").Into(text);
                Ui.Text(Compare(item, loadout.WouldReplace(item)), "tiny").Into(text);
                var buttons = Ui.Box().Into(card);
                Ui.Btn("Equip", () => { equip(item); modalDirty = true; RebuildMenu(); }, "btn-small").Into(buttons);
                if (discard != null)
                    Ui.Btn(confirmDiscard == item ? "Sure?" : "Discard", () => discard(item), "btn-small", "btn-ghost").Into(buttons);
            }
        }

        /// <summary>A hero's progress toward the next level, with the numbers on top.</summary>
        static VisualElement XpBar(HeroProgress progress)
        {
            var box = Ui.Box();
            var bar = Ui.Bar(out var fill).Into(box);
            bar.style.height = 6;
            fill.SetFill(progress.IsMaxLevel ? 1 : (float)progress.Xp / progress.XpToNext, Palette.Xp);
            Ui.Text(progress.IsMaxLevel ? "Max level" : $"{progress.Xp} / {progress.XpToNext} XP", "tiny", "muted").Into(box);
            return box;
        }
    }
}
