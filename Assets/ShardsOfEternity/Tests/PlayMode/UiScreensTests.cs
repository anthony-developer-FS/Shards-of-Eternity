using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShardsOfEternity.Core;
using ShardsOfEternity.Game;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShardsOfEternity.Tests
{
    /// <summary>
    /// Plays through every screen of the game (title, squad, battle, pause, settings, camp, end of run) in landscape
    /// and portrait, and saves a screenshot of each to Logs/Screenshots (or $SOE_SCREENSHOTS). Any error logged
    /// along the way fails the test.
    /// </summary>
    public class UiScreensTests
    {
        GameObject game;
        string saveFolder;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Never touch the player's real save.
            saveFolder = Path.Combine(Path.GetTempPath(), "soe-test-" + Guid.NewGuid().ToString("N"));
            ProfileStore.Folder = saveFolder;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            ProfileStore.Folder = null;
            if (Directory.Exists(saveFolder)) Directory.Delete(saveFolder, true);
            if (game != null) Object.Destroy(game);
            foreach (var cam in Object.FindObjectsByType<Camera>()) Object.Destroy(cam.gameObject);
            foreach (var light in Object.FindObjectsByType<Light>()) Object.Destroy(light.gameObject);
            foreach (var board in Object.FindObjectsByType<BoardView>()) Object.Destroy(board.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryScreen_Landscape() => EveryScreen(1600, 900, "landscape");

        [UnityTest]
        public IEnumerator EveryScreen_Portrait() => EveryScreen(900, 1800, "portrait");

        IEnumerator EveryScreen(int width, int height, string tag)
        {
            game = new GameObject("Shards of Eternity");
            var bootstrap = game.AddComponent<GameBootstrap>();
            bootstrap.seed = 11;
            yield return null;
            var hud = game.GetComponent<BattleHud>();
            var controller = game.GetComponent<BattleController>();
            Assert.IsNotNull(hud, "The bootstrap adds the UI");
            Assert.AreEqual(BattleHud.Menu.Title, hud.CurrentMenu, "The game opens on the title screen");

            // A profile with some history: levels, gear on Kael and a few spares in the vault.
            var profile = controller.Profile;
            profile.Hero("kael").AddXp(450);
            profile.Hero("sera").AddXp(120);
            var loot = new System.Random(8);
            for (int i = 0; i < 5; i++) profile.Vault.Add(LootTable.RollItem(loot, 3, (Rarity)(i % 4)));
            profile.Equip("kael", profile.Vault[1]);
            profile.Equip("kael", profile.Vault[2]);
            profile.RunsPlayed = 3;
            profile.RunsWon = 1;
            profile.DeepestFloor = 4;
            profile.EnemiesSlain = 37;

            var shooter = new Shooter(hud, controller.Camera, width, height, tag);
            yield return shooter.Take("01-title");

            hud.ShowArmory();
            Assert.AreEqual(BattleHud.Menu.Armory, hud.CurrentMenu);
            yield return shooter.Take("01b-armory");
            hud.ShowTitle();

            hud.OpenSettings(true);
            yield return shooter.Take("02-title-settings");
            hud.OpenSettings(false);

            controller.Squad.Clear();
            controller.Squad.AddRange(new[] { "kael", "sera", "nyra", "ilse" });
            controller.UseActionShards = true;
            hud.ShowSquad();
            yield return shooter.Take("03-squad");

            hud.StartRun();
            Assert.AreEqual(BattleHud.Menu.None, hud.CurrentMenu);
            Assert.IsNotNull(controller.Battle);
            yield return new WaitForSecondsRealtime(1.5f);
            hud.AddFloater(Vector3.zero, "-12", Palette.Crit, true);
            yield return shooter.Take("04-battle");
            yield return new WaitForSecondsRealtime(1.2f);

            controller.SetPaused(true);
            yield return shooter.Take("05-pause");
            hud.OpenSettings(true);
            yield return shooter.Take("06-pause-settings");
            hud.OpenSettings(false);
            controller.SetPaused(false);

            var rng = new System.Random(3);
            for (int i = 0; i < 6; i++) controller.Run.Battle.PickedUp.Add(LootTable.RollItem(rng, 2, (Rarity)(i % 4)));
            controller.MakeCampNow();
            controller.Run.Equip("kael", controller.Run.Stash.First());
            yield return shooter.Take("07-camp");

            controller.LeaveCamp();
            Assert.IsFalse(controller.InCamp);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(2, controller.Run.Floor);

            // Slay a couple of enemies first so the end screen has XP to show.
            foreach (var e in controller.Battle.Units.Where(u => u.IsEnemy && u.Alive).Take(2).ToList()) controller.Battle.Hurt(e, 999, false);
            yield return null;
            foreach (var h in controller.Battle.Heroes.ToList()) controller.Battle.Hurt(h, 999, false);
            Assert.AreEqual(BattleOutcome.Defeat, controller.Battle.Outcome);
            yield return shooter.Take("08-game-over");
            Assert.IsNotNull(controller.LastResult, "Losing pays the run out");
            Assert.IsTrue(File.Exists(ProfileStore.FilePath), "and saves it");
            Assert.AreEqual(4, controller.Profile.RunsPlayed);
            Assert.Greater(controller.LastResult.Xp, 0, "Kills on a lost run still pay XP");
            Assert.AreEqual(controller.Profile.ToJson(), ProfileStore.Load().ToJson(), "What's on disk matches the profile");

            hud.AbandonRun();
            Assert.AreEqual(BattleHud.Menu.Title, hud.CurrentMenu);
            Assert.IsNull(controller.Battle);
            yield return shooter.Take("09-back-to-title");
            shooter.Release();
        }

        /// <summary>
        /// Renders the camera and the UI into their own render textures and blends them into one PNG, which works in
        /// batch mode where there is no screen to capture.
        /// </summary>
        sealed class Shooter
        {
            readonly BattleHud hud;
            readonly Camera cam;
            readonly RenderTexture camTarget, uiTarget;
            readonly string folder, tag;
            readonly int width, height;

            public Shooter(BattleHud hud, Camera cam, int width, int height, string tag)
            {
                this.hud = hud;
                this.cam = cam;
                this.width = width;
                this.height = height;
                this.tag = tag;
                // The camera keeps a full-size target between shots so its aspect matches the screen being simulated.
                camTarget = new RenderTexture(width, height, 24);
                // 24 bits gives the UI a stencil buffer for its clipping masks.
                uiTarget = new RenderTexture(width, height, 24);
                cam.targetTexture = camTarget;
                hud.PanelSettings.targetTexture = uiTarget;
                hud.PanelSettings.clearColor = true;
                hud.PanelSettings.colorClearValue = new Color(0, 0, 0, 0);
                string env = Environment.GetEnvironmentVariable("SOE_SCREENSHOTS");
                folder = !string.IsNullOrEmpty(env) ? env : Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "Screenshots"));
                Directory.CreateDirectory(folder);
            }

            public IEnumerator Take(string name)
            {
                // A few frames for layout, the camera fit and any animation to settle.
                for (int i = 0; i < 4; i++) yield return null;
                var scene = RenderScene();
                var ui = Read(uiTarget);
                var output = new Texture2D(width, height, TextureFormat.RGB24, false);
                var a = scene.GetPixels32();
                var b = ui.GetPixels32();
                var c = new Color32[a.Length];
                for (int i = 0; i < a.Length; i++)
                {
                    float t = b[i].a / 255f;
                    c[i] = new Color32((byte)(a[i].r + (b[i].r - a[i].r) * t), (byte)(a[i].g + (b[i].g - a[i].g) * t),
                        (byte)(a[i].b + (b[i].b - a[i].b) * t), 255);
                }
                output.SetPixels32(c);
                output.Apply();
                string path = Path.Combine(folder, $"{tag}-{name}.png");
                File.WriteAllBytes(path, output.EncodeToPNG());
                Debug.Log($"Screenshot: {path}");
                Object.Destroy(scene);
                Object.Destroy(ui);
                Object.Destroy(output);
            }

            /// <summary>
            /// The 3D view as it appears on screen: the camera renders only into its viewport rect (the area the HUD
            /// leaves free), with the background color around it. A manual render into a texture ignores the rect, so
            /// render the rect's area into its own texture and place it.
            /// </summary>
            Texture2D RenderScene()
            {
                var rect = cam.rect;
                int x = Mathf.RoundToInt(rect.x * width), y = Mathf.RoundToInt(rect.y * height);
                int w = Mathf.Max(1, Mathf.RoundToInt(rect.width * width)), h = Mathf.Max(1, Mathf.RoundToInt(rect.height * height));
                var view = RenderTexture.GetTemporary(w, h, 24);
                cam.targetTexture = view;
                cam.rect = new Rect(0, 0, 1, 1);
                cam.Render();
                var part = Read(view);
                cam.targetTexture = camTarget;
                cam.rect = rect;
                RenderTexture.ReleaseTemporary(view);

                var full = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var fill = new Color32[width * height];
                Color32 background = cam.backgroundColor;
                for (int i = 0; i < fill.Length; i++) fill[i] = background;
                full.SetPixels32(fill);
                full.SetPixels(x, y, Mathf.Min(w, width - x), Mathf.Min(h, height - y), part.GetPixels(0, 0, Mathf.Min(w, width - x), Mathf.Min(h, height - y)));
                full.Apply();
                Object.Destroy(part);
                return full;
            }

            static Texture2D Read(RenderTexture rt)
            {
                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;
                return tex;
            }

            public void Release()
            {
                cam.targetTexture = null;
                hud.PanelSettings.targetTexture = null;
                camTarget.Release();
                uiTarget.Release();
            }
        }
    }
}
