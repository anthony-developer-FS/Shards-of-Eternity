using System.Linq;
using NUnit.Framework;
using ShardsOfEternity.Core;

namespace ShardsOfEternity.Tests
{
    public class PriestTests
    {
        const int HolyLight = 0, Resurrection = 1, Sanctuary = 2, MassResurrection = 3;

        /// <summary>Sera acts first; a sleepless ghoul with lots of HP keeps the battle going.</summary>
        static (Battle b, Unit sera) Arena(params string[] rows)
        {
            var layout = new RoomLayout { Map = GridMap.FromRows(rows), Theme = Theme.AshenCrypt };
            var b = new Battle(layout, new string[0], 1) { Roll = () => 1 };
            var sera = b.AddUnit(Roster.Hero("sera"), new Point(1, 1));
            var ghoul = b.AddUnit(Roster.Enemy(EnemyKind.Ghoul), new Point(rows[0].Length - 2, 1));
            ghoul.Awake = true;
            ghoul.Hp = 999;
            ghoul.NextTurnAt = 5000;
            return (b, sera);
        }

        static Unit Fallen(Battle b, string id, int x, int y)
        {
            var h = b.AddUnit(Roster.Hero(id), new Point(x, y));
            h.Alive = false;
            h.Hp = 0;
            h.NextTurnAt = 5000;
            return h;
        }

        [Test]
        public void Resurrection_BringsAFallenAllyBackAtHalfHp()
        {
            var (b, sera) = Arena("#########", "#.......#", "#########");
            var kael = Fallen(b, "kael", 3, 1);
            b.Start();
            Assert.AreSame(sera, b.Current);
            CollectionAssert.AreEqual(new[] { new Point(3, 1) }, b.AbilityTargets(sera, Resurrection));

            Assert.IsTrue(b.TryUseAbility(Resurrection, new Point(3, 1)));
            Assert.IsTrue(kael.Alive);
            Assert.AreEqual(kael.MaxHp / 2, kael.Hp);
            Assert.AreEqual(new Point(3, 1), kael.Position);
            Assert.AreEqual(b.Clock + kael.TurnInterval, kael.NextTurnAt, 1e-9, "The revived hero takes a turn soon after");
            Assert.AreEqual(6, sera.Cooldowns[Resurrection]);
            Assert.IsTrue(b.DrainEvents().Any(e => e.Type == BattleEventType.Revived && e.Unit == kael));
            Assert.IsEmpty(b.FallenHeroes);
        }

        [Test]
        public void Resurrection_NeedsTheBodyInRange_AndMovesTheHeroOffAnOccupiedBody()
        {
            var (b, sera) = Arena("##########", "#........#", "##########");
            var far = Fallen(b, "nyra", 6, 1);
            var kael = Fallen(b, "kael", 3, 1);
            var bram = b.AddUnit(Roster.Hero("bram"), new Point(3, 1));
            bram.NextTurnAt = 5000;
            b.Start();
            CollectionAssert.DoesNotContain(b.AbilityTargets(sera, Resurrection), far.Position, "Nyra is 5 tiles away");

            Assert.IsTrue(b.TryUseAbility(Resurrection, new Point(3, 1)));
            Assert.IsTrue(kael.Alive);
            Assert.AreNotEqual(bram.Position, kael.Position);
            Assert.AreEqual(1, Point.Distance(kael.Position, new Point(3, 1)));
            Assert.IsFalse(far.Alive);
        }

        [Test]
        public void HolyLight_CuresPoison_EvenAtFullHealth()
        {
            var (b, sera) = Arena("#######", "#.....#", "#######");
            var bram = b.AddUnit(Roster.Hero("bram"), new Point(3, 1));
            bram.NextTurnAt = 5000;
            bram.Poison = 2;
            b.Start();
            Assert.IsTrue(b.TryUseAbility(HolyLight, bram.Position));
            Assert.AreEqual(0, bram.Poison);
            Assert.AreEqual(bram.MaxHp, bram.Hp);
        }

