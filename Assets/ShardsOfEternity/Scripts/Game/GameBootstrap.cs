using System.Collections.Generic;
using ShardsOfEternity.Core;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Entry point for the combat prototype. Put it on one GameObject in an otherwise empty scene and press Play:
    /// it creates the camera, light, board, battle loop and HUD at runtime, so no prefabs or art are needed yet.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Hero ids: kael, bram, nyra, ilse, wren, tova. Pick 4.")]
        public List<string> squad = new List<string>(Roster.DefaultSquad);

        [Tooltip("Dungeons have 3-4 floors, raids have 12. The last floor has the boss.")]
        public RunKind runKind = RunKind.Dungeon;

        [Tooltip("Use the browser prototype's handmade room for floor 1.")]
        public bool handmadeFirstFloor;

        [Tooltip("Experimental: 2 action shards per turn, spent on any mix of moves, attacks and abilities.")]
        public bool useActionShards;

        [Tooltip("With action shards, keep unspent shards for later turns, up to 4.")]
        public bool bankShards;

        [Tooltip("Seconds of unpaused play before each reinforcement wave on a floor, once the room is awake. The last entry repeats.")]
        public float[] reinforcementDelays = { 600f, 300f, 120f };
        [Tooltip("Testing aid: a wave every 2 minutes from the start, ignoring the schedule above.")]
        public bool fastTestTimer;

        [Tooltip("Room and crit seed. 0 picks a random one each time.")]
        public int seed;

        void Start()
        {
            Application.targetFrameRate = 60;

            var cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
                cam.gameObject.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Hex(0x0E0F14);

            if (FindAnyObjectByType<Light>() == null)
            {
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(55, -30, 0);
                light.intensity = 1.1f;
            }
            RenderSettings.ambientLight = new Color(.45f, .45f, .5f);

            var board = new GameObject("Board").AddComponent<BoardView>();
            var controller = gameObject.AddComponent<BattleController>();
            var hud = gameObject.AddComponent<BattleHud>();

            board.Hud = hud;
            hud.Controller = controller;
            controller.Camera = cam;
            controller.Board = board;
            controller.Hud = hud;
            controller.Squad = new List<string>(squad);
            controller.RunKind = runKind;
            controller.HandmadeFirstFloor = handmadeFirstFloor;
            controller.UseActionShards = useActionShards;
            controller.BankShards = bankShards;
            controller.ReinforcementDelays = reinforcementDelays;
            controller.FastTestTimer = fastTestTimer;
            controller.Seed = seed;
            controller.NewRun();
        }
    }
}
