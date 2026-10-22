using System;
using System.Collections.Generic;
using System.Linq;

namespace ShardsOfEternity.Core
{
    public sealed partial class Battle
    {
        /// <summary>Walking distance from every tile to the nearest hero, ignoring units.</summary>
        int[] HeroDistances()
        {
            int W = Width;
            var dist = Enumerable.Repeat(int.MaxValue, W * Height).ToArray();
            var queue = new Queue<Point>();
            foreach (var h in Heroes)
            {
                dist[h.Y * W + h.X] = 0;
                queue.Enqueue(h.Position);
            }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int d = dist[c.Y * W + c.X];
                foreach (var dir in Point.Directions)
                {
                    int nx = c.X + dir.X, ny = c.Y + dir.Y;
                    if (IsWall(nx, ny) || (dir.X != 0 && dir.Y != 0 && (IsWall(c.X + dir.X, c.Y) || IsWall(c.X, c.Y + dir.Y)))) continue;
                    int k = ny * W + nx;
                    if (dist[k] <= d + 1) continue;
                    dist[k] = d + 1;
                    queue.Enqueue(new Point(nx, ny));
                }
            }
            return dist;
        }

        /// <summary>
        /// Plays the current enemy's turn: move toward a spot it can attack from (ranged enemies prefer distance
        /// and back away from melee), then hit the hero it can bring lowest. Call <see cref="EndTurn"/> afterwards.
        /// </summary>
        public void RunEnemyTurn()
        {
            var u = Current;
            if (IsOver || u == null || !u.IsEnemy || !u.Alive) return;

            var targets = Heroes.Where(h => CanHitFrom(u, u.X, u.Y, h)).ToList();
            bool ranged = u.Range > 1;
            if (u.Rooted)
            {
                Say($"{u.Name} is pinned and cannot move.");
            }
            else if (targets.Count == 0 || (ranged && Heroes.Any(h => Point.Distance(h.Position, u.Position) == 1)))
            {
                var dist = HeroDistances();
                var options = Reach(u);
                options.Add(new MoveOption { Target = u.Position, Distance = 0, Path = new List<Point>(), Kind = MoveKind.Walk });
                MoveOption best = null;
                double bestScore = double.NegativeInfinity;
                var heroes = Heroes.ToList();
                foreach (var m in options)
                {
                    double score;
                    if (heroes.Any(h => CanHitFrom(u, m.Target.X, m.Target.Y, h)))
                    {
                        score = 1000 - m.Distance;
                        if (ranged) score += 10 * heroes.Min(h => Point.Distance(h.Position, m.Target));
                    }
                    else
                    {
                        int d = dist[m.Target.Y * Width + m.Target.X];
                        score = (d == int.MaxValue ? -1e6 : -d * 10) - m.Distance;
                    }
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = m;
                    }
                }
                if (best != null && best.Distance > 0)
                {
                    u.X = best.Target.X;
                    u.Y = best.Target.Y;
                    Emit(new BattleEvent { Type = BattleEventType.Moved, Unit = u, Path = best.Path, To = best.Target });
                }
            }

            targets = Heroes.Where(h => CanHitFrom(u, u.X, u.Y, h)).ToList();
            if (targets.Count > 0)
            {
                var target = targets.OrderBy(h => h.Hp - GetDamageInfo(u, h).Damage).First();
                Strike(u, target, u.Damage);
            }
            u.Rooted = false;
        }
    }
}
