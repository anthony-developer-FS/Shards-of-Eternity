using System;
using System.Collections.Generic;
using System.Linq;

namespace ShardsOfEternity.Core
{
    public struct DamageInfo
    {
        public int Damage;
        public int CritDamage;
        public double CritChance;
        public bool Flanked;
    }

    public sealed partial class Battle
    {
        public const int SpikeDamage = 4;
        public const int MarkBonus = 3;
        public const int PoisonDamage = 3;

        static int RoundHalfUp(double v) => (int)Math.Floor(v + 0.5);

        static int MarkBonusOn(Unit t) => t.Marked > 0 ? MarkBonus : 0;

        /// <summary>
        /// Damage forecast for an attack. Damage is fixed (base + mark - armor, at least 1); crits are the only luck.
        /// Flanking (an ally of the attacker directly behind the target) adds 25% crit chance.
        /// </summary>
        public DamageInfo GetDamageInfo(Unit a, Unit t, int? baseDamage = null) => GetDamageInfo(a, a.Position, t, baseDamage);

        public DamageInfo GetDamageInfo(Unit a, Point from, Unit t, int? baseDamage = null)
        {
            int b = baseDamage ?? a.Damage;
            if (a.Is(HeroClass.Hunter) && !a.Moved && b == a.Damage) b += 2;
            int dmg = Math.Max(1, b + MarkBonusOn(t) - t.EffectiveArmor);
            double crit = a.Crit;
            bool flank = false;
            if (Point.Distance(from, t.Position) == 1)
            {
                var behind = UnitAt(t.X + (t.X - from.X), t.Y + (t.Y - from.Y));
                if (behind != null && behind != a && behind.IsEnemy == a.IsEnemy)
                {
                    crit += .25;
                    flank = true;
                }
            }
            return new DamageInfo
            {
                Damage = dmg,
                CritDamage = RoundHalfUp(dmg * (1 + a.CritDamage)),
                CritChance = Math.Min(1, crit),
                Flanked = flank,
            };
        }

        public void Strike(Unit a, Unit t, int baseDamage)
        {
            var info = GetDamageInfo(a, t, baseDamage);
            bool crit = Roll() < info.CritChance;
            int n = crit ? info.CritDamage : info.Damage;
            Say($"{a.Name} hits {t.Name} for {n}{(crit ? ", a critical" : "")}{(info.Flanked ? " (flanked)" : "")}.");
            Emit(new BattleEvent { Type = BattleEventType.Attacked, Unit = a, Other = t });
            if (a.IsHero) ChargeUltimate(a, 10);
            Hurt(t, n, crit);
        }

        public void Hurt(Unit t, int n, bool crit)
        {
            t.Hp = Math.Max(0, t.Hp - n);
            if (t.IsHero) ChargeUltimate(t, 5);
            Emit(new BattleEvent { Type = BattleEventType.Damaged, Unit = t, Amount = n, Crit = crit });
            if (t.Hp <= 0 && t.Alive)
            {
                t.Alive = false;
                Say($"{t.Name} falls.");
                Emit(new BattleEvent { Type = BattleEventType.Died, Unit = t });
                if (t.IsEnemy)
                {
                    Slain.Add(t);
                    DropLoot(t);
                }
            }
            CheckEnd();
        }

        /// <summary>Heals up to <paramref name="n"/>. A Priest's heals also cure poison.</summary>
        public void Heal(Unit h, int n, Unit healer = null)
        {
            if (healer != null && healer.Is(HeroClass.Priest) && h.Poison > 0)
            {
                h.Poison = 0;
                Say($"{h.Name} is cured of poison.");
            }
            n = Math.Min(n, h.MaxHp - h.Hp);
            if (n <= 0) return;
            h.Hp += n;
            Emit(new BattleEvent { Type = BattleEventType.Healed, Unit = h, Amount = n });
            Say($"{h.Name} recovers {n} HP.");
        }

        /// <summary>Heroes who have fallen this run. Their bodies stay where they fell.</summary>
        public IEnumerable<Unit> FallenHeroes => Units.Where(u => !u.Alive && u.IsHero);

        public Unit FallenAt(Point p) => FallenHeroes.FirstOrDefault(h => h.Position == p);

