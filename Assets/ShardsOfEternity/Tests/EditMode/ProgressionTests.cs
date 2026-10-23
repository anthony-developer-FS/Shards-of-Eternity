using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShardsOfEternity.Core;

namespace ShardsOfEternity.Tests
{
    public class ProgressionTests
    {
        static readonly string[] Squad = { "kael", "bram", "wren", "tova" };

        static GearItem Item(GearSlot slot, StatType stat, double value, string name = null) => new GearItem
        {
            Name = name ?? "Test " + slot,
            Slot = slot,
            Rarity = Rarity.Rare,
            ItemLevel = 2,
            Stats = { new StatRoll(stat, value) },
        };

        /// <summary>Pretends the squad picked an item up on the current floor.</summary>
        static GearItem Find(DungeonRun run, GearItem item)
        {
            run.Battle.PickedUp.Add(item);
            run.CollectLoot();
            return item;
        }

        [Test]
        public void Xp_LevelsUpAlongTheCurve_AndStopsAtTheCap()
        {
            var h = new HeroProgress("kael");
            Assert.AreEqual(2, h.AddXp(350), "100 for level 2, then 200 for level 3");
            Assert.AreEqual(3, h.Level);
            Assert.AreEqual(50, h.Xp);
            Assert.AreEqual(300, h.XpToNext);

            h.AddXp(1000000);
            Assert.AreEqual(Progression.MaxLevel, h.Level);
            Assert.AreEqual(0, h.Xp);
            Assert.AreEqual(0, h.AddXp(500));
        }

        [Test]
        public void Levels_AddHealthDamageAndArmor_BeforeGear()
        {
            var profile = new Profile();
            profile.Hero("kael").Level = 7;
            profile.LoadoutFor("kael").Equip(Item(GearSlot.Chest, StatType.MaxHp, 5));
            var run = profile.StartRun(RunKind.Dungeon, Squad, 4);
            run.NextFloor();

            var kael = run.Battle.Units.First(u => u.Def.Id == "kael");
            var def = Roster.Hero("kael");
            Assert.AreEqual(7, kael.Level);
            Assert.AreEqual(def.Hp + 18 + 5, kael.MaxHp);
            Assert.AreEqual(kael.MaxHp, kael.Hp);
            Assert.AreEqual(def.Damage + 2, kael.Damage);
            Assert.AreEqual(def.Armor + 1, kael.Armor);

            var bram = run.Battle.Units.First(u => u.Def.Id == "bram");
            Assert.AreEqual(1, bram.Level);
            Assert.AreEqual(Roster.Hero("bram").Hp, bram.MaxHp);
        }

        [Test]
        public void Kills_FloorClears_AndReinforcements_PayXp()
        {
            var layout = new RoomLayout { Map = GridMap.FromRows("######", "#....E", "######"), Theme = Theme.AshenCrypt, Floor = 1, IsFinalFloor = false };
            var b = new Battle(layout, new string[0], 1) { Roll = () => 1 };
            b.AddUnit(Roster.Hero("kael"), new Point(1, 1));
            var ghoul = b.AddUnit(Roster.Enemy(EnemyKind.Ghoul), new Point(2, 1));
            b.Start();
            ghoul.Hp = 1;
            Assert.IsTrue(b.TryAttack(ghoul));
            CollectionAssert.AreEqual(new[] { ghoul }, b.Slain);
            Assert.AreEqual(8 + Progression.FloorClearXp(1), Progression.XpFor(b, RunKind.Dungeon));

            var extra = new Unit(9, Roster.Enemy(EnemyKind.Hellknight), default) { IsReinforcement = true };
            Assert.AreEqual(9, Progression.KillXp(extra, 1), "Reinforcements pay half");
            extra.SummonedBy = ghoul;
            Assert.AreEqual(2, Progression.KillXp(extra, 1));
            Assert.Greater(Progression.KillXp(ghoul, 10), Progression.KillXp(ghoul, 1), "Deeper floors pay more");
        }

        [Test]
        public void RunGear_IsACopy_UntilTheRunIsSettled()
        {
            var profile = new Profile();
            var helm = Item(GearSlot.Head, StatType.Armor, 1, "Old helm");
            profile.Vault.Add(helm);
            profile.Equip("kael", helm);
            Assert.IsEmpty(profile.Vault);

            var run = profile.StartRun(RunKind.Dungeon, Squad, 2);
            run.NextFloor();
            run.Unequip("kael", GearSlot.Head);
            Assert.AreSame(helm, profile.LoadoutFor("kael")[GearSlot.Head], "The profile is untouched mid-run");
            Assert.Contains(helm, run.Stash);
        }

