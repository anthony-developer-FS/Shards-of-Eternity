using System.Collections.Generic;
using ShardsOfEternity.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Placeholder look for the prototype: flat colored primitives, no imported art.
    /// Materials are created at runtime and cached per color.
    /// </summary>
    public static class Palette
    {
        static readonly Dictionary<Color32, Material> cache = new Dictionary<Color32, Material>();
        static Shader shader;

        public static readonly Color Move = Hex(0x4FA3F7);
        public static readonly Color Charge = Hex(0xF0A13C);
        public static readonly Color Swap = Hex(0x3FC1B0);
        public static readonly Color AttackTarget = Hex(0xE5484D);
        public static readonly Color AbilityTarget = Hex(0xB07CF0);
        public static readonly Color Area = Hex(0xF5D76E);
        public static readonly Color Spikes = Hex(0x6E2A2A);
        public static readonly Color ExitClosed = Hex(0x5B3F8C);
        public static readonly Color ExitOpen = Hex(0x5CD18A);
        public static readonly Color ActiveRing = Hex(0xFFE08A);
        public static readonly Color Heal = Hex(0x6EE7A0);
        public static readonly Color Crit = Hex(0xFFD54A);
        public static readonly Color Pillar = Hex(0x6B6F7D);

        public static Color Hex(uint rgb) =>
            new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);

        public static Color ForUnit(Unit u)
        {
            if (u.IsEnemy)
            {
                switch (u.Def.EnemyKind)
                {
                    case EnemyKind.Ghoul: return Hex(0x8FA36B);
                    case EnemyKind.Imp: return Hex(0xE0663A);
                    default: return Hex(0x8C2F2F);
                }
            }
            switch (u.Def.HeroClass)
            {
                case HeroClass.Barbarian: return Hex(0xD9534F);
                case HeroClass.Paladin: return Hex(0xF0C75E);
                case HeroClass.Rogue: return Hex(0x9B6BDF);
                case HeroClass.Sorcerer: return Hex(0x4FA3F7);
                case HeroClass.Hunter: return Hex(0x5CB85C);
                default: return Hex(0x3FC1B0);
            }
        }

        public static Material Mat(Color color)
        {
            Color32 key = color;
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(FindShader()) { color = color };
            cache[key] = m;
            return m;
        }

        static Shader FindShader()
        {
            if (shader != null) return shader;
            // URP if a render pipeline is active, otherwise the built-in Standard shader.
            if (GraphicsSettings.currentRenderPipeline != null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            return shader;
        }
    }
}