        /// <summary>The closest empty tile a unit could stand on, starting with <paramref name="p"/> itself.</summary>
        public Point? FreeTileNear(Point p)
        {
            Point? best = null;
            int bestDistance = int.MaxValue;
            for (int y = 1; y < Height - 1; y++)
                for (int x = 1; x < Width - 1; x++)
                {
                    if (IsWall(x, y) || Map[x, y] == TileType.Exit || UnitAt(x, y) != null) continue;
                    int d = Point.Distance(x, y, p.X, p.Y);
                    if (d < bestDistance)
                    {
                        best = new Point(x, y);
                        bestDistance = d;
                    }
                }
            return best;
        }

        /// <summary>Brings a fallen hero back at <paramref name="at"/>. Their next turn comes one turn interval from now.</summary>
        public void Revive(Unit h, Point at, int hp)
        {
            if (h == null || h.Alive) return;
            h.Alive = true;
            h.Hp = Math.Min(h.MaxHp, Math.Max(1, hp));
            h.X = at.X;
            h.Y = at.Y;
            h.Poison = 0;
            h.Rooted = false;
            h.Marked = 0;
            h.ArmorBuff = 0;
            h.Shards = 0;
            h.Recovery = 0;
            h.NextTurnAt = Clock + h.TurnInterval;
            Say($"{h.Name} rises again with {h.Hp} HP.");
            Emit(new BattleEvent { Type = BattleEventType.Revived, Unit = h, To = at });
        }

        /// <summary>Moves a unit's next turn earlier on the timeline.</summary>
        public void Hasten(Unit h, int n)
        {
            h.NextTurnAt = Math.Max(Clock + 1, h.NextTurnAt - n);
            Say($"{h.Name} moves {n} up the timeline.");
        }

        public int SpellDamage(Unit t, int n) => Math.Max(1, n + MarkBonusOn(t) - t.EffectiveArmor);

        public void Spell(Unit t, int n, string source)
        {
            n = SpellDamage(t, n);
            Say($"{source} hits {t.Name} for {n}.");
            Hurt(t, n, false);
        }

        public string AreaPreview(Point c, int n, bool hitsAllies)
        {
            var hit = Units.Where(v => v.Alive && InArea(v, c) && (hitsAllies || v.IsEnemy)).ToList();
            if (hit.Count == 0) return "Nothing in the area.";
            var mine = hit.Where(v => v.IsHero).ToList();
            return $"Hits {string.Join(", ", hit.Select(v => $"{v.Name} for {SpellDamage(v, n)}"))}." +
                (mine.Count > 0 ? $" Warning: this also hits {Names(mine)}." : "");
        }

        // ---------- hero commands ----------

        bool HeroCanCommand => !IsOver && Current != null && Current.IsHero && Current.Alive;

        /// <summary>Moves the current hero to <paramref name="target"/> if it is one of its move options.</summary>
        public bool TryMove(Point target)
        {
            if (!HeroCanCommand || !CanMove(Current)) return false;
            var m = MoveOptions(Current).FirstOrDefault(o => o.Target == target);
            if (m == null) return false;
            DoMove(Current, m);
            return true;
        }

        void DoMove(Unit u, MoveOption m)
        {
            foreach (var e in FreeAttackers(u, m))
            {
                Say($"{e.Name} gets a free attack as {u.Name} leaves.");
                Strike(e, u, e.Damage);
                if (!u.Alive || IsOver) break;
            }
            if (u.Alive && !IsOver)
            {
                if (m.Kind == MoveKind.Swap)
                {
                    var a = m.Ally;
                    int ox = u.X, oy = u.Y;
                    u.X = a.X; u.Y = a.Y;
                    a.X = ox; a.Y = oy;
                    Emit(new BattleEvent { Type = BattleEventType.Swapped, Unit = u, Other = a });
                    Say($"{u.Name} swaps places with {a.Name}.");
                    PickUp(u, u.Position);
                    PickUp(a, a.Position);
                }
                else
                {
                    u.X = m.Target.X;
                    u.Y = m.Target.Y;
                    Emit(new BattleEvent { Type = BattleEventType.Moved, Unit = u, Path = m.Path, To = m.Target });
                    if (m.Kind == MoveKind.Charge) Say($"{u.Name} charges {m.Distance} tiles.");
                    foreach (var p in m.Path) PickUp(u, p);
                }
                if (CrossesSpikes(u, m))
                {
                    Say($"{u.Name} crosses spikes for {SpikeDamage}.");
                    Hurt(u, SpikeDamage, false);
                }
            }
            SpendMove(u);
            CheckWake();
            if (u.Alive && ExitOpen && !IsOver && Map[u.X, u.Y] == TileType.Exit)
            {
                Outcome = BattleOutcome.FloorComplete;
                Say($"{u.Name} leads the squad through the rift door.");
                Emit(new BattleEvent { Type = BattleEventType.BattleOver });
            }
        }

