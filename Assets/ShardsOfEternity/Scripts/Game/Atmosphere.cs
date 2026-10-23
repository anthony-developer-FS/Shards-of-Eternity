using System.Collections.Generic;
using ShardsOfEternity.Core;
using UnityEngine;

namespace ShardsOfEternity.Game
{
    /// <summary>
    /// Everything around the board that sets the mood: the sun and ambient light, fog, a sky dome split between a
    /// golden heaven glow on one side and hellfire on the other, a rift tearing open behind the room, distant spires,
    /// floating rocks and drifting motes. Each rift theme has its own look. Between runs it shows the seven heroes on
    /// a floating island behind the menus, under a slowly drifting camera.
    /// </summary>
    public sealed class Atmosphere : MonoBehaviour
    {
        sealed class Look
        {
            public Color Sun, AmbientSky, AmbientEquator, AmbientGround, Fog;
            public float SunIntensity = 1f;
            public Vector3 SunAngles = new Vector3(52, -32, 0);
            public Color SkyTop, Horizon, Abyss, Heaven, Hell, Motes, Under;
            public float MoteRate = 14f, MoteSpeed = 0.18f;
        }

        static Color C(float r, float g, float b) => new Color(r, g, b);

        static readonly Dictionary<string, Look> Looks = new Dictionary<string, Look>
        {
            ["crypt"] = new Look
            {
                Sun = C(.62f, .72f, 1f), SunIntensity = .95f, AmbientSky = C(.24f, .27f, .36f), AmbientEquator = C(.16f, .17f, .22f),
                AmbientGround = C(.18f, .1f, .26f), Fog = C(.07f, .08f, .12f), SkyTop = C(.02f, .02f, .04f), Horizon = C(.12f, .14f, .21f),
                Abyss = C(.1f, .04f, .17f), Heaven = C(.5f, .58f, .7f), Hell = C(.16f, .34f, .28f), Motes = C(.55f, 1f, .7f),
                Under = C(.45f, .3f, 1f),
            },
            ["hellforge"] = new Look
            {
                Sun = C(1f, .58f, .38f), SunIntensity = .95f, AmbientSky = C(.3f, .17f, .13f), AmbientEquator = C(.2f, .1f, .08f),
                AmbientGround = C(.38f, .12f, .05f), Fog = C(.13f, .045f, .03f), SkyTop = C(.04f, .01f, .01f), Horizon = C(.36f, .09f, .03f),
                Abyss = C(.42f, .1f, .02f), Heaven = C(.6f, .42f, .26f), Hell = C(.95f, .28f, .05f), Motes = C(1f, .55f, .15f),
                Under = C(1f, .35f, .1f), MoteRate = 26f, MoteSpeed = .35f,
            },
            ["cathedral"] = new Look
            {
                Sun = C(.72f, .62f, 1f), SunIntensity = .85f, AmbientSky = C(.21f, .17f, .32f), AmbientEquator = C(.13f, .1f, .19f),
                AmbientGround = C(.26f, .1f, .36f), Fog = C(.08f, .05f, .13f), SkyTop = C(.02f, .01f, .05f), Horizon = C(.19f, .1f, .31f),
                Abyss = C(.26f, .08f, .42f), Heaven = C(.78f, .66f, .45f), Hell = C(.48f, .12f, .42f), Motes = C(.8f, .6f, 1f),
                Under = C(.7f, .35f, 1f),
            },
            ["wastes"] = new Look
            {
                Sun = C(1f, .84f, .62f), SunIntensity = 1.1f, AmbientSky = C(.36f, .29f, .23f), AmbientEquator = C(.25f, .2f, .15f),
                AmbientGround = C(.26f, .13f, .1f), Fog = C(.21f, .13f, .09f), SkyTop = C(.08f, .04f, .04f), Horizon = C(.56f, .26f, .13f),
                Abyss = C(.26f, .08f, .06f), Heaven = C(.96f, .8f, .5f), Hell = C(.72f, .16f, .08f), Motes = C(1f, .85f, .6f),
                Under = C(1f, .4f, .2f), MoteRate = 30f, MoteSpeed = .5f,
            },
            ["title"] = new Look
            {
                Sun = C(1f, .9f, .75f), SunIntensity = 1.05f, SunAngles = new Vector3(38, -140, 0), AmbientSky = C(.26f, .22f, .32f),
                AmbientEquator = C(.16f, .12f, .2f), AmbientGround = C(.3f, .1f, .3f), Fog = C(.1f, .06f, .14f), SkyTop = C(.03f, .02f, .06f),
                Horizon = C(.2f, .1f, .24f), Abyss = C(.3f, .06f, .3f), Heaven = C(1f, .84f, .5f), Hell = C(.75f, .1f, .22f),
                Motes = C(1f, .85f, .6f), Under = C(.75f, .3f, 1f),
            },
        };

