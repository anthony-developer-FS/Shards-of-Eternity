using System.Collections.Generic;
using ShardsOfEternity.Core;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Plays the soundtrack from Resources/Music and crossfades between tracks as the game moves between the menus,
    /// each rift theme, the boss floor and camp. The tracks are composed and rendered from code by Tools/MusicGen.
    /// The volume is a setting and is remembered between sessions.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        const string VolumeKey = "soe.music";
        const float FadeTime = 2.5f;

        public static readonly float[] Volumes = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        public static float Volume { get; private set; } = 0.6f;

        public static void SetVolume(float value)
        {
            Volume = value;
            PlayerPrefs.SetFloat(VolumeKey, value);
        }

        /// <summary>The track playing now (or fading in), by file name.</summary>
        public string Current { get; private set; }

        AudioSource playing, fading;
        float blend = 1f;
        BattleController controller;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        void Awake()
        {
            Volume = PlayerPrefs.GetFloat(VolumeKey, 0.6f);
            playing = MakeSource();
            fading = MakeSource();
        }

        AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.ignoreListenerPause = true;
            s.volume = 0f;
            return s;
        }

        AudioClip Clip(string name)
        {
            if (clips.TryGetValue(name, out var c)) return c;
            c = Resources.Load<AudioClip>("Music/" + name);
            clips[name] = c;
            return c;
        }

        /// <summary>The track that fits what is on screen.</summary>
        public static string TrackFor(BattleController controller)
        {
            var battle = controller != null ? controller.Battle : null;
            if (battle == null) return "title";
            if (controller.InCamp) return "camp";
            foreach (var u in battle.Units)
                if (u.Alive && u.IsEnemy && u.Def.EnemyKind == EnemyKind.RiftLord) return "boss";
            return BoardView.ThemeKey(battle.Layout.Theme);
        }

        void Update()
        {
            if (controller == null) controller = GetComponent<BattleController>();
            var want = TrackFor(controller);
            if (want != Current)
            {
                var clip = Clip(want);
                Current = want;
                // Swap sources: the old track fades out while the new one fades in from the top.
                var old = playing;
                playing = fading;
                fading = old;
                playing.clip = clip;
                playing.volume = 0f;
                if (clip != null) playing.Play();
                else playing.Stop();
                blend = 0f;
            }

            blend = Mathf.MoveTowards(blend, 1f, Time.unscaledDeltaTime / FadeTime);
            float k = Mathf.SmoothStep(0, 1, blend);
            playing.volume = Volume * k;
            fading.volume = Volume * (1 - k);
            if (blend >= 1f && fading.isPlaying) fading.Stop();
        }
    }
}