        [Test]
        public void Sanctuary_ShieldsNearbyAllies()
        {
            var (b, sera) = Arena("########", "#......#", "########");
            var near = b.AddUnit(Roster.Hero("bram"), new Point(3, 1));
            var far = b.AddUnit(Roster.Hero("wren"), new Point(5, 1));
            near.NextTurnAt = far.NextTurnAt = 5000;
            b.Start();
            Assert.IsTrue(b.TryUseAbility(Sanctuary, sera.Position));
            Assert.AreEqual(2, sera.ArmorBuff);
            Assert.AreEqual(2, near.ArmorBuff);
            Assert.AreEqual(0, far.ArmorBuff);
        }

        [Test]
        public void MassResurrection_RevivesEveryoneNextToSera_AndHealsTheRest()
        {
            var (b, sera) = Arena("############", "#..........#", "#..........#", "############");
            var kael = Fallen(b, "kael", 8, 2);
            var bram = Fallen(b, "bram", 9, 2);
            var nyra = b.AddUnit(Roster.Hero("nyra"), new Point(5, 2));
            nyra.NextTurnAt = 5000;
            nyra.Hp = 5;
            sera.Ultimate = 100;
            b.Start();
            Assert.IsTrue(b.TryUseAbility(MassResurrection, sera.Position));

            Assert.IsTrue(kael.Alive && bram.Alive);
            Assert.AreEqual(kael.MaxHp / 3, kael.Hp);
            Assert.AreEqual(bram.MaxHp / 3, bram.Hp);
            Assert.AreEqual(1, Point.Distance(kael.Position, sera.Position));
            Assert.AreEqual(1, Point.Distance(bram.Position, sera.Position));
            Assert.AreNotEqual(kael.Position, bram.Position);
            Assert.AreEqual(15, nyra.Hp, "Heroes already standing heal for 10");
            Assert.AreEqual(bram.MaxHp / 3, bram.Hp, "Revived heroes are not healed on top");
            Assert.AreEqual(0, sera.Ultimate);
        }

        [Test]
        public void FallenHeroes_CarryOverToTheNextFloor_AndCanBeRevivedThere()
        {
            var run = new DungeonRun(RunKind.Dungeon, new[] { "sera", "kael", "bram", "nyra" }, 4);
            run.NextFloor();
            var kael = run.Battle.Units.First(u => u.Name == "Kael");
            kael.Alive = false;
            kael.Hp = 0;
            run.NextFloor();
            var next = run.Battle.Units.First(u => u.Name == "Kael");
            Assert.IsFalse(next.Alive);
            CollectionAssert.Contains(run.Battle.FallenHeroes.ToList(), next);
            Assert.IsTrue(run.Battle.FreeTileNear(next.Position).HasValue);
        }

        [Test]
        public void SimulatedBattles_WithAPriest_AlwaysFinish()
        {
            for (int seed = 0; seed < 20; seed++)
            {
                var room = RoomGenerator.Generate(seed);
                var b = new Battle(room, new[] { "kael", "sera", "nyra", "wren" }, seed);
                b.Start();
                int revives = 0;
                for (int turn = 0; turn < 2000 && !b.IsOver; turn++)
                {
                    var u = b.Current;
                    if (u.IsEnemy) b.RunEnemyTurn();
                    else
                    {
                        var body = u.Is(HeroClass.Priest) ? b.AbilityTargets(u, Resurrection).Cast<Point?>().FirstOrDefault() : null;
                        if (body.HasValue && b.TryUseAbility(Resurrection, body.Value)) revives++;
                        var target = b.AttackTargets(u).FirstOrDefault();
                        if (target != null) b.TryAttack(target);
                        else
                        {
                            var nearest = b.Enemies.OrderBy(e => Point.Distance(e.Position, u.Position)).FirstOrDefault();
                            var move = nearest == null ? null : b.MoveOptions(u)
                                .OrderBy(m => Point.Distance(m.Target, nearest.Position)).FirstOrDefault();
                            if (move != null) b.TryMove(move.Target);
                            target = b.AttackTargets(u).FirstOrDefault();
                            if (target != null) b.TryAttack(target);
                        }
                    }
                    b.DrainEvents();
                    if (!b.IsOver) b.EndTurn();
                }
                Assert.IsTrue(b.IsOver, $"Seed {seed} did not finish");
                Assert.IsTrue(b.Units.Where(x => x.Alive).All(x => b.UnitAt(x.Position) == x), "No two living units share a tile");
            }
        }
    }
}