        public List<Unit> AttackTargets(Unit u) =>
            !CanAct(u) ? new List<Unit>() : Units.Where(t => t.IsEnemy != u.IsEnemy && CanAttack(u, t)).ToList();

        public bool TryAttack(Unit target)
        {
            if (!HeroCanCommand || !CanAct(Current) || target == null || !CanAttack(Current, target)) return false;
            var u = Current;
            if (!target.Awake) WakeAll();
            Strike(u, target, u.Damage);
            SpendAction(u);
            return true;
        }

        // ---------- abilities ----------

        public bool AbilityReady(Unit u, int i) =>
            u.Abilities[i].IsUltimate ? u.Ultimate >= UltimateMax : u.Cooldowns[i] == 0;

        /// <summary>Tiles the ability can be aimed at right now. Empty if it is on cooldown or the action is used.</summary>
        public List<Point> AbilityTargets(Unit u, int i)
        {
            var a = u.Abilities[i];
            var output = new List<Point>();
            if (!CanAct(u) || !AbilityReady(u, i)) return output;
            bool Need(Unit v) => a.Need == null || a.Need(this, u, v);
            switch (a.Target)
            {
                case TargetKind.Self:
                    if (Need(u)) output.Add(u.Position);
                    break;
                case TargetKind.AdjacentEnemy:
                    output.AddRange(Enemies.Where(e => Point.Distance(e.Position, u.Position) == 1 && Need(e)).Select(e => e.Position));
                    break;
                case TargetKind.Ally:
                    output.AddRange(Heroes.Where(h => Point.Distance(h.Position, u.Position) <= a.Range && Need(h)).Select(h => h.Position));
                    break;
                case TargetKind.Enemy:
                    output.AddRange(Enemies.Where(e => Point.Distance(e.Position, u.Position) <= a.Range &&
                        LineOfSight(u.X, u.Y, e.X, e.Y) && Need(e)).Select(e => e.Position));
                    break;
                case TargetKind.Tile:
                    for (int y = 1; y < Height - 1; y++)
                        for (int x = 1; x < Width - 1; x++)
                        {
                            if (IsWall(x, y) || Map[x, y] == TileType.Exit) continue;
                            int d = Point.Distance(x, y, u.X, u.Y);
                            if (d <= a.Range && (d == 0 || LineOfSight(u.X, u.Y, x, y))) output.Add(new Point(x, y));
                        }
                    break;
                case TargetKind.FallenAlly:
                    output.AddRange(FallenHeroes.Where(h => Point.Distance(h.Position, u.Position) <= a.Range && Need(h))
                        .Select(h => h.Position).Distinct());
                    break;
                case TargetKind.AnyTile:
                    for (int y = 1; y < Height - 1; y++)
                        for (int x = 1; x < Width - 1; x++)
                            if (!IsWall(x, y) && Map[x, y] != TileType.Exit && UnitAt(x, y) == null) output.Add(new Point(x, y));
                    break;
            }
            return output;
        }

        public string AbilityPreview(Unit u, int i, Point target) => u.Abilities[i].Preview(this, u, target, UnitAt(target));

        public bool TryUseAbility(int i, Point target)
        {
            if (!HeroCanCommand) return false;
            var u = Current;
            if (i < 0 || i >= u.Abilities.Length || !AbilityTargets(u, i).Contains(target)) return false;
            var a = u.Abilities[i];
            if (Units.Any(e => e.IsEnemy && e.Alive && !e.Awake &&
                    (Point.Distance(e.Position, target) <= 2 || Point.Distance(e.Position, u.Position) <= 2)))
                WakeAll();
            Say($"{u.Name} uses {a.Name}.");
            Emit(new BattleEvent { Type = BattleEventType.AbilityUsed, Unit = u, To = target, Text = a.Name });
            a.Run(this, u, target, UnitAt(target));
            if (a.IsUltimate) u.Ultimate = 0;
            else u.Cooldowns[i] = a.Cooldown;
            u.Recovery += a.Recovery;
            SpendAction(u);
            CheckWake();
            return true;
        }

        // ---------- ability helpers ----------