        public static Color TorchColor(string theme)
        {
            switch (theme)
            {
                case "crypt": return C(.55f, 1f, .7f);
                case "hellforge": return C(1f, .5f, .2f);
                case "cathedral": return C(.8f, .55f, 1f);
                default: return C(1f, .7f, .35f);
            }
        }

        // The heaven side of the sky (gold) is up and to the right behind the board; hell is opposite.
        static readonly Vector3 HeavenDir = new Vector3(0.8f, 0, 0.6f).normalized;

        Transform sky, backdrop, showcase;
        Mesh skyMesh;
        Light sun, under;
        ParticleSystem motes;
        Look look;
        string shownKey;
        Vector3 boardCenter;
        float boardRadius = 8f;
        bool menu;
        readonly List<(Transform t, Vector3 home, float phase)> floaters = new List<(Transform, Vector3, float)>();

        /// <summary>The scene's directional light. GameBootstrap hands over the one it found or made.</summary>
        public Light Sun
        {
            get
            {
                if (sun != null) return sun;
                sun = new GameObject("Directional Light").AddComponent<Light>();
                sun.type = LightType.Directional;
                return sun;
            }
            set => sun = value;
        }

        void Awake()
        {
            sky = new GameObject("Sky").transform;
            sky.SetParent(transform, false);
            sky.gameObject.AddComponent<MeshFilter>();
            var mr = sky.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Palette.Sky;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            under = new GameObject("Rift glow").AddComponent<Light>();
            under.transform.SetParent(transform, false);
            under.type = LightType.Point;
            under.shadows = LightShadows.None;
        }

        /// <summary>Dresses the scene for a battle on this board.</summary>
        public void ShowBoard(Battle battle, string themeKey)
        {
            menu = false;
            if (showcase != null) showcase.gameObject.SetActive(false);
            boardCenter = new Vector3((battle.Width - 1) / 2f, 0, -(battle.Height - 1) / 2f);
            boardRadius = Mathf.Max(battle.Width, battle.Height) * 0.6f + 4f;
            Apply(themeKey);
            BuildBackdrop(themeKey, battle.Width, battle.Height);
        }

        /// <summary>The scene behind the menus: the heroes on a floating island.</summary>
        public void ShowMenu()
        {
            menu = true;
            boardCenter = Vector3.zero;
            boardRadius = 6f;
            Apply("title");
            BuildBackdrop("title", 3, 3);
            BuildShowcase();
            showcase.gameObject.SetActive(true);
        }

        void Apply(string key)
        {
            look = Looks.TryGetValue(key, out var l) ? l : Looks["crypt"];
            var s = Sun;
            s.color = look.Sun;
            s.intensity = look.SunIntensity;
            s.shadows = LightShadows.Soft;
            s.shadowStrength = 0.75f;
            s.transform.rotation = Quaternion.Euler(look.SunAngles);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = look.AmbientSky;
            RenderSettings.ambientEquatorColor = look.AmbientEquator;
            RenderSettings.ambientGroundColor = look.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = look.Fog;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = look.Fog;
                cam.farClipPlane = Mathf.Max(cam.farClipPlane, 300f);
            }
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 6);

            under.color = look.Under;
            under.range = boardRadius * 1.6f;
            under.intensity = 1.4f;
            under.transform.localPosition = boardCenter + Vector3.down * 3f;

