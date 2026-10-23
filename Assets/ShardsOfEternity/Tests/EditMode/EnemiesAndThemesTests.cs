using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShardsOfEternity.Core;

namespace ShardsOfEternity.Tests
{
    public class EnemiesAndThemesTests
    {
        /// <summary>A custom room with Kael and one awake enemy of <paramref name="kind"/>, who takes the first turn. Crits never happen.</summary>
        static Battle Arena(string[] rows, out Unit kael, EnemyKind kind, Point at, int floor = 1)
        {
            var layout = new RoomLayout { Map = GridMap.FromRows(rows), Theme = Theme.AshenCrypt, Floor = floor };
            var b = new Battle(layout, new string[0], 1) { Roll = () => 1 };
            kael = b.AddUnit(Roster.Hero("kael"), new Point(1, 1));
            kael.NextTurnAt = 5000;
            var first = b.AddUnit(Roster.Enemy(kind), at);
            first.Awake = true;
            first.NextTurnAt = 1;
            return b;
        }

        static readonly string[] Corridor = { "##########", "#........#", "##########" };

        [Test]
        public void Cultist_HealsABadlyWoundedDemon_InsteadOfAttacking()
        {
            var b = Arena(Corridor, out var kael, EnemyKind.Cultist, new Point(3, 1));
            var knight = b.AddUnit(Roster.Enemy(EnemyKind.Hellknight), new Point(5, 1));
            knight.Awake = true;
            knight.NextTurnAt = 4000;
            knight.Hp = 10;
            b.Start();
            Assert.AreEqual(EnemyKind.Cultist, b.Current.Def.EnemyKind);
            b.RunEnemyTurn();
            Assert.AreEqual(10 + Battle.CultistHeal, knight.Hp);
            Assert.AreEqual(kael.MaxHp, kael.Hp, "Healing replaces the attack");
        }

        [Test]
        public void Cultist_Attacks_WhenNoDemonIsBadlyHurt()
        {
            var b = Arena(Corridor, out var kael, EnemyKind.Cultist, new Point(3, 1));
            b.Start();
            b.RunEnemyTurn();
            Assert.Less(kael.Hp, kael.MaxHp);
        }

        [Test]
        public void Hellhound_PouncesAfterALongDash()
        {
            var b = Arena(Corridor, out var kael, EnemyKind.Hellhound, new Point(8, 1));
            b.Start();
            var hound = b.Current;
            b.RunEnemyTurn();
            Assert.AreEqual(new Point(2, 1), hound.Position, "Dashes 6 tiles");
            int expected = b.GetDamageInfo(hound, kael, hound.Damage + Battle.PounceBonus).Damage;
            Assert.AreEqual(kael.MaxHp - expected, kael.Hp);
        }

        [Test]
        public void Hellhound_BitesNormally_WhenAlreadyAdjacent()
        {
            var b = Arena(Corridor, out var kael, EnemyKind.Hellhound, new Point(2, 1));
            b.Start();
            var hound = b.Current;
            b.RunEnemyTurn();
            Assert.AreEqual(kael.MaxHp - b.GetDamageInfo(hound, kael).Damage, kael.Hp);
        }

