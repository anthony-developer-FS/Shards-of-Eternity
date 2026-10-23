using System;
using System.Linq;
using NUnit.Framework;
using ShardsOfEternity.Core;

namespace ShardsOfEternity.Tests
{
    public class GearAndLootTests
    {
        static GearItem Item(GearSlot slot, params StatRoll[] stats)
        {
            var item = new GearItem { Name = "Test " + slot, Slot = slot, Rarity = Rarity.Rare, ItemLevel = 1 };
            item.Stats.AddRange(stats);
            return item;
        }

        [Test]
        public void RolledItems_HaveOneStatPerRarityStep_FromTheirSlotPool()
        {
            var rng = new Random(11);
            for (int i = 0; i < 2000; i++)
            {
                var rarity = (Rarity)(i % 4);
                var item = LootTable.RollItem(rng, 1 + i % 12, rarity);
                Assert.AreNotEqual(GearSlot.Ring2, item.Slot, "Rings always roll as Ring1 and choose a finger on equip");
                int expected = (int)rarity + 1 + (rarity == Rarity.Legendary && item.Slot == GearSlot.Feet ? 1 : 0);
                Assert.LessOrEqual(item.Stats.Count, expected, item.ToString());
                Assert.GreaterOrEqual(item.Stats.Count, Math.Min(expected, 2) - 1, item.ToString());
                Assert.AreEqual(item.Stats.Count, item.Stats.Select(s => s.Stat).Distinct().Count(), "No duplicate stats");
                Assert.IsTrue(item.Stats.All(s => s.Value > 0), item.ToString());
                if (item.Slot == GearSlot.MainHand) Assert.AreEqual(StatType.Damage, item.Stats[0].Stat);
                Assert.IsFalse(string.IsNullOrEmpty(item.Name));
            }
        }

        [Test]
        public void DeeperFloors_RollBetterRarities()
        {
            int Legendaries(int floor)
            {
                var rng = new Random(3);
                return Enumerable.Range(0, 5000).Count(_ => LootTable.RollRarity(rng, floor) == Rarity.Legendary);
            }
            Assert.Greater(Legendaries(12), Legendaries(1) * 2);
        }

        [Test]
        public void Boss_AlwaysDropsTwoRareOrBetterItems()
        {
            var rng = new Random(5);
            var boss = new Unit(0, Roster.Enemy(EnemyKind.RiftLord), new Point(1, 1));
            for (int i = 0; i < 100; i++)
            {
                var drops = LootTable.RollDrops(rng, boss, 4);
                Assert.AreEqual(2, drops.Count);
                Assert.IsTrue(drops.All(d => d.Rarity >= Rarity.Rare));
                Assert.IsTrue(drops.All(d => d.ItemLevel == 4));
            }
        }

        [Test]
        public void Rings_FillBothRingSlotsBeforeReplacing()
        {
            var loadout = new Loadout();
            var a = Item(GearSlot.Ring1, new StatRoll(StatType.Damage, 1));
            var b = Item(GearSlot.Ring1, new StatRoll(StatType.Damage, 2));
            var c = Item(GearSlot.Ring1, new StatRoll(StatType.Damage, 3));
            Assert.IsNull(loadout.Equip(a));
            Assert.IsNull(loadout.Equip(b));
            Assert.AreSame(a, loadout[GearSlot.Ring1]);
            Assert.AreSame(b, loadout[GearSlot.Ring2]);
            Assert.AreSame(a, loadout.WouldReplace(c));
            Assert.AreSame(a, loadout.Equip(c));
            Assert.AreEqual(5, loadout.Total(StatType.Damage));
        }

        [Test]
        public void GearStats_FeedIntoCombat()
        {
            var u = new Unit(0, Roster.Hero("kael"), new Point(1, 1));
            var loadout = new Loadout();
            loadout.Equip(Item(GearSlot.MainHand, new StatRoll(StatType.Damage, 3), new StatRoll(StatType.CritChance, .05)));
            loadout.Equip(Item(GearSlot.Chest, new StatRoll(StatType.MaxHp, 6), new StatRoll(StatType.Armor, 2)));
            loadout.Equip(Item(GearSlot.Feet, new StatRoll(StatType.Speed, 2), new StatRoll(StatType.Move, 1)));
            Gear.ApplyTo(u, loadout);
            Assert.AreEqual(11, u.Damage);
            Assert.AreEqual(.2, u.Crit, 1e-9);
            Assert.AreEqual(36, u.MaxHp);
            Assert.AreEqual(36, u.Hp);
            Assert.AreEqual(4, u.Armor);
            Assert.AreEqual(13, u.Speed);
            Assert.AreEqual(5, u.Move);
            Assert.AreEqual(1000.0 / 13, u.NextTurnAt, 1e-9, "Attack Speed from gear moves the first turn earlier");
        }

