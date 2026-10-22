using System.Linq;
using NUnit.Framework;
using ShardsOfEternity.Core;

namespace ShardsOfEternity.Tests
{
    public class RunAndTurnRulesTests
    {
        static Battle Arena(bool finalFloor, params string[] rows)
        {
            var layout = new RoomLayout { Map = GridMap.FromRows(rows), Theme = Theme.AshenCrypt, IsFinalFloor = finalFloor };
            return new Battle(layout, new string[0], 1) { Roll = () => 1 };
        }

        [Test]
        public void ClearingAnEarlyFloor_OpensTheExit_WithoutEndingTheRun()
        {
            var b = Arena(false, "######", "#....E", "######");
            var kael = b.AddUnit(Roster.Hero("kael"), new Point(1, 1));
            var ghoul = b.AddUnit(Roster.Enemy(EnemyKind.Ghoul), new Point(2, 1));
            b.Start();
            ghoul.Hp = 1;
            Assert.IsTrue(b.TryAttack(ghoul));
            Assert.IsTrue(b.FloorCleared);
            Assert.IsTrue(b.ExitOpen);
            Assert.IsFalse(b.IsOver, "Only the boss floor ends the run");

            b.EndTurn();
            Assert.AreSame(kael, b.Current);
            Assert.IsTrue(b.TryMove(new Point(5, 1)), "The open exit is a walkable tile");
            Assert.AreEqual(BattleOutcome.FloorComplete, b.Outcome);
        }

        [Test]
        public void Reinforcements_ComeFromTheTimer_NotRiftPressure()
        {
            var b = new Battle(RoomLayout.Prototype(), Roster.DefaultSquad, 3);
            b.Start();
            int before = b.Units.Count;
            b.CallReinforcements();
            Assert.AreEqual(before, b.Units.Count, "No reinforcements before the room wakes");

            // Wake the room, then let lots of timeline pass: pressure rises but nobody spawns.
            var nyra = b.Current;
            var ghoul = b.Units.First(u => u.IsEnemy);
            nyra.X = ghoul.X + 1;
            nyra.Y = ghoul.Y;
            Assert.IsTrue(b.TryAttack(ghoul));
            Assert.IsTrue(b.InCombat);
            foreach (var u in b.Units.Where(u => u.IsEnemy)) u.Alive = u == ghoul;
            ghoul.Hp = 999;
            for (int i = 0; i < 60; i++)
            {
                if (b.Current.IsEnemy) b.Current.NextTurnAt += 1;
                b.EndTurn();
            }
            Assert.Greater(b.Pressure, 4);
            Assert.AreEqual(before, b.Units.Count);

            b.CallReinforcements();
            var spawned = b.Units.Skip(before).ToList();
            CollectionAssert.AreEqual(Theme.AshenCrypt.FirstWave, spawned.Select(u => u.Def.EnemyKind));
            Assert.IsTrue(spawned.All(u => u.Awake));
            Assert.AreEqual(1, b.WavesCalled);
        }

        [Test]
        public void ActionShards_AllowMovingTwice()
        {
            var b = Arena(true, "##########", "#........#", "##########");
            b.Rules = new TurnRules { UseActionShards = true };
            var kael = b.AddUnit(Roster.Hero("kael"), new Point(1, 1));
            b.AddUnit(Roster.Enemy(EnemyKind.Ghoul), new Point(8, 1)).NextTurnAt = 1e9;
            b.Start();
            Assert.AreEqual(2, kael.Shards);
            Assert.IsTrue(b.TryMove(new Point(2, 1)));
            Assert.IsTrue(b.TryMove(new Point(3, 1)), "Second shard buys a second move");
            Assert.AreEqual(0, kael.Shards);
            Assert.IsTrue(b.CurrentTurnSpent);
            Assert.IsFalse(b.TryMove(new Point(4, 1)));
        }

        [Test]
        public void ActionShards_AllowAttackingTwice()
        {
            var b = Arena(true, "#####", "#...#", "#####");
            b.Rules = new TurnRules { UseActionShards = true };
            var kael = b.AddUnit(Roster.Hero("kael"), new Point(1, 1));
            var knight = b.AddUnit(Roster.Enemy(EnemyKind.Hellknight), new Point(2, 1));
            b.Start();
            Assert.IsTrue(b.TryAttack(knight));
            Assert.IsTrue(b.TryAttack(knight));
            Assert.AreEqual(knight.MaxHp - 2 * b.GetDamageInfo(kael, knight).Damage, knight.Hp);
            Assert.IsFalse(b.TryAttack(knight));
        }

        [Test]
        public void ClassicRules_StillOneMoveAndOneAction()
        {
            var b = Arena(true, "##########", "#........#", "##########");
            b.AddUnit(Roster.Hero("kael"), new Point(1, 1));
            b.AddUnit(Roster.Enemy(EnemyKind.Ghoul), new Point(8, 1)).NextTurnAt = 1e9;
            b.Start();
            Assert.IsTrue(b.TryMove(new Point(2, 1)));
            Assert.IsFalse(b.TryMove(new Point(3, 1)));
        }