        /// <summary>Best free tile next to <paramref name="e"/> for Shadowstep: prefers flanking spots, then orthogonal ones.</summary>
        public Point? StepSpot(Unit u, Unit e)
        {
            Point? best = null;
            int bestScore = -1;
            foreach (var d in Point.Directions)
            {
                int x = e.X + d.X, y = e.Y + d.Y;
                var occupant = UnitAt(x, y);
                if (IsWall(x, y) || Map[x, y] == TileType.Exit || (occupant != null && occupant != u)) continue;
                var opposite = UnitAt(e.X - d.X, e.Y - d.Y);
                int score = (opposite != null && opposite.IsHero && opposite != u ? 2 : 0) + (d.X != 0 && d.Y != 0 ? 0 : 1);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = new Point(x, y);
                }
            }
            return best;
        }

        public void Teleport(Unit u, Point to)
        {
            u.X = to.X;
            u.Y = to.Y;
            Emit(new BattleEvent { Type = BattleEventType.Teleported, Unit = u, To = to });
            PickUp(u, to);
        }

        public List<Unit> LightningChain(Unit t)
        {
            var chain = new List<Unit> { t };
            while (chain.Count < 3)
            {
                var last = chain[chain.Count - 1];
                var next = Enemies.Where(e => !chain.Contains(e) && Point.Distance(e.Position, last.Position) <= 2)
                    .OrderBy(e => Point.Distance(e.Position, last.Position)).FirstOrDefault();
                if (next == null) break;
                chain.Add(next);
            }
            return chain;
        }

        /// <summary>
        /// Knockback path: each step goes straight away from the attacker, or bends 45 degrees if blocked.
        /// Every step must increase the distance from the attacker.
        /// </summary>
        public List<Point> KnockbackPath(Unit a, Unit t, int n, out bool blocked)
        {
            int i = Array.FindIndex(Point.Ring, r => r.X == Math.Sign(t.X - a.X) && r.Y == Math.Sign(t.Y - a.Y));
            var dirs = new[] { Point.Ring[i], Point.Ring[(i + 1) % 8], Point.Ring[(i + 7) % 8] };
            int x = t.X, y = t.Y;
            var steps = new List<Point>();
            for (int k = 0; k < n; k++)
            {
                bool found = false;
                foreach (var d in dirs)
                {
                    int nx = x + d.X, ny = y + d.Y;
                    if (IsWall(nx, ny) || UnitAt(nx, ny) != null || Map[nx, ny] == TileType.Exit) continue;
                    if (d.X != 0 && d.Y != 0 && (IsWall(x + d.X, y) || IsWall(x, y + d.Y))) continue;
                    if (Point.Distance(nx, ny, a.X, a.Y) <= Point.Distance(x, y, a.X, a.Y)) continue;
                    x = nx;
                    y = ny;
                    steps.Add(new Point(x, y));
                    found = true;
                    break;
                }
                if (!found)
                {
                    blocked = true;
                    return steps;
                }
            }
            blocked = false;
            return steps;
        }

        public void Knockback(Unit a, Unit t, int n)
        {
            var steps = KnockbackPath(a, t, n, out bool blocked);
            foreach (var p in steps)
            {
                t.X = p.X;
                t.Y = p.Y;
                Emit(new BattleEvent { Type = BattleEventType.KnockedBack, Unit = t, To = p });
                if (Map[p] == TileType.Spikes)
                {
                    Say($"{t.Name} is knocked onto spikes for {SpikeDamage}.");
                    Hurt(t, SpikeDamage, false);
                    if (!t.Alive) return;
                }
            }
            if (steps.Count > 0) Say($"{t.Name} is knocked back {steps.Count} tile{(steps.Count > 1 ? "s" : "")}.");
            if (blocked)
            {
                Say($"{t.Name} slams into an obstacle for 2.");
                Hurt(t, 2, false);
            }
        }

        public string KnockbackPreview(Unit a, Unit t, int n)
        {
            var steps = KnockbackPath(a, t, n, out bool blocked);
            if (steps.Count == 0) return "nowhere, because it is blocked, so it takes 2 extra damage instead";
            return $"{steps.Count} tile{(steps.Count > 1 ? "s" : "")}" +
                (blocked ? ", then it slams into an obstacle for 2" : "") +
                (steps.Any(p => Map[p] == TileType.Spikes) ? $", onto spikes for {SpikeDamage}" : "");
        }
    }
}