        [Test]
        public void SlainEnemies_DropLootOnTheirTile_AndHeroesPickItUpByWalkingOverIt()
        {
            var layout = new RoomLayout { Map = GridMap.FromRows("########", "#......#", "########"), Theme = Theme.AshenCrypt };
            var b = new Battle(layout, new string[0], 1) { Roll = () => 1, LootEnabled = true };
            var kael = b.AddUnit(Roster.Hero("kael"), new Point(1, 1));
            var boss = b.AddUnit(Roster.Enemy(EnemyKind.RiftLord), new Point(2, 1));
            b.AddUnit(Roster.Enemy(EnemyKind.Ghoul), new Point(6, 1)).Hp = 999;
            b.Start();
            boss.Hp = 1;
            Assert.IsTrue(b.TryAttack(boss));
            Assert.AreEqual(2, b.GroundLootCount, "The boss always drops 2 items");
            Assert.IsTrue(b.GroundLoot.ContainsKey(new Point(2, 1)));
            Assert.IsTrue(b.DrainEvents().Count(e => e.Type == BattleEventType.LootDropped) == 2);

            Assert.IsTrue(b.TryMove(new Point(3, 1)), "Walking through the tile picks the loot up");
            Assert.AreEqual(0, b.GroundLootCount);
            Assert.AreEqual(2, b.PickedUp.Count);
        }

        [Test]
        public void Runs_StashPickedUpLoot_AndApplyEquippedGearOnTheNextFloor()
        {
            var run = new DungeonRun(RunKind.Dungeon, Roster.DefaultSquad, 9);
            run.NextFloor();
            Assert.IsTrue(run.Battle.LootEnabled);
            var kael = run.Battle.Units.First(u => u.Name == "Kael");
            kael.Hp = kael.MaxHp - 10;

            var sword = Item(GearSlot.MainHand, new StatRoll(StatType.Damage, 4));
            var plate = Item(GearSlot.Chest, new StatRoll(StatType.MaxHp, 8));
            run.Battle.PickedUp.Add(sword);
            run.Battle.PickedUp.Add(plate);
            run.CollectLoot();
            run.CollectLoot();
            CollectionAssert.AreEqual(new[] { sword, plate }, run.Stash);

            run.Equip("kael", sword);
            run.Equip("kael", plate);
            Assert.IsEmpty(run.Stash);
            run.NextFloor();

            var next = run.Battle.Units.First(u => u.Name == "Kael");
            Assert.AreEqual(12, next.Damage);
            Assert.AreEqual(38, next.MaxHp);
            Assert.AreEqual(28, next.Hp, "Damage taken carries over; the new Health is added on top");
            Assert.AreEqual(2, run.Battle.Layout.Floor);

            run.Unequip("kael", GearSlot.MainHand);
            CollectionAssert.AreEqual(new[] { sword }, run.Stash);
            Assert.IsNull(run.LoadoutFor("kael")[GearSlot.MainHand]);
        }

        [Test]
        public void FinalVictory_GathersTheLootLeftOnTheFloor()
        {
            var layout = new RoomLayout { Map = GridMap.FromRows("#####", "#...#", "#####"), Theme = Theme.AshenCrypt };
            var b = new Battle(layout, new string[0], 2) { Roll = () => 1, LootEnabled = true };
            b.AddUnit(Roster.Hero("kael"), new Point(1, 1));
            var boss = b.AddUnit(Roster.Enemy(EnemyKind.RiftLord), new Point(2, 1));
            b.Start();
            boss.Hp = 1;
            b.TryAttack(boss);
            Assert.AreEqual(BattleOutcome.Victory, b.Outcome);
            Assert.AreEqual(0, b.GroundLootCount);
            Assert.AreEqual(2, b.PickedUp.Count);
        }
    }
}