        [Test]
        public void ActionShards_BankUpToTheCap()
        {
            var b = Arena(true, "##########", "#........#", "##########");
            b.Rules = new TurnRules { UseActionShards = true, ShardsPerTurn = 2, ShardCap = 4 };
            var kael = b.AddUnit(Roster.Hero("kael"), new Point(1, 1));
            b.Start();
            b.EndTurn();
            Assert.AreEqual(4, kael.Shards);
            b.EndTurn();
            Assert.AreEqual(4, kael.Shards, "Capped");
        }

        [Test]
        public void Runs_HaveTheRightFloorCount_AndEndWithTheBoss()
        {
            for (int seed = 1; seed < 20; seed++)
            {
                var dungeon = new DungeonRun(RunKind.Dungeon, Roster.DefaultSquad, seed);
                Assert.That(dungeon.FloorCount, Is.InRange(3, 4));
                Assert.AreEqual(12, new DungeonRun(RunKind.Raid, Roster.DefaultSquad, seed).FloorCount);

                while (dungeon.NextFloor())
                {
                    bool boss = dungeon.Battle.Layout.HasBoss;
                    Assert.AreEqual(dungeon.IsFinalFloor, boss, $"Seed {seed} floor {dungeon.Floor}");
                    Assert.AreEqual(dungeon.IsFinalFloor, dungeon.Battle.Layout.IsFinalFloor);
                }
                Assert.AreEqual(dungeon.FloorCount, dungeon.Floor);
                Assert.AreEqual(1, dungeon.Battle.Units.Count(u => u.Def.IsBoss));
            }
        }

        [Test]
        public void Runs_CarryHeroHealthAndDeathsBetweenFloors()
        {
            var run = new DungeonRun(RunKind.Dungeon, Roster.DefaultSquad, 5);
            run.NextFloor();
            var kael = run.Battle.Units.First(u => u.Name == "Kael");
            var nyra = run.Battle.Units.First(u => u.Name == "Nyra");
            kael.Hp = 7;
            kael.Ultimate = 60;
            nyra.Alive = false;
            nyra.Hp = 0;
            run.NextFloor();
            var next = run.Battle.Units;
            Assert.AreEqual(7, next.First(u => u.Name == "Kael").Hp);
            Assert.AreEqual(60, next.First(u => u.Name == "Kael").Ultimate);
            Assert.IsFalse(next.First(u => u.Name == "Nyra").Alive);
            Assert.AreEqual(2, run.Floor);
        }

        [Test]
        public void Boss_CleavesEveryAdjacentHero()
        {
            var b = Arena(true, "#####", "#...#", "#...#", "#####");
            var bram = b.AddUnit(Roster.Hero("bram"), new Point(1, 1));
            var kael = b.AddUnit(Roster.Hero("kael"), new Point(1, 2));
            bram.NextTurnAt = kael.NextTurnAt = 1000;
            var boss = b.AddUnit(Roster.Enemy(EnemyKind.RiftLord), new Point(2, 1));
            boss.Awake = true;
            boss.NextTurnAt = 1;
            b.Start();
            b.RunEnemyTurn();
            Assert.Less(bram.Hp, bram.MaxHp);
            Assert.Less(kael.Hp, kael.MaxHp);
        }

        [Test]
        public void SimulatedRuns_WithActionShards_Finish()
        {
            for (int seed = 0; seed < 15; seed++)
            {
                var run = new DungeonRun(RunKind.Dungeon, new[] { "kael", "bram", "wren", "tova" }, seed)
                {
                    Rules = new TurnRules { UseActionShards = true, ShardCap = 3 },
                };
                run.NextFloor();
                for (int turn = 0; turn < 5000; turn++)
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
                    else PlayHero(b, u);
                    b.DrainEvents();
                    if (!b.IsOver) b.EndTurn();
                }
                Assert.IsTrue(run.Battle.IsOver && run.Battle.Outcome != BattleOutcome.FloorComplete, $"Seed {seed} did not finish");
            }
        }

        /// <summary>Naive policy: attack anything in reach, else step toward the nearest enemy, or the exit once clear.</summary>
        static void PlayHero(Battle b, Unit u)
        {
            for (int i = 0; i < 4 && !b.IsOver && !b.CurrentTurnSpent; i++)
            {
                var target = b.AttackTargets(u).FirstOrDefault();
                if (target != null)
                {
                    b.TryAttack(target);
                    continue;
                }
                Point? goal = b.FloorCleared
                    ? b.Map.Find(TileType.Exit)
                    : b.Enemies.OrderBy(e => Point.Distance(e.Position, u.Position)).Select(e => (Point?)e.Position).FirstOrDefault();
                if (!goal.HasValue || !b.CanMove(u)) return;
                var move = b.MoveOptions(u).OrderBy(m => Point.Distance(m.Target, goal.Value)).FirstOrDefault();
                if (move == null || Point.Distance(move.Target, goal.Value) >= Point.Distance(u.Position, goal.Value)) return;
                b.TryMove(move.Target);
            }
        }
    }
}
