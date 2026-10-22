using System.Linq;
using NUnit.Framework;
using ShardsOfEternity.Core;

namespace ShardsOfEternity.Tests
{
    public class RoomGeneratorTests
    {
        [Test]
        public void SameSeed_GivesSameRoom()
        {
            var a = RoomGenerator.Generate(1234);
            var b = RoomGenerator.Generate(1234);
            CollectionAssert.AreEqual(a.Map.ToRows(), b.Map.ToRows());
            CollectionAssert.AreEqual(a.Enemies.Select(e => e.Position), b.Enemies.Select(e => e.Position));
            Assert.AreSame(a.Theme, b.Theme);
        }

        [Test]
        public void GeneratedRooms_AreFullyConnectedAndPlayable()
        {
            for (int seed = 0; seed < 300; seed++)
            {
                var room = RoomGenerator.Generate(seed);
                var map = room.Map;
                string rows = string.Join("\n", map.ToRows());

                var exit = map.Find(TileType.Exit);
                Assert.IsTrue(exit.HasValue, rows);
                Assert.AreEqual(map.Width - 1, exit.Value.X, rows);
                Assert.IsTrue(map.Find(TileType.Spawn).HasValue, rows);

                Assert.IsTrue(RoomGenerator.AllOpenTilesConnected(map.Width, map.Height,
                    (x, y) => map.IsBorder(x, y) || map[x, y] == TileType.Wall || map[x, y] == TileType.Pillar, exit), rows);

                Assert.AreEqual(4, room.HeroSlots.Count);
                foreach (var slot in room.HeroSlots)
                    Assert.AreEqual(TileType.Floor, map[slot], $"Seed {seed}\n{rows}");

                Assert.GreaterOrEqual(room.Enemies.Count, 5, rows);
                Assert.AreEqual(room.Enemies.Count, room.Enemies.Select(e => e.Position).Distinct().Count());
                foreach (var e in room.Enemies)
                {
                    Assert.AreEqual(TileType.Floor, map[e.Position], rows);
                    Assert.IsTrue(room.HeroSlots.All(h => Point.Distance(h, e.Position) > 5), "Enemies start out of wake range");
                }
            }
        }

        [Test]
        public void Themes_ChangeTheEnemyMix()
        {
            var crypt = RoomGenerator.Generate(7, Theme.AshenCrypt);
            var forge = RoomGenerator.Generate(7, Theme.Hellforge);
            Assert.AreSame(Theme.AshenCrypt, crypt.Theme);
            Assert.AreSame(Theme.Hellforge, forge.Theme);
            Assert.LessOrEqual(crypt.Enemies.Count(e => e.Kind == EnemyKind.Hellknight), 1);
        }
    }
}
