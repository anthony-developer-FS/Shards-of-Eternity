using System;

namespace ShardsOfEternity.Core
{
    /// <summary>A tile coordinate on the board. X grows east, Y grows south (row 0 is the top row).</summary>
    public readonly struct Point : IEquatable<Point>
    {
        public readonly int X;
        public readonly int Y;

        public Point(int x, int y) { X = x; Y = y; }

        /// <summary>The 8 neighbour directions: orthogonal first, then diagonal.</summary>
        public static readonly Point[] Directions =
        {
            new Point(1, 0), new Point(-1, 0), new Point(0, 1), new Point(0, -1),
            new Point(1, 1), new Point(1, -1), new Point(-1, 1), new Point(-1, -1),
        };

        /// <summary>The 8 directions in clockwise ring order, used by knockback.</summary>
        public static readonly Point[] Ring =
        {
            new Point(1, 0), new Point(1, 1), new Point(0, 1), new Point(-1, 1),
            new Point(-1, 0), new Point(-1, -1), new Point(0, -1), new Point(1, -1),
        };

        /// <summary>Chebyshev distance: diagonal steps cost the same as straight ones.</summary>
        public static int Distance(Point a, Point b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        public static int Distance(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

        public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y);
        public static Point operator -(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y);
        public static bool operator ==(Point a, Point b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Point a, Point b) => !(a == b);

        public bool Equals(Point other) => this == other;
        public override bool Equals(object obj) => obj is Point p && this == p;
        public override int GetHashCode() => (X * 397) ^ Y;
        public override string ToString() => $"({X},{Y})";
    }
}
