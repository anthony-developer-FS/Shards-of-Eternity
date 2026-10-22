using System;
using System.Collections.Generic;
using System.Linq;

namespace ShardsOfEternity.Core
{
    /// <summary>
    /// A rift theme: which enemies live there, what the rift sends as reinforcements, and the palette the view uses.
    /// Colors are 0xRRGGBB so the rules stay free of engine types.
    /// </summary>
    public sealed class Theme
    {
        public string Name;
        public uint FloorColor;
        public uint WallColor;
        public uint AccentColor;
        /// <summary>Enemies that can start in the room, repeated to weight them.</summary>
        public EnemyKind[] Pool;
        public int MaxHellknights = 1;
        public EnemyKind[] FirstWave;
        public EnemyKind[] LaterWaves;

        public static readonly Theme AshenCrypt = new Theme
        {
            Name = "Ashen Crypt",
            FloorColor = 0x3B3D47, WallColor = 0x1E1F26, AccentColor = 0x7FD4A0,
            Pool = new[] { EnemyKind.Ghoul, EnemyKind.Ghoul, EnemyKind.Ghoul, EnemyKind.Imp, EnemyKind.Hellknight },
            FirstWave = new[] { EnemyKind.Ghoul, EnemyKind.Ghoul },
            LaterWaves = new[] { EnemyKind.Ghoul, EnemyKind.Imp, EnemyKind.Ghoul },
        };

        public static readonly Theme Hellforge = new Theme
        {
            Name = "Hellforge",
            FloorColor = 0x402A24, WallColor = 0x1F1310, AccentColor = 0xFF7A3D,
            Pool = new[] { EnemyKind.Ghoul, EnemyKind.Imp, EnemyKind.Imp, EnemyKind.Hellknight, EnemyKind.Hellknight },
            MaxHellknights = 2,
            FirstWave = new[] { EnemyKind.Imp, EnemyKind.Ghoul },
            LaterWaves = new[] { EnemyKind.Imp, EnemyKind.Ghoul, EnemyKind.Imp },
        };

        public static readonly Theme[] All = { AshenCrypt, Hellforge };
    }

    public struct EnemySpawn
    {
        public EnemyKind Kind;
        public Point Position;

        public EnemySpawn(EnemyKind kind, Point position)
        {
            Kind = kind;
            Position = position;
        }
    }

    /// <summary>Everything needed to start a battle in one room.</summary>
    public sealed class RoomLayout
    {
        public GridMap Map;
        public Theme Theme;
        public List<Point> HeroSlots = new List<Point>();
        public List<EnemySpawn> Enemies = new List<EnemySpawn>();
        /// <summary>The seed the room was generated from, or -1 for a handmade room.</summary>
        public int Seed = -1;

        /// <summary>The handmade room from the browser prototype.</summary>
        public static RoomLayout Prototype() => new RoomLayout
        {
            Map = GridMap.FromRows(
                "##############",
                "#......#.....#",
                "#..^...#..o..#",
                "#......#.....#",
                "#....o.......E",
                "#............#",
                "#..#.....^...#",
                "#..#....o....#",
                "#........##..#",
                "#...........S#",
                "##############"),
            Theme = Theme.AshenCrypt,
            HeroSlots = { new Point(3, 4), new Point(3, 5), new Point(2, 3), new Point(1, 5) },
            Enemies =
            {
                new EnemySpawn(EnemyKind.Ghoul, new Point(8, 1)),
                new EnemySpawn(EnemyKind.Ghoul, new Point(9, 3)),
                new EnemySpawn(EnemyKind.Ghoul, new Point(11, 7)),
                new EnemySpawn(EnemyKind.Imp, new Point(11, 1)),
                new EnemySpawn(EnemyKind.Imp, new Point(12, 5)),
                new EnemySpawn(EnemyKind.Hellknight, new Point(11, 4)),
            },
        };
    }

