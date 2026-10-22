using System;
using System.Collections.Generic;
using System.Linq;

namespace ShardsOfEternity.Core
{
    public enum MoveKind
    {
        Walk,
        /// <summary>Barbarian: up to 12 tiles in a straight line, ending next to an enemy.</summary>
        Charge,
        /// <summary>Shaman: trade places with an adjacent ally.</summary>
        Swap,
    }

    public sealed class MoveOption
    {
        public Point Target;
        public int Distance;
        public List<Point> Path;
        public MoveKind Kind;
        public Unit Ally;
    }

    public sealed partial class Battle
    {
        public bool IsWall(int x, int y)
        {
            if (!Map.InBounds(x, y)) return true;
            var t = Map[x, y];
            return t == TileType.Wall || t == TileType.Pillar || (t == TileType.Exit && !ExitOpen);
        }

        public Unit UnitAt(int x, int y)
        {
            foreach (var u in Units)
                if (u.Alive && u.X == x && u.Y == y) return u;
            return null;
        }

        public Unit UnitAt(Point p) => UnitAt(p.X, p.Y);

        /// <summary>Bresenham line of sight. Only walls, pillars and the closed exit block it; units do not.</summary>
        public bool LineOfSight(int x0, int y0, int x1, int y1)
        {
            int dx = Math.Abs(x1 - x0), dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, x = x0, y = y0;
            while (true)
            {
                if (x == x1 && y == y1) return true;
                int e2 = 2 * err;
                if (e2 >= dy) { err += dy; x += sx; }
                if (e2 <= dx) { err += dx; y += sy; }
                if (x == x1 && y == y1) return true;
                if (IsWall(x, y)) return false;
            }
        }

        /// <summary>
        /// Body blocking: heroes can't pass through anyone; enemies pass through enemies but not heroes.
        /// Diagonal steps can't cut wall corners. Enemies won't walk onto spikes.
        /// </summary>
        public bool CanStep(Unit u, int x, int y, int dx, int dy)
        {
            int nx = x + dx, ny = y + dy;
            if (IsWall(nx, ny)) return false;
            if (dx != 0 && dy != 0 && (IsWall(x + dx, y) || IsWall(x, y + dy))) return false;
            var other = UnitAt(nx, ny);
            if (other != null && !(u.IsEnemy && other.IsEnemy)) return false;
            if (u.IsEnemy && Map[nx, ny] == TileType.Spikes) return false;
            return true;
        }

        /// <summary>Paladin's Hold the line: enemies must stop next to him.</summary>
        bool NearGuardian(int x, int y) =>
            Units.Any(h => h.Alive && h.Is(HeroClass.Paladin) && Point.Distance(h.X, h.Y, x, y) == 1);

        /// <summary>Every tile the unit can walk to this turn, with the path to get there.</summary>
        public List<MoveOption> Reach(Unit u)
        {
            int W = Width;
            int start = u.Y * W + u.X;
            var dist = new Dictionary<int, int> { [start] = 0 };
            var prev = new Dictionary<int, int>();
            var order = new List<int> { start };
            var queue = new Queue<Point>();
            queue.Enqueue(u.Position);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int d = dist[c.Y * W + c.X];
                if (d >= u.Move) continue;
                if (u.IsEnemy && d > 0 && NearGuardian(c.X, c.Y)) continue;
                foreach (var dir in Point.Directions)
                {
                    if (!CanStep(u, c.X, c.Y, dir.X, dir.Y)) continue;
                    int k = (c.Y + dir.Y) * W + c.X + dir.X;
                    if (dist.ContainsKey(k)) continue;
                    dist[k] = d + 1;
                    prev[k] = c.Y * W + c.X;
                    order.Add(k);
                    queue.Enqueue(c + dir);
                }
            }

            var output = new List<MoveOption>();
            foreach (int k in order)
            {
                int d = dist[k];
                if (d == 0) continue;
                int x = k % W, y = k / W;
                if (UnitAt(x, y) != null || Map[x, y] == TileType.Exit) continue;
                var path = new List<Point>();
                for (int c = k; c != start; c = prev[c]) path.Insert(0, new Point(c % W, c / W));
                output.Add(new MoveOption { Target = new Point(x, y), Distance = d, Path = path, Kind = MoveKind.Walk });
            }
            return output;
        }

        /// <summary>Walks plus any class movement rule (Barbarian charge, Shaman swap).</summary>
        public List<MoveOption> MoveOptions(Unit u)
        {
            var options = Reach(u);
            var have = new HashSet<Point>(options.Select(m => m.Target));
            if (u.Is(HeroClass.Barbarian))
            {
                foreach (var dir in Point.Directions)
                {
                    int x = u.X, y = u.Y;
                    var path = new List<Point>();
                    for (int i = 1; i <= 12; i++)
                    {
                        if (!CanStep(u, x, y, dir.X, dir.Y)) break;
                        x += dir.X;
                        y += dir.Y;
                        if (UnitAt(x, y) != null) break;
                        var p = new Point(x, y);
                        path.Add(p);
                        if (have.Contains(p)) continue;
                        if (Enemies.Any(e => Point.Distance(e.X, e.Y, x, y) == 1))
                        {
                            options.Add(new MoveOption { Target = p, Distance = i, Path = new List<Point>(path), Kind = MoveKind.Charge });
                            have.Add(p);
                        }
                    }
                }
            }
            if (u.Is(HeroClass.Shaman))
            {
                foreach (var a in Heroes)
                    if (a != u && Point.Distance(a.Position, u.Position) == 1)
                        options.Add(new MoveOption { Target = a.Position, Distance = 1, Path = new List<Point> { a.Position }, Kind = MoveKind.Swap, Ally = a });
            }
            return options;
        }

        public bool CrossesSpikes(Unit u, MoveOption m) =>
            !u.Slips && !u.Levitates && m.Path.Any(p => Map[p] == TileType.Spikes);

        /// <summary>Awake melee enemies that get a free hit when the unit steps out of their reach.</summary>
        public List<Unit> FreeAttackers(Unit u, MoveOption m)
        {
            if (u.Slips || m.Kind == MoveKind.Swap) return new List<Unit>();
            return Units.Where(e => e.Alive && e.IsEnemy && e.Awake && e.Range == 1 &&
                Point.Distance(e.Position, u.Position) == 1 && Point.Distance(e.Position, m.Target) > 1).ToList();
        }

        public bool CanHitFrom(Unit u, int x, int y, Unit t) =>
            Point.Distance(x, y, t.X, t.Y) <= u.Range && (u.Range == 1 || LineOfSight(x, y, t.X, t.Y));

        public bool CanAttack(Unit u, Unit t) => t.Alive && t.IsEnemy != u.IsEnemy && CanHitFrom(u, u.X, u.Y, t);

        public List<Unit> FoesNear(Point c, int radius) =>
            Enemies.Where(e => Point.Distance(e.Position, c) <= radius).ToList();

        public List<Unit> FriendsNear(Point c, int radius) =>
            Heroes.Where(h => Point.Distance(h.Position, c) <= radius).ToList();

        /// <summary>Awake enemies within 8 tiles that the unit can see.</summary>
        public List<Unit> SightFoes(Unit u) =>
            Enemies.Where(e => e.Awake && Point.Distance(e.Position, u.Position) <= 8 && LineOfSight(u.X, u.Y, e.X, e.Y)).ToList();

        public static bool InArea(Unit v, Point c) => Math.Abs(v.X - c.X) <= 1 && Math.Abs(v.Y - c.Y) <= 1;

        public static string Names(IEnumerable<Unit> list) => string.Join(", ", list.Select(v => v.Name));
    }
}
