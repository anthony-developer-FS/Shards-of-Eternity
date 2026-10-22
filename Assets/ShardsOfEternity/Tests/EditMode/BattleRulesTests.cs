using System.Linq;
using NUnit.Framework;
using ShardsOfEternity.Core;

namespace ShardsOfEternity.Tests
{
    public class BattleRulesTests
    {
        /// <summary>An empty battle on a custom map. Crits never happen unless a test says so.</summary>
        static Battle Arena(params string[] rows)
        {
            var layout = new RoomLayout { Map = GridMap.FromRows(rows), Theme = Theme.AshenCrypt };
            return new Battle(layout, new string[0], 1) { Roll = () => 1 };
        }

        static Unit Hero(Battle b, string id, int x, int y) => b.AddUnit(Roster.Hero(id), new Point(x, y));

        static Unit Foe(Battle b, EnemyKind kind, int x, int y)
        {
            var u = b.AddUnit(Roster.Enemy(kind), new Point(x, y));
            u.Awake = true;
            return u;
        }

        [Test]
        public void PrototypeRoom_MatchesBrowserPrototype()
        {
            var room = RoomLayout.Prototype();
            Assert.AreEqual(14, room.Map.Width);
            Assert.AreEqual(11, room.Map.Height);
            Assert.AreEqual(new Point(13, 4), room.Map.Find(TileType.Exit));
            Assert.AreEqual(new Point(12, 9), room.Map.Find(TileType.Spawn));
            Assert.AreEqual(6, room.Enemies.Count);
        }

        [Test]
        public void Heroes_CannotWalkThroughAnyone()
        {
            var b = Arena("#######", "#.....#", "#######");
            var kael = Hero(b, "kael", 1, 1);
            Foe(b, EnemyKind.Ghoul, 3, 1);
            var targets = b.Reach(kael).Select(m => m.Target).ToList();
            CollectionAssert.AreEquivalent(new[] { new Point(2, 1) }, targets);
        }

        [Test]
        public void Heroes_CannotWalkThroughAllies()
        {
            var b = Arena("#######", "#.....#", "#######");
            var kael = Hero(b, "kael", 1, 1);
            Hero(b, "bram", 2, 1);
            Assert.IsEmpty(b.Reach(kael));
        }

        [Test]
        public void Enemies_PassThroughEnemies_ButNeverStopOnThem()
        {
            var b = Arena("#######", "#.....#", "#######");
            var ghoul = Foe(b, EnemyKind.Ghoul, 1, 1);
            Foe(b, EnemyKind.Ghoul, 2, 1);
            var targets = b.Reach(ghoul).Select(m => m.Target).ToList();
            CollectionAssert.Contains(targets, new Point(3, 1));
            CollectionAssert.DoesNotContain(targets, new Point(2, 1));
        }

        [Test]
        public void Enemies_CannotPassThroughHeroes()
        {
            var b = Arena("#######", "#.....#", "#######");
            var ghoul = Foe(b, EnemyKind.Ghoul, 1, 1);
            Hero(b, "kael", 3, 1);
            CollectionAssert.AreEquivalent(new[] { new Point(2, 1) }, b.Reach(ghoul).Select(m => m.Target).ToList());
        }

        [Test]
        public void Paladin_HoldsTheLine()
        {
            var b = Arena("#########", "#.......#", "#.......#", "#.......#", "#########");
            Hero(b, "bram", 3, 2);
            var ghoul = Foe(b, EnemyKind.Ghoul, 1, 2);
            Assert.IsTrue(b.Reach(ghoul).All(m => m.Target.X <= 4), "Enemies must stop when they step next to the Paladin");
        }

        [Test]
        public void Barbarian_ChargesInAStraightLine()
        {
            var b = Arena("################", "#..............#", "################");
            var kael = Hero(b, "kael", 1, 1);
            Foe(b, EnemyKind.Ghoul, 12, 1);
            var charge = b.MoveOptions(kael).Single(m => m.Kind == MoveKind.Charge);
            Assert.AreEqual(new Point(11, 1), charge.Target);
            Assert.AreEqual(10, charge.Distance);
        }