    /// <summary>
    /// Builds a random combat room from a seed: walls, pillars and spikes scattered so every open tile stays
    /// reachable, the squad on the west side, a sleeping enemy group on the east side, and the exit and rift
    /// spawn on the east wall. The same seed always gives the same room.
    /// </summary>
    public static class RoomGenerator
    {
        public static RoomLayout Generate(int seed, Theme theme = null)
        {
            var rng = new Random(seed);
            theme = theme ?? Theme.All[rng.Next(Theme.All.Length)];
            int W = 14 + rng.Next(5), H = 9 + rng.Next(3);
            var map = new GridMap(W, H);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    map[x, y] = map.IsBorder(x, y) ? TileType.Wall : TileType.Floor;

            int exitY = 2 + rng.Next(H - 4);
            var exit = new Point(W - 1, exitY);
            map[exit] = TileType.Exit;
            var spawn = new Point(W - 2, exitY < H / 2 ? H - 2 : 1);
            map[spawn] = TileType.Spawn;

            int mid = H / 2;
            var layout = new RoomLayout { Map = map, Theme = theme, Seed = seed };
            layout.HeroSlots.AddRange(new[] { new Point(3, mid - 1), new Point(3, mid), new Point(2, mid - 2), new Point(1, mid) });

            // Keep the squad's start, the exit approach and the rift spawn clear.
            var reserved = new HashSet<Point>();
            foreach (var c in layout.HeroSlots.Concat(new[] { exit, spawn }))
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        reserved.Add(new Point(c.X + dx, c.Y + dy));

            bool Free(Point p) => !map.IsBorder(p.X, p.Y) && map.InBounds(p) && map[p] == TileType.Floor && !reserved.Contains(p);
            bool Blocking(TileType t) => t == TileType.Wall || t == TileType.Pillar;

            void Place(TileType type, int count, int minLength, int maxLength)
            {
                for (int placed = 0, tries = 0; placed < count && tries < 80; tries++)
                {
                    int x = 1 + rng.Next(W - 2), y = 1 + rng.Next(H - 2);
                    bool horizontal = rng.Next(2) == 0;
                    int length = minLength + rng.Next(maxLength - minLength + 1);
                    var tiles = Enumerable.Range(0, length).Select(i => new Point(x + (horizontal ? i : 0), y + (horizontal ? 0 : i))).ToList();
                    if (!tiles.All(Free)) continue;
                    foreach (var t in tiles) map[t] = type;
                    if (Blocking(type) && !AllOpenTilesConnected(W, H, (cx, cy) => map.IsBorder(cx, cy) || Blocking(map[cx, cy]), exit))
                    {
                        foreach (var t in tiles) map[t] = TileType.Floor;
                        continue;
                    }
                    placed++;
                }
            }

            Place(TileType.Wall, 2 + rng.Next(2), 2, 3);
            Place(TileType.Pillar, 2 + rng.Next(3), 1, 1);
            Place(TileType.Spikes, 2 + rng.Next(2), 1, 2);

            // Sleeping enemies on the far side, more than 5 tiles from every hero so the room starts quiet.
            var candidates = new List<Point>();
            for (int y = 1; y < H - 1; y++)
                for (int x = W / 2; x < W - 1; x++)
                {
                    var p = new Point(x, y);
                    if (Free(p) && layout.HeroSlots.All(h => Point.Distance(h, p) > 5)) candidates.Add(p);
                }
            int enemyCount = 5 + rng.Next(3), knights = 0;
            while (layout.Enemies.Count < enemyCount && candidates.Count > 0)
            {
                var p = candidates[rng.Next(candidates.Count)];
                candidates.Remove(p);
                var kind = theme.Pool[rng.Next(theme.Pool.Length)];
                if (kind == EnemyKind.Hellknight && ++knights > theme.MaxHellknights) kind = EnemyKind.Ghoul;
                layout.Enemies.Add(new EnemySpawn(kind, p));
            }
            return layout;
        }

        /// <summary>
        /// Flood fill (4 directions) from the tile beside the exit. True if every unblocked interior tile is reached,
        /// so no wall layout can seal off part of the room.
        /// </summary>
        public static bool AllOpenTilesConnected(int width, int height, Func<int, int, bool> blocked, Point? exit)
        {
            var orthogonal = Point.Directions.Take(4).ToArray();
            Point? start = null;
            if (exit.HasValue)
            {
                foreach (var d in orthogonal)
                {
                    var n = exit.Value + d;
                    if (n.X > 0 && n.Y > 0 && n.X < width - 1 && n.Y < height - 1 && !blocked(n.X, n.Y)) { start = n; break; }
                }
            }
            if (!start.HasValue)
            {
                for (int y = 1; y < height - 1 && !start.HasValue; y++)
                    for (int x = 1; x < width - 1; x++)
                        if (!blocked(x, y)) { start = new Point(x, y); break; }
            }
            if (!start.HasValue) return true;

            var seen = new HashSet<Point> { start.Value };
            var queue = new Queue<Point>();
            queue.Enqueue(start.Value);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in orthogonal)
                {
                    var n = c + d;
                    if (n.X < 0 || n.Y < 0 || n.X >= width || n.Y >= height) continue;
                    if (blocked(n.X, n.Y) || !seen.Add(n)) continue;
                    queue.Enqueue(n);
                }
            }
            for (int y = 1; y < height - 1; y++)
                for (int x = 1; x < width - 1; x++)
                    if (!blocked(x, y) && !seen.Contains(new Point(x, y))) return false;
            return true;
        }
    }
}
