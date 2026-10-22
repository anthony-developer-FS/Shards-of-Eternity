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

        [Tooltip("Start in a procedurally generated room instead of the handmade prototype room.")]
        public bool useGeneratedRoom;

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
            controller.UseGeneratedRoom = useGeneratedRoom;
            controller.Seed = seed;
            controller.NewBattle();
        }
    }
}