        [Test]
        public void Defeat_KeepsEquippedFinds_AndBroughtGear_ButLosesTheStash()
        {
            var profile = new Profile();
            var oldHelm = Item(GearSlot.Head, StatType.Armor, 1, "Old helm");
            profile.Vault.Add(oldHelm);
            profile.Equip("kael", oldHelm);

            var run = profile.StartRun(RunKind.Dungeon, Squad, 2);
            run.NextFloor();
            var newHelm = Find(run, Item(GearSlot.Head, StatType.Armor, 3, "New helm"));
            var spare = Find(run, Item(GearSlot.Feet, StatType.Speed, 2, "Spare boots"));
            run.Equip("kael", newHelm);
            Assert.Contains(oldHelm, run.Stash, "The old helm went to the stash when the new one went on");

            var result = profile.Settle(run);
            Assert.IsFalse(result.Won);
            Assert.AreSame(newHelm, profile.LoadoutFor("kael")[GearSlot.Head]);
            CollectionAssert.AreEquivalent(new[] { oldHelm }, profile.Vault, "Gear the hero brought always comes back");
            CollectionAssert.AreEquivalent(new[] { newHelm }, result.Kept);
            CollectionAssert.AreEquivalent(new[] { spare }, result.Lost);
            Assert.AreEqual(1, profile.RunsPlayed);
            Assert.AreEqual(0, profile.RunsWon);
            Assert.AreEqual(1, profile.DeepestFloor);
            Assert.IsNull(profile.Settle(run), "A run pays out once");
            Assert.AreEqual(1, profile.RunsPlayed);
        }

        [Test]
        public void KeepStashOnDefeat_SendsEveryFindToTheVault()
        {
            var profile = new Profile { KeepStashOnDefeat = true };
            var run = profile.StartRun(RunKind.Dungeon, Squad, 2);
            run.NextFloor();
            var spare = Find(run, Item(GearSlot.Feet, StatType.Speed, 2));
            var result = profile.Settle(run);
            CollectionAssert.AreEquivalent(new[] { spare }, profile.Vault);
            CollectionAssert.IsEmpty(result.Lost);
        }

        [Test]
        public void SimulatedRuns_PayXpToTheWholeSquad_AndAccountForEveryFind()
        {
            bool sawWin = false;
            for (int seed = 0; seed < 12; seed++)
            {
                // Half the runs use a veteran squad, so the simulation also wins some.
                var profile = new Profile();
                if (seed % 2 == 1) foreach (var id in Squad) profile.Hero(id).Level = Progression.MaxLevel;
                var run = profile.StartRun(RunKind.Dungeon, Squad, seed, rules: new TurnRules { UseActionShards = true, ShardCap = 3 });
                run.NextFloor();
                for (int turn = 0; turn < 5000; turn++)
                {
                    var b = run.Battle;
                    if (b.Outcome == BattleOutcome.FloorComplete)
                    {
                        run.NextFloor();
                        continue;
                    }
                    if (b.IsOver) break;
                    var u = b.Current;
                    if (u.IsEnemy) b.RunEnemyTurn();
                    else RunAndTurnRulesTests.PlayHero(b, u);
                    b.DrainEvents();
                    if (!b.IsOver) b.EndTurn();
                }
                int xp = run.Xp;
                var result = profile.Settle(run);
                sawWin |= result.Won;
                Assert.AreEqual(xp, result.Xp);
                Assert.Greater(result.Xp, 0, $"Seed {seed}");
                Assert.AreEqual(4, result.Heroes.Count);
                foreach (var h in result.Heroes)
                    Assert.AreEqual(profile.Hero(h.Id).Level, h.LevelAfter);
                Assert.IsTrue(result.Heroes.All(h => h.LevelAfter == result.Heroes[0].LevelAfter), "The squad shares the XP");
                CollectionAssert.AreEquivalent(run.Found, result.Kept.Concat(result.Lost));
                if (result.Won)
                {
                    CollectionAssert.IsEmpty(result.Lost);
                    Assert.Greater(result.Xp, Progression.VictoryXp(RunKind.Dungeon));
                    Assert.AreEqual(1, profile.RunsWon);
                }
                Assert.AreEqual(run.Kills, profile.EnemiesSlain);
            }
            Assert.IsTrue(sawWin, "At least one simulated run should win");
        }

