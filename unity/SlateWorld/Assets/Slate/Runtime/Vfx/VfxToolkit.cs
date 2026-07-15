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

        private static Material _dotAlpha, _dotAdd, _streakAlpha, _fleckAdd, _fleckAlpha;

        public static Material DotMaterial => _dotAlpha ??= Build(SoftDot(), false);          // smoke, mist, snow
        public static Material DotAdditiveMaterial => _dotAdd ??= Build(SoftDot(), true);     // flames, motes, glitter
        public static Material StreakMaterial => _streakAlpha ??= Build(Streak(), false);     // rain
        public static Material FleckMaterial => _fleckAdd ??= Build(Fleck(), true);           // embers, sparks
        public static Material FleckAlphaMaterial => _fleckAlpha ??= Build(Fleck(), false);   // locusts (dark specks)

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
            main.startSize = new ParticleSystem.MinMaxCurve(s.SizeMin, s.SizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(s.ColorA, s.ColorB);
            main.gravityModifier = s.Gravity;
            main.maxParticles = s.MaxParticles > 0 ? s.MaxParticles : 2000;
            main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);

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
