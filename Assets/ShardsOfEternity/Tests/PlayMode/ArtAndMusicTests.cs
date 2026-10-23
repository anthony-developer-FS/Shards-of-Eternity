using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShardsOfEternity.Core;
using ShardsOfEternity.Game;
using UnityEngine;

namespace ShardsOfEternity.Tests
{
    /// <summary>
    /// The generated models and music (Tools/ArtGen, Tools/MusicGen) are all present, load, and match what the board
    /// and the music player ask for.
    /// </summary>
    public class ArtAndMusicTests
    {
        static IEnumerable<string> BoardModels()
        {
            foreach (var theme in Theme.All)
            {
                var key = BoardView.ThemeKey(theme);
                for (int v = 0; v < 4; v++) yield return $"floor_{key}_{v}";
                for (int v = 0; v < 2; v++)
                {
                    yield return $"wall_{key}_{v}";
                    yield return $"rock_{key}_{v}";
                }
                yield return $"pillar_{key}";
                yield return $"light_{key}";
            }
            foreach (var name in new[] { "spikes", "exit_open", "exit_closed", "spawn_crack", "loot_shard", "active_ring",
                         "float_rock_0", "float_rock_1", "float_rock_2", "spire_dark", "spire_light", "rift_tear" })
                yield return name;
        }

        [Test]
        public void EveryThemeHasItsOwnKey()
        {
            var keys = Theme.All.Select(BoardView.ThemeKey).ToList();
            CollectionAssert.AllItemsAreUnique(keys);
            CollectionAssert.AreEquivalent(new[] { "crypt", "hellforge", "cathedral", "wastes" }, keys);
        }

        [Test]
        public void EveryUnitHasAModel()
        {
            var defs = Roster.Heroes.Concat(Roster.Enemies).ToList();
            foreach (var kind in (EnemyKind[])Enum.GetValues(typeof(EnemyKind)))
                if (defs.All(d => !d.IsEnemy || d.EnemyKind != kind)) defs.Add(Roster.Enemy(kind));
            foreach (var def in defs)
            {
                var name = BoardView.ModelName(new Unit(0, def, new Point(0, 0)));
                var m = ModelLibrary.Get(name);
                Assert.IsNotNull(m, $"{def.Name} needs Resources/Models/{name}.bytes");
                Assert.Greater(m.Mesh.vertexCount, 300, name);
                Assert.That(m.Anchors.ContainsKey("head"), $"{name} marks where its health bar goes");
                Assert.That(m.Bounds.min.y, Is.GreaterThanOrEqualTo(-0.01f), $"{name} stands on its base");
                Assert.That(m.Bounds.size.x, Is.LessThan(1.8f), $"{name} fits on a tile, give or take the boss's wings");
            }
        }

        [Test]
        public void EveryBoardPieceLoads()
        {
            foreach (var name in BoardModels())
            {
                var m = ModelLibrary.Get(name);
                Assert.IsNotNull(m, $"Resources/Models/{name}.bytes is missing or unreadable");
                Assert.Greater(m.Mesh.triangles.Length, 0, name);
                Assert.AreEqual(m.Mesh.vertexCount, m.Mesh.colors32.Length, $"{name} has a color per vertex");
            }
        }

        [Test]
        public void TileModelsFitTheirTile()
        {
            foreach (var name in BoardModels().Where(n => n.StartsWith("floor_") || n.StartsWith("wall_")))
            {
                var b = ModelLibrary.Get(name).Bounds;
                Assert.That(b.size.x, Is.LessThanOrEqualTo(1.12f), name);
                Assert.That(b.size.z, Is.LessThanOrEqualTo(1.12f), name);
                if (name.StartsWith("floor_")) Assert.That(b.max.y, Is.LessThan(0.08f), $"{name} stays flat so units stand on it");
            }
        }

        [Test]
        public void ParseRejectsOtherData()
        {
            Assert.IsNull(ModelLibrary.Parse("junk", new byte[64]));
            Assert.IsNull(ModelLibrary.Parse("short", new byte[3]));
        }

        [Test]
        public void EveryTrackLoads()
        {
            foreach (var name in new[] { "title", "crypt", "hellforge", "cathedral", "wastes", "boss", "camp" })
            {
                var clip = Resources.Load<AudioClip>("Music/" + name);
                Assert.IsNotNull(clip, $"Resources/Music/{name}.ogg is missing");
                Assert.Greater(clip.length, 45f, $"{name} is a full track");
            }
        }

        [Test]
        public void MenusPlayTheTitleTheme()
        {
            Assert.AreEqual("title", MusicPlayer.TrackFor(null));
        }
    }
}