        [Test]
        public void TurnOrder_FollowsAttackSpeed()
        {
            var b = new Battle(RoomLayout.Prototype(), Roster.DefaultSquad, 1);
            b.Start();
            Assert.AreEqual("Nyra", b.Current.Name);
            var order = b.Forecast(5).Select(e => e.Unit.Name).ToArray();
            CollectionAssert.AreEqual(new[] { "Nyra", "Kael", "Ilse", "Bram", "Nyra" }, order);
        }

        [Test]
        public void Damage_ArmorReduces_ButNeverBelowOne()
        {
            var b = Arena("#####", "#...#", "#####");
            var kael = Hero(b, "kael", 1, 1);
            var knight = Foe(b, EnemyKind.Hellknight, 2, 1);
            Assert.AreEqual(5, b.GetDamageInfo(kael, knight).Damage);
            Assert.AreEqual(1, b.GetDamageInfo(kael, knight, 1).Damage);
        }

        [Test]
        public void Flanking_AddsCritChance()
        {
            var b = Arena("#####", "#...#", "#####");
            var kael = Hero(b, "kael", 1, 1);
            var ghoul = Foe(b, EnemyKind.Ghoul, 2, 1);
            Assert.IsFalse(b.GetDamageInfo(kael, ghoul).Flanked);
            Hero(b, "bram", 3, 1);
            var info = b.GetDamageInfo(kael, ghoul);
            Assert.IsTrue(info.Flanked);
            Assert.AreEqual(.4, info.CritChance, 1e-9);
        }

        [Test]
        public void Hunter_SteadyAim_OnlyBeforeMoving()
        {
            var b = Arena("#########", "#.......#", "#########");
            var wren = Hero(b, "wren", 1, 1);
            var ghoul = Foe(b, EnemyKind.Ghoul, 6, 1);
            Assert.AreEqual(8, b.GetDamageInfo(wren, ghoul).Damage);
            wren.Moved = true;
            Assert.AreEqual(6, b.GetDamageInfo(wren, ghoul).Damage);
        }

        [Test]
        public void HolySmite_IntoWall_DealsObstacleDamage()
        {
            var b = Arena("#####", "#...#", "#####");
            var bram = Hero(b, "bram", 2, 1);
            var ghoul = Foe(b, EnemyKind.Ghoul, 3, 1);
            var path = b.KnockbackPath(bram, ghoul, 2, out bool blocked);
            Assert.IsEmpty(path);
            Assert.IsTrue(blocked);
            b.Knockback(bram, ghoul, 2);
            Assert.AreEqual(ghoul.MaxHp - 2, ghoul.Hp);
        }

        [Test]
        public void Teleport_CanReachAnyOpenTile_WithoutFreeAttacks()
        {
            var b = new Battle(RoomLayout.Prototype(), new[] { "ilse" }, 1) { Roll = () => 1 };
            b.Start();
            var ilse = b.Current;
            var ghoul = b.Units.First(u => u.IsEnemy);
            ghoul.Awake = true;
            ghoul.X = ilse.X + 1;
            ghoul.Y = ilse.Y;
            int teleport = System.Array.FindIndex(ilse.Abilities, a => a.Name == "Teleport");
            Assert.IsTrue(b.TryUseAbility(teleport, new Point(1, 9)));
            Assert.AreEqual(new Point(1, 9), ilse.Position);
            Assert.AreEqual(ilse.MaxHp, ilse.Hp);
            Assert.AreEqual(3, ilse.Cooldowns[teleport]);
        }

        [Test]
        public void LeavingMelee_GivesFreeAttack_ExceptForRogue()
        {
            var b = Arena("#######", "#.....#", "#.....#", "#######");
            var kael = Hero(b, "kael", 2, 1);
            var ghoul = Foe(b, EnemyKind.Ghoul, 3, 1);
            var away = b.Reach(kael).First(m => m.Target == new Point(1, 2));
            CollectionAssert.AreEqual(new[] { ghoul }, b.FreeAttackers(kael, away));
            kael.Alive = false;
            var nyra = Hero(b, "nyra", 2, 1);
            Assert.IsEmpty(b.FreeAttackers(nyra, b.Reach(nyra).First(m => m.Target == new Point(1, 2))));
        }

