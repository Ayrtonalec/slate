// SLATE — the VFX toolkit. Every phenomenon in the world gets a particle
// voice, built entirely in code (textures included) so headless setup works.
// The founder's bar (TODO T7.VFX): each effect must be recognizable with no
// label and make a first-time viewer say "wow".
using UnityEngine;

namespace Slate.Game
{
    public static class VfxToolkit
    {
        private static Texture2D _softDot, _streak, _fleck;

        // ---------- procedural textures ----------

        private static Texture2D SoftDot()
        {
            if (_softDot != null) return _softDot;
            const int S = 48;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a); // soft shoulder
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            _softDot = t;
            return t;
        }

        private static Texture2D Streak()
        {
            if (_streak != null) return _streak;
            const int W = 8, H = 32;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float ax = 1f - Mathf.Abs((x + 0.5f) / W - 0.5f) * 2f;
                    float ay = Mathf.Sin((y + 0.5f) / H * Mathf.PI);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(ax * ay)));
                }
            t.Apply();
            _streak = t;
            return t;
        }

        private static float Hash01(int x, int y)
            => Mathf.Abs(Mathf.Sin(x * 127.1f + y * 311.7f) * 43758.5453f) % 1f;

        private static float ValueNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(
                Mathf.Lerp(Hash01(ix, iy), Hash01(ix + 1, iy), fx),
                Mathf.Lerp(Hash01(ix, iy + 1), Hash01(ix + 1, iy + 1), fx), fy);
        }

        // Billowy smoke: fractal noise inside a radial falloff, so smoke reads
        // as roiling cloud matter instead of a soft disc.
        private static Texture2D _billow;
        private static Texture2D Billow()
        {
            if (_billow != null) return _billow;
            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S - 0.5f, dy = (y + 0.5f) / S - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float falloff = Mathf.Clamp01(1f - r);
                    falloff = falloff * falloff * (3f - 2f * falloff);
                    float n = ValueNoise(x * 0.11f, y * 0.11f) * 0.6f
                            + ValueNoise(x * 0.23f, y * 0.23f) * 0.3f
                            + ValueNoise(x * 0.47f, y * 0.47f) * 0.1f;
                    float a = Mathf.Clamp01(falloff * (0.30f + 1.0f * n));
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            _billow = t;
            return t;
        }

        // A flame lick: teardrop with a near-white core cooling to deep
        // orange-red edges. Color is baked in; additive blending + bloom
        // make the core burn.
        private static Texture2D _flame;
        private static Texture2D Flame()
        {
            if (_flame != null) return _flame;
            const int W = 48, H = 64;
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = ((x + 0.5f) / W - 0.5f) * 2f;   // -1..1
                    float v = (y + 0.5f) / H;                 // 0 bottom .. 1 tip
                    float wobble = (ValueNoise(x * 0.2f, y * 0.13f) - 0.5f) * 0.35f * v;
                    float uu = u + wobble;
                    float radius = 0.62f * (1f - v * 0.68f);
                    float d = Mathf.Abs(uu) / Mathf.Max(0.05f, radius);
                    float baseFade = v < 0.12f ? v / 0.12f : 1f;
                    float tipFade = 1f - Mathf.SmoothStep(0.70f, 1f, v);
                    float a = Mathf.Clamp01(1f - d) * baseFade * tipFade;
                    a = a * a * (3f - 2f * a);
                    float heat = Mathf.Clamp01(1.3f - d * 0.8f - v * 0.5f);
                    Color c = Color.Lerp(new Color(0.80f, 0.13f, 0.02f), new Color(1f, 0.93f, 0.66f), heat * heat);
                    c.a = a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            _flame = t;
            return t;
        }

        // A tiny irregular speck, for locusts and embers.
        private static Texture2D Fleck()
        {
            if (_fleck != null) return _fleck;
            const int S = 16;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x + 0.5f) / S - 0.5f, dy = ((y + 0.5f) / S - 0.5f) * 1.6f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.4f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a > 0.15f ? 1f : 0f));
                }
            t.Apply();
            _fleck = t;
            return t;
        }

        // Build a particle material on URP's built-in unlit particle shader —
        // guaranteed to render in URP (a hand-written particle shader silently
        // failed to draw). Vertex color (the particle's start color) tints it.
        // blend: false = alpha (smoke, mist, snow, rain), true = additive
        // (fire, embers, sparks, motes — they glow).
        private static Material Build(Texture2D tex, bool additive)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default"); // last-ditch
            var m = new Material(shader);
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);                    // Transparent
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive
                ? UnityEngine.Rendering.BlendMode.One
                : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_Blend", additive ? 2f : 0f);      // 0 alpha, 2 additive
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20;
            return m;
        }

        private static Material _dotAlpha, _dotAdd, _streakAlpha, _fleckAdd, _fleckAlpha, _billowAlpha, _flameAdd;

        public static Material DotMaterial => _dotAlpha ??= Build(SoftDot(), false);          // snow, mist
        public static Material DotAdditiveMaterial => _dotAdd ??= Build(SoftDot(), true);     // motes, glitter
        public static Material StreakMaterial => _streakAlpha ??= Build(Streak(), false);     // rain
        public static Material FleckMaterial => _fleckAdd ??= Build(Fleck(), true);           // embers, sparks
        public static Material FleckAlphaMaterial => _fleckAlpha ??= Build(Fleck(), false);   // locusts (dark specks)
        public static Material BillowMaterial => _billowAlpha ??= Build(Billow(), false);     // smoke, miasma, dust
        public static Material FlameMaterial => _flameAdd ??= Build(Flame(), true);           // fire licks (color baked in)

        // ---------- system factory ----------

        public struct SystemSpec
        {
            public string Name;
            public Material Material;
            public Color ColorA, ColorB;       // start color range
            public float SizeMin, SizeMax;
            public float LifeMin, LifeMax;
            public float SpeedMin, SpeedMax;   // initial speed along emit direction
            public float Gravity;              // + falls, - rises
            public float NoiseStrength, NoiseFrequency;
            public bool Stretch;               // stretched billboards (rain)
            public float StretchLength;
            public int MaxParticles;
            public bool FadeInOut;             // alpha ramps in then out
            public float GrowOverLife;         // end size multiplier (smoke billows)
            public float TallAspect;           // >0: upright sprite, height = width * aspect (flame licks)
        }

        public static ParticleSystem Create(Transform parent, SystemSpec s)
        {
            var go = new GameObject("vfx-" + s.Name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.LifeMin, s.LifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.SpeedMin, s.SpeedMax);
            main.startColor = new ParticleSystem.MinMaxGradient(s.ColorA, s.ColorB);
            main.gravityModifier = s.Gravity;
            main.maxParticles = s.MaxParticles > 0 ? s.MaxParticles : 2000;
            if (s.TallAspect > 0f)
            {
                // Upright sprites (flame licks): taller than wide, no spin.
                main.startSize3D = true;
                main.startSizeX = new ParticleSystem.MinMaxCurve(s.SizeMin, s.SizeMax);
                main.startSizeY = new ParticleSystem.MinMaxCurve(s.SizeMin * s.TallAspect, s.SizeMax * s.TallAspect);
                main.startSizeZ = new ParticleSystem.MinMaxCurve(s.SizeMin, s.SizeMax);
                main.startRotation = 0f;
            }
            else
            {
                main.startSize = new ParticleSystem.MinMaxCurve(s.SizeMin, s.SizeMax);
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            }

            var emission = ps.emission;
            emission.rateOverTime = 0f; // everything is emitted by WorldVfx, on purpose

            var shape = ps.shape;
            shape.enabled = false;

            if (s.FadeInOut)
            {
                var col = ps.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[]
                    {
                        new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                        new GradientAlphaKey(0.85f, 0.6f), new GradientAlphaKey(0f, 1f),
                    });
                col.color = new ParticleSystem.MinMaxGradient(g);
            }

            if (s.GrowOverLife > 0f && Mathf.Abs(s.GrowOverLife - 1f) > 0.01f)
            {
                var size = ps.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f,
                    AnimationCurve.EaseInOut(0f, 1f, 1f, s.GrowOverLife));
            }

            if (s.NoiseStrength > 0f)
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = s.NoiseStrength;
                noise.frequency = s.NoiseFrequency > 0 ? s.NoiseFrequency : 0.3f;
                noise.scrollSpeed = 0.4f;
            }

            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = s.Material;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
            if (s.Stretch)
            {
                rend.renderMode = ParticleSystemRenderMode.Stretch;
                rend.lengthScale = s.StretchLength > 0 ? s.StretchLength : 6f;
            }

            ps.Play();
            return ps;
        }

        // Emit n particles at a world position with a small positional jitter.
        public static void Emit(ParticleSystem ps, Vector3 pos, int count, float jitter = 0f, Vector3 velocity = default)
        {
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 j = jitter > 0f
                    ? new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter * 0.3f, jitter * 0.3f), Random.Range(-jitter, jitter))
                    : Vector3.zero;
                ep.position = pos + j;
                if (velocity != default) ep.velocity = velocity + j * 0.2f;
                ps.Emit(ep, 1);
            }
        }
    }
}