        [Test]
        public void RiftCaller_SummonsAnImpEveryOtherTurn_UpToTwo()
        {
            var b = Arena(new[] { "############", "#..........#", "#..........#", "############" }, out var kael,
                EnemyKind.RiftCaller, new Point(9, 1));
            kael.NextTurnAt = 1e9;
            b.Start();
            var caller = b.Current;
            var counts = new List<int>();
            for (int i = 0; i < 60 && counts.Count < 6; i++)
            {
                var u = b.Current;
                if (u == caller)
                {
                    b.RunEnemyTurn();
                    counts.Add(b.Units.Count(s => s.Alive && s.SummonedBy == caller));
                }
                b.EndTurn();
            }
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 2, 2 }, counts);
            Assert.IsTrue(b.Units.Where(s => s.SummonedBy == caller).All(s => s.Def.EnemyKind == EnemyKind.Imp && s.IsReinforcement && s.Awake));
        }

        [Test]
        public void DeeperFloors_MakeEnemiesTougher()
        {
            var b1 = Arena(Corridor, out _, EnemyKind.Ghoul, new Point(5, 1));
            var ghoul1 = b1.Units.First(u => u.IsEnemy);
            Assert.AreEqual(16, ghoul1.MaxHp);
            Assert.AreEqual(6, ghoul1.Damage);

            var b4 = Arena(Corridor, out _, EnemyKind.Ghoul, new Point(5, 1), floor: 4);
            var ghoul4 = b4.Units.First(u => u.IsEnemy);
            Assert.AreEqual(20, ghoul4.MaxHp);
            Assert.AreEqual(20, ghoul4.Hp);
            Assert.AreEqual(7, ghoul4.Damage);

            var hero = b4.Units.First(u => u.IsHero);
            Assert.AreEqual(Roster.Hero("kael").Hp, hero.MaxHp, "Heroes are not scaled");
        }

        [Test]
        public void Raids_ChangeThemeEveryThreeFloors_DungeonsKeepOne()
        {
            var raid = new DungeonRun(RunKind.Raid, Roster.DefaultSquad, 5);
            var themes = Enumerable.Range(1, DungeonRun.RaidFloors).Select(raid.ThemeFor).ToList();
            for (int f = 0; f < themes.Count; f++)
                Assert.AreSame(themes[f / 3 * 3], themes[f], $"Floor {f + 1} keeps its block's theme");
            CollectionAssert.AreEquivalent(Theme.All, themes.Distinct(), "A 12 floor raid visits all 4 themes");
            for (int i = 0; i < 4; i++)
            {
                raid.NextFloor();
                Assert.AreSame(raid.ThemeFor(raid.Floor), raid.Battle.Layout.Theme);
            }

            var dungeon = new DungeonRun(RunKind.Dungeon, Roster.DefaultSquad, 5);
            Assert.IsTrue(Enumerable.Range(1, dungeon.FloorCount).All(f => dungeon.ThemeFor(f) == dungeon.Theme));
        }

        [Test]
        public void EveryTheme_GeneratesPlayableRooms_WithinItsCaps()
        {
            foreach (var theme in Theme.All)
                for (int seed = 0; seed < 60; seed++)
                {
                    var room = RoomGenerator.Generate(seed, theme, floor: 1 + seed % 12);
                    var map = room.Map;
                    Assert.IsTrue(RoomGenerator.AllOpenTilesConnected(map.Width, map.Height,
                        (x, y) => map.IsBorder(x, y) || map[x, y] == TileType.Wall || map[x, y] == TileType.Pillar, map.Find(TileType.Exit)));
                    Assert.LessOrEqual(room.Enemies.Count(e => e.Kind == EnemyKind.Hellknight), theme.MaxHellknights);
                    Assert.LessOrEqual(room.Enemies.Count(e => e.Kind == EnemyKind.RiftCaller), theme.MaxRiftCallers);
                    Assert.IsTrue(room.Enemies.All(e => theme.Pool.Contains(e.Kind) || e.Kind == EnemyKind.Ghoul));
                }
        }

        [Test]
        public void DeepRaidFloors_HoldMoreEnemies()
        {
            double Average(int floor) => Enumerable.Range(0, 100).Average(s => RoomGenerator.Generate(s, Theme.AshenCrypt, floor: floor).Enemies.Count);
            Assert.Greater(Average(9), Average(1) + 1.5);
        }

        [Test]
        public void SimulatedRuns_InEveryTheme_Finish()
        {
            foreach (var theme in Theme.All)
                for (int seed = 0; seed < 6; seed++)
                {
                    var run = new DungeonRun(RunKind.Dungeon, new[] { "kael", "bram", "wren", "tova" }, seed, theme);
                    run.NextFloor();
                    for (int turn = 0; turn < 6000; turn++)
                    {
                        var b = run.Battle;
                        if (b.Outcome == BattleOutcome.FloorComplete)
                        {
                            Assert.IsTrue(run.NextFloor());
                            continue;
                        }
                        if (b.IsOver) break;
                        if (turn % 40 == 39) b.CallReinforcements();
                        var u = b.Current;
                        if (u.IsEnemy) b.RunEnemyTurn();
                        else RunAndTurnRulesTests.PlayHero(b, u);
                        b.DrainEvents();
                        if (!b.IsOver) b.EndTurn();
                    }
                    Assert.IsTrue(run.Battle.IsOver && run.Battle.Outcome != BattleOutcome.FloorComplete,
                        $"{theme.Name} seed {seed} did not finish on floor {run.Floor}");
                    Assert.IsTrue(run.Battle.Units.Where(x => x.Alive).All(x => run.Battle.UnitAt(x.Position) == x), "No two living units share a tile");
                }
        }
    }
}