        [Test]
        public void HeroCommands_RespectMoveAndActionBudget()
        {
            var b = new Battle(RoomLayout.Prototype(), Roster.DefaultSquad, 1);
            b.Start();
            var nyra = b.Current;
            var step = b.MoveOptions(nyra).First().Target;
            Assert.IsTrue(b.TryMove(step));
            Assert.IsFalse(b.TryMove(b.MoveOptions(nyra).FirstOrDefault()?.Target ?? step), "Only one move per turn");
            Assert.IsTrue(nyra.Moved);
            Assert.IsFalse(b.CurrentTurnSpent);
            b.EndTurn();
            Assert.AreEqual("Kael", b.Current.Name);
        }

        [Test]
        public void Room_WakesWhenAHeroComesWithinSight()
        {
            var b = Arena("##########", "#........#", "##########");
            var kael = Hero(b, "kael", 1, 1);
            var ghoul = b.AddUnit(Roster.Enemy(EnemyKind.Ghoul), new Point(8, 1));
            b.Start();
            Assert.IsFalse(b.InCombat);
            Assert.IsTrue(b.TryMove(new Point(3, 1)));
            Assert.IsTrue(b.InCombat);
            Assert.IsTrue(ghoul.Awake);
        }

        [Test]
        public void EnemyTurn_WalksUpAndAttacks()
        {
            var b = Arena("##########", "#........#", "##########");
            var bram = Hero(b, "bram", 1, 1);
            bram.NextTurnAt = 1000;
            var ghoul = Foe(b, EnemyKind.Ghoul, 6, 1);
            ghoul.NextTurnAt = 1;
            b.Start();
            Assert.AreSame(ghoul, b.Current);
            b.RunEnemyTurn();
            Assert.AreEqual(new Point(2, 1), ghoul.Position);
            Assert.AreEqual(bram.MaxHp - (ghoul.Damage - bram.Armor), bram.Hp);
        }

        [Test]
        public void Ultimate_ChargesAndResets()
        {
            var b = Arena("#####", "#...#", "#####");
            var nyra = Hero(b, "nyra", 1, 1);
            var knight = Foe(b, EnemyKind.Hellknight, 2, 1);
            b.Start();
            nyra.Ultimate = Battle.UltimateMax;
            Assert.IsTrue(b.TryUseAbility(3, knight.Position));
            Assert.AreEqual(0, nyra.Ultimate);
            Assert.AreEqual(knight.MaxHp - b.GetDamageInfo(nyra, knight).CritDamage, knight.Hp);
        }

        [Test]
        public void Victory_OpensTheExit()
        {
            var b = Arena("#####", "#...E", "#####");
            Hero(b, "kael", 1, 1);
            var ghoul = b.AddUnit(Roster.Enemy(EnemyKind.Ghoul), new Point(2, 1));
            b.Start();
            ghoul.Hp = 1;
            Assert.IsTrue(b.TryAttack(ghoul));
            Assert.AreEqual(BattleOutcome.Victory, b.Outcome);
            Assert.IsTrue(b.ExitOpen);
            Assert.IsFalse(b.IsWall(4, 1));
        }

        [Test]
        public void EveryClass_HasThreeAbilitiesAndAnUltimate()
        {
            foreach (HeroClass cls in System.Enum.GetValues(typeof(HeroClass)))
            {
                var abilities = AbilityLibrary.For(cls);
                Assert.AreEqual(4, abilities.Length, cls.ToString());
                Assert.AreEqual(1, abilities.Count(a => a.IsUltimate), cls.ToString());
                Assert.IsTrue(abilities[3].IsUltimate, cls.ToString());
            }
        }

        [Test]
        public void SimulatedBattles_AlwaysFinishWithoutErrors()
        {
            // Smoke test: heroes and enemies alternate with a naive hero policy until the battle ends.
            for (int seed = 0; seed < 30; seed++)
            {
                var room = seed % 2 == 0 ? RoomLayout.Prototype() : RoomGenerator.Generate(seed);
                var b = new Battle(room, new[] { "kael", "bram", "wren", "tova" }, seed);
                b.Start();
                for (int turn = 0; turn < 2000 && !b.IsOver; turn++)
                {
                    var u = b.Current;
                    if (u.IsEnemy) b.RunEnemyTurn();
                    else
                    {
                        var target = b.AttackTargets(u).FirstOrDefault();
                        if (target != null) b.TryAttack(target);
                        else
                        {
                            var nearest = b.Units.Where(e => e.Alive && e.IsEnemy)
                                .OrderBy(e => Point.Distance(e.Position, u.Position)).FirstOrDefault();
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
            }
        }
    }
}
