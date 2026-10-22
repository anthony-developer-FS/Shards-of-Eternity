using System;

namespace ShardsOfEternity.Core
{
    public enum TileType
    {
        Floor,
        Wall,
        Pillar,
        Spikes,
        Exit,
        Spawn,
    }

    /// <summary>The tiles of one room. Units are tracked by <see cref="Battle"/>, not here.</summary>
    public sealed class GridMap
    {
        readonly TileType[,] tiles;

        public int Width { get; }
        public int Height { get; }

        public GridMap(int width, int height)
        {
            Width = width;
            Height = height;
            tiles = new TileType[width, height];
        }

        public TileType this[int x, int y]
        {
            get => tiles[x, y];
            set => tiles[x, y] = value;
        }

        public TileType this[Point p]
        {
            get => tiles[p.X, p.Y];
            set => tiles[p.X, p.Y] = value;
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public bool InBounds(Point p) => InBounds(p.X, p.Y);

        public bool IsBorder(int x, int y) => x == 0 || y == 0 || x == Width - 1 || y == Height - 1;

        public Point? Find(TileType type)
        {
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (tiles[x, y] == type) return new Point(x, y);
            return null;
        }

        public GridMap Clone()
        {
            var copy = new GridMap(Width, Height);
            Array.Copy(tiles, copy.tiles, tiles.Length);
            return copy;
        }

        /// <summary>Character legend used by the browser prototype and by tests.</summary>
        public static TileType FromChar(char c)
        {
            switch (c)
            {
                case '#': return TileType.Wall;
                case 'o': return TileType.Pillar;
                case '^': return TileType.Spikes;
                case 'E': return TileType.Exit;
                case 'S': return TileType.Spawn;
                case '.': return TileType.Floor;
                default: throw new ArgumentException($"Unknown map character '{c}'");
            }
        }

        public static char ToChar(TileType t)
        {
            switch (t)
            {
                case TileType.Wall: return '#';
                case TileType.Pillar: return 'o';
                case TileType.Spikes: return '^';
                case TileType.Exit: return 'E';
                case TileType.Spawn: return 'S';
                default: return '.';
            }
        }

        public static GridMap FromRows(params string[] rows)
        {
            var map = new GridMap(rows[0].Length, rows.Length);
            for (int y = 0; y < rows.Length; y++)
            {
                if (rows[y].Length != map.Width) throw new ArgumentException("Map rows must all have the same length");
                for (int x = 0; x < map.Width; x++) map[x, y] = FromChar(rows[y][x]);
            }
            return map;
        }

        public string[] ToRows()
        {
            var rows = new string[Height];
            var chars = new char[Width];
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++) chars[x] = ToChar(tiles[x, y]);
                rows[y] = new string(chars);
            }
            return rows;
        }
    }
}