            if (shownKey != key)
            {
                shownKey = key;
                BuildSky();
            }
            BuildMotes(key);
        }

        // ---------- sky ----------

        void BuildSky()
        {
            const int seg = 40, rings = 20;
            const float radius = 120f;
            var verts = new List<Vector3>();
            var colors = new List<Color>();
            var tris = new List<int>();
            for (int i = 0; i <= rings; i++)
            {
                float v = (float)i / rings;
                float elev = Mathf.Lerp(-Mathf.PI / 2, Mathf.PI / 2, v);
                for (int j = 0; j <= seg; j++)
                {
                    float az = 2 * Mathf.PI * j / seg;
                    var d = new Vector3(Mathf.Cos(elev) * Mathf.Cos(az), Mathf.Sin(elev), Mathf.Cos(elev) * Mathf.Sin(az));
                    verts.Add(d * radius);
                    colors.Add(SkyColor(d));
                }
            }
            for (int i = 0; i < rings; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = i * (seg + 1) + j, b = a + seg + 1;
                    tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            if (skyMesh == null) skyMesh = new Mesh { name = "Sky dome" };
            skyMesh.Clear();
            skyMesh.SetVertices(verts);
            skyMesh.SetColors(colors);
            skyMesh.SetTriangles(tris, 0);
            skyMesh.RecalculateBounds();
            sky.GetComponent<MeshFilter>().sharedMesh = skyMesh;
        }

        Color SkyColor(Vector3 d)
        {
            float e = d.y;
            var c = e >= 0
                ? Color.Lerp(look.Horizon, look.SkyTop, Mathf.SmoothStep(0, 1, Mathf.Clamp01(e / 0.55f)))
                : Color.Lerp(look.Horizon, look.Abyss, Mathf.SmoothStep(0, 1, Mathf.Clamp01(-e / 0.35f)));
            var flat = new Vector3(d.x, 0, d.z).normalized;
            float side = Vector3.Dot(flat, HeavenDir);
            float band = Mathf.Exp(-Mathf.Abs(e - 0.08f) * 5f);
            c += look.Heaven * Mathf.Pow(Mathf.Max(0, side), 3f) * band * 0.75f;
            c += look.Hell * Mathf.Pow(Mathf.Max(0, -side), 2f) * Mathf.Exp(-Mathf.Abs(e) * 4f) * 0.6f;
            // Deep below, the abyss glows with rift light.
            c += look.Under * Mathf.Pow(Mathf.Clamp01(-e), 3f) * 0.25f;
            c.a = 1;
            return c;
        }

        // ---------- backdrop ----------

        void BuildBackdrop(string key, int w, int h)
        {
            if (backdrop != null) Destroy(backdrop.gameObject);
            floaters.Clear();
            backdrop = new GameObject("Backdrop").transform;
            backdrop.SetParent(transform, false);
            var rng = new System.Random(key.GetHashCode() ^ (w * 31 + h));
            float R = boardRadius;
            var c = boardCenter;

            var tear = Spawn("rift_tear", c + new Vector3(0, 7f, R + 9f), 0, 2.4f);
            if (tear != null) tear.localRotation = Quaternion.Euler(0, 0, -8);
            Spawn("spire_dark", c + new Vector3(-R - 6f, -5f, R + 4f), 30, 1.7f);
            Spawn("spire_dark", c + new Vector3(-R - 14f, -7f, R + 16f), 70, 2.2f);
            Spawn("spire_light", c + new Vector3(R + 8f, -4f, R + 7f), -20, 1.8f);
            Spawn("spire_light", c + new Vector3(R + 18f, -6f, R + 20f), 10, 2.4f);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)(i / 9.0 * Mathf.PI * 2 + rng.NextDouble() * 0.5);
                float dist = R * (0.85f + (float)rng.NextDouble() * 0.6f);
                var pos = c + new Vector3(Mathf.Cos(a) * dist, -1.5f - (float)rng.NextDouble() * 3.5f, Mathf.Sin(a) * dist * 0.9f);
                // Keep the space in front of the camera clear.
                if (pos.z < c.z - R * 0.6f) pos.y -= 3f;
                var t = Spawn($"float_rock_{i % 3}", pos, (float)rng.NextDouble() * 360, 0.6f + (float)rng.NextDouble() * 1.1f);
                if (t != null) floaters.Add((t, pos, (float)rng.NextDouble() * 6f));
            }
        }

        Transform Spawn(string model, Vector3 pos, float yaw, float scale)
        {
            var t = ModelLibrary.Spawn(model, backdrop);
            if (t == null) return null;
            t.localPosition = pos;
            t.localRotation = Quaternion.Euler(0, yaw, 0);
            t.localScale = Vector3.one * scale;
            var r = t.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        void BuildMotes(string key)
        {
            if (motes == null)
            {
                var go = new GameObject("Motes");
                go.transform.SetParent(transform, false);
                go.SetActive(false);
                motes = go.AddComponent<ParticleSystem>();
                var r = go.GetComponent<ParticleSystemRenderer>();
                r.sharedMaterial = Palette.Overlay(Color.white);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                go.SetActive(true);
            }
            motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = motes.main;
            main.loop = true;
            main.startLifetime = 7f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = look.Motes;
            main.maxParticles = 300;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = motes.emission;
            emission.rateOverTime = look.MoteRate;
            var shape = motes.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(boardRadius * 1.6f, 0.2f, boardRadius * 1.6f);
            motes.transform.localPosition = boardCenter + Vector3.down * 0.5f;
            var vel = motes.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            vel.y = new ParticleSystem.MinMaxCurve(look.MoteSpeed * 0.5f, look.MoteSpeed);
            vel.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            var col = motes.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.9f, 0.2f), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = grad;
            motes.Play();
        }

        // ---------- the menu showcase ----------

        static readonly string[] ShowcaseHeroes = { "hunter", "rogue", "barbarian", "paladin", "priest", "sorcerer", "shaman" };

        void BuildShowcase()
        {
            if (showcase != null) return;
            showcase = new GameObject("Showcase").transform;
            showcase.SetParent(transform, false);
            for (int x = -3; x <= 3; x++)
                for (int z = -1; z <= 1; z++)
                {
                    if (Mathf.Abs(x) == 3 && z != 0) continue;
                    int h = (x * 7 + z * 13 + 40) % 4;
                    var p = new Vector3(x, 0, z);
                    var floor = ModelLibrary.Spawn($"floor_cathedral_{h}", showcase);
                    if (floor != null) floor.localPosition = p;
                    var rock = ModelLibrary.Spawn($"rock_cathedral_{(Mathf.Abs(x) == 3 || z != 0 ? 1 : 0)}", showcase);
                    if (rock != null)
                    {
                        rock.localPosition = p;
                        rock.localRotation = Quaternion.Euler(0, 90 * h, 0);
                    }
                }
            var pillarL = ModelLibrary.Spawn("pillar_cathedral", showcase);
            if (pillarL != null) pillarL.localPosition = new Vector3(-3, 0, 0);
            var pillarR = ModelLibrary.Spawn("pillar_crypt", showcase);
            if (pillarR != null) pillarR.localPosition = new Vector3(3, 0, 0);
            var door = ModelLibrary.Spawn("exit_closed", showcase);
            if (door != null)
            {
                door.localPosition = new Vector3(0, 0, 1.1f);
                door.localScale = Vector3.one * 1.6f;
            }
            for (int i = 0; i < ShowcaseHeroes.Length; i++)
            {
                float t = i - (ShowcaseHeroes.Length - 1) / 2f;
                var hero = ModelLibrary.Spawn("hero_" + ShowcaseHeroes[i], showcase);
                if (hero == null) continue;
                hero.localPosition = new Vector3(t * 0.78f, 0, -Mathf.Abs(t) * 0.18f + 0.1f);
                hero.localRotation = Quaternion.Euler(0, -t * 9f, 0);
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            sky.position = cam.transform.position;

            if (menu)
            {
                // A slow drift around the heroes.
                float yaw = Mathf.Sin(Time.unscaledTime * 0.12f) * 14f;
                // Looking a little above the heroes puts them in the lower half, under the title and buttons.
                var rot = Quaternion.Euler(13f, yaw, 0);
                var focus = new Vector3(0, 1.2f, 0);
                cam.fieldOfView = 38f;
                cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * 6.6f, rot);
            }

            float d = Vector3.Distance(cam.transform.position, boardCenter);
            RenderSettings.fogStartDistance = d + boardRadius * 0.4f;
            RenderSettings.fogEndDistance = d + boardRadius * 4f + 20f;

            float time = Time.time;
            foreach (var (t, home, phase) in floaters)
            {
                if (t == null) continue;
                t.localPosition = home + Vector3.up * (Mathf.Sin(time * 0.4f + phase) * 0.25f);
                t.localRotation = Quaternion.Euler(0, phase * 60f + time * 3f, 0);
            }
        }
    }
}