        [Test]
        public void Profile_RoundTripsThroughJson()
        {
            var p = new Profile { RunsPlayed = 5, RunsWon = 2, DeepestFloor = 9, EnemiesSlain = 140 };
            p.Hero("kael").AddXp(450);
            p.Hero("sera").Level = 4;
            var ringA = Item(GearSlot.Ring1, StatType.CritChance, 0.04, "Band \"of\" quotes\\ and\nlines");
            var ringB = Item(GearSlot.Ring1, StatType.CritDamage, 0.15);
            p.LoadoutFor("kael").Equip(ringA);
            p.LoadoutFor("kael").Equip(ringB);
            p.LoadoutFor("kael").Equip(Item(GearSlot.MainHand, StatType.Damage, 4));
            p.Vault.Add(Item(GearSlot.Feet, StatType.Move, 1));
            p.Vault[0].Stats.Add(new StatRoll(StatType.Speed, 2));
            p.Vault[0].Rarity = Rarity.Legendary;

            string json = p.ToJson();
            var back = Profile.FromJson(json);
            Assert.AreEqual(json, back.ToJson());
            Assert.AreEqual(3, back.Hero("kael").Level);
            Assert.AreEqual(150, back.Hero("kael").Xp);
            Assert.AreEqual(4, back.Hero("sera").Level);
            Assert.AreEqual(ringA.Name, back.LoadoutFor("kael")[GearSlot.Ring1].Name);
            Assert.AreEqual(0.15, back.LoadoutFor("kael")[GearSlot.Ring2].Total(StatType.CritDamage), 1e-9);
            Assert.AreEqual(Rarity.Legendary, back.Vault[0].Rarity);
            Assert.AreEqual(2, back.Vault[0].Stats.Count);
            Assert.AreEqual(140, back.EnemiesSlain);
        }

        [Test]
        public void DamagedSaves_ThrowFormatException()
        {
            foreach (var bad in new[]
            {
                "", "{", "[]", "{\"version\": 99}", "{\"version\": 1, \"vault\": [{\"name\": \"x\", \"slot\": \"Nose\", \"rarity\": \"Rare\"}]}",
                "{\"version\": 1, \"heroes\": {\"kael\": 3}}", "{\"version\": 1} trailing",
            })
                Assert.Throws<FormatException>(() => Profile.FromJson(bad), bad);
            Assert.AreEqual(0, Profile.FromJson("{\"version\": 1}").RunsPlayed, "A minimal save is a fresh profile");
        }

        [Test]
        public void Json_ReadsWhatItWrites()
        {
            var value = new Dictionary<string, object>
            {
                ["s"] = "tab\t quote\" slash\\ unicode é \u0001",
                ["n"] = -12.5e3,
                ["list"] = new List<object> { true, false, null, 3 },
                ["empty"] = new Dictionary<string, object>(),
            };
            var back = (Dictionary<string, object>)Json.Parse(Json.Write(value));
            Assert.AreEqual(value["s"], back["s"]);
            Assert.AreEqual(-12500.0, back["n"]);
            CollectionAssert.AreEqual(new object[] { true, false, null, 3.0 }, (List<object>)back["list"]);
            Assert.IsEmpty((Dictionary<string, object>)back["empty"]);
        }

        [Test]
        public void Vault_DropsTheWeakestItems_WhenFull()
        {
            var profile = new Profile();
            for (int i = 0; i < Profile.VaultCap; i++) profile.Vault.Add(Item(GearSlot.Head, StatType.Armor, 1));
            var legend = Item(GearSlot.Head, StatType.Armor, 9);
            legend.Rarity = Rarity.Legendary;
            var run = profile.StartRun(RunKind.Dungeon, Squad, 1);
            run.NextFloor();
            profile.KeepStashOnDefeat = true;
            Find(run, legend);
            profile.Settle(run);
            Assert.AreEqual(Profile.VaultCap, profile.Vault.Count);
            Assert.Contains(legend, profile.Vault);
        }
    }
}
