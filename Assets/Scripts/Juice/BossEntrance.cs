using System;
using System.Collections;
using System.Collections.Generic;
using MagicTouch.Gameplay;
using UnityEngine;

namespace MagicTouch.Juice
{
    /// <summary>
    /// Ember Drake arrival, as one timeline: an infernal cocoon falls and hits the floor, cracks and bursts in a wind burst;
    /// the boss flaps up out of it to its hover spot between two ground fire pillars; the boss roars (sonic ring, ground
    /// quake); everything settles. Kept deliberately sparse so the cocoon and the boss stay readable.
    /// <see cref="Defeat"/> drops the dying boss from its hover spot to the player's floor.
    /// Every effect sprite comes from the boss's animation set (keys <c>fx_*</c>), is built once and ticked here, so
    /// pausing freezes it. Lives on the boss root. <see cref="Tagged"/> reports the event tags of the FX sheet JSON.
    /// </summary>
    public sealed class BossEntrance : MonoBehaviour, IBossEntrance
    {
        private const string SfxVolumeKey = "SFX_Volume";
        private const int PillarCount = 2;
        private const float CellUnits = 5.12f;       // a 512 px cell at scale 1 (100 pixels per unit)

        [Header("Cocoon")]
        [Tooltip("Cocoon size as a multiple of the boss scale.")]
        [SerializeField, Min(.1f)] private float _cocoonScale = 1f;
        [Tooltip("How far above the floor the falling cocoon starts, in world units at boss scale 1.5.")]
        [SerializeField, Min(0f)] private float _fallHeight = 3.2f;
#pragma warning disable CS0414 // kept so saved scenes keep their values
        [Tooltip("Unused: the cocoon base now sits exactly on the player's feet line.")]
        [SerializeField, Min(0f)] private float _groundLift = .3f;

        [Header("Boss rises out of the cocoon")]
        [SerializeField, Min(.2f)] private float _riseSeconds = 1.7f;
        [Tooltip("Boss size when it leaves the cocoon, as a fraction of its final size.")]
        [SerializeField, Range(.2f, 1f)] private float _riseStartScale = .55f;

        [Header("Arena effects (multiples of the boss scale)")]
        [Tooltip("Unused: the fire tornadoes were cut from the entrance.")]
        [SerializeField, Min(.1f)] private float _tornadoScale = .5f;
#pragma warning restore CS0414
        [SerializeField, Min(.1f)] private float _pillarScale = .6f;
        [SerializeField, Min(.1f)] private float _windScale = .8f;
        [Tooltip("Roar origin relative to the boss centre, in world units at boss scale 1 (head thrown back, so above the centre).")]
        [SerializeField] private Vector2 _mouthOffset = new Vector2(0f, .9f);
        [SerializeField, Min(.1f)] private float _roarWaveWidth = 1.1f;

        // Clip keys inside the boss's animation set (built from ember_drake_tyrant_clips.json).
        private const string FallKey = "fx_cocoon_fall", BreakKey = "fx_cocoon_break", ImpactKey = "fx_impact", WindKey = "fx_wind",
            PillarKey = "fx_pillar", WaveKey = "fx_roar_wave", QuakeKey = "fx_quake", AuraKey = "fx_roar_aura";

        private struct Timed { public float t; public Action a; }
        private struct Tween { public float t0, t1; public Action<float> a; public bool done; }

        private readonly List<BossFxSprite> _all = new List<BossFxSprite>();
        private readonly List<Timed> _events = new List<Timed>();
        private readonly List<Tween> _tweens = new List<Tween>();
        private BossFxSprite _cocoon, _impact, _wind, _wave, _quake, _aura;
        private BossFxSprite[] _pillars;
        private AudioSource _audio;
        private BossVisual _visual;
        private Vector3 _restPos, _restScale;
        private bool _hasRest;

        /// <summary>Raised with the event tags of the entrance JSON (cocoon_impact, boss_reveal, roar_peak, entrance_complete ...).</summary>
        public event Action<string> Tagged;

        private void EnsureObjects()
        {
            if (_cocoon != null) return;
            _cocoon = Make("Cocoon", 1);
            _impact = Make("CocoonImpact", 0);
            _quake = Make("RoarQuake", 0);
            _wind = Make("DemonicWind", 3);
            _wave = Make("RoarWave", 4);
            _aura = Make("RoarMouthAura", 4);
            _pillars = MakeMany("GroundFirePillar", PillarCount, 1);
            var go = new GameObject("RoarAudio", typeof(AudioSource), typeof(AudioEchoFilter), typeof(AudioReverbFilter));
            go.transform.SetParent(transform, false);
            _audio = go.GetComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            var echo = go.GetComponent<AudioEchoFilter>();
            echo.delay = 320f;
            echo.decayRatio = .55f;
            echo.wetMix = .6f;
            echo.dryMix = 1f;
            go.GetComponent<AudioReverbFilter>().reverbPreset = AudioReverbPreset.Cave;
        }

        private BossFxSprite Make(string name, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BossFxSprite));
            go.transform.SetParent(transform, false);
            go.GetComponent<SpriteRenderer>().sortingOrder = order;
            go.SetActive(false);
            var fx = go.GetComponent<BossFxSprite>();
            _all.Add(fx);
            return fx;
        }

        private BossFxSprite[] MakeMany(string name, int count, int order)
        {
            var result = new BossFxSprite[count];
            for (int i = 0; i < count; i++) result[i] = Make($"{name}_{i}", order);
            return result;
        }

        public void Prepare(BossDefinition def, BossVisual visual, Vector3 hover, Bounds player) { }

        public void SetRise(float t) { }

        public void Cancel()
        {
            StopAllCoroutines();
            foreach (var fx in _all) fx.Stop();
            if (_audio != null) _audio.Stop();
            RestoreBoss();
        }

        private void RestoreBoss()
        {
            if (_visual == null || !_hasRest) return;
            _visual.transform.position = _restPos;
            _visual.transform.localScale = _restScale;
            _visual.SetAlpha(1f);
            _hasRest = false;
        }

        private static BossClip Slice(BossClip src, int from, int count, bool loop, float timeScale = 1f)
        {
            if (src == null) return null;
            count = Mathf.Min(count, src.Length - from);
            var clip = new BossClip { key = src.key, loop = loop, frames = new Sprite[count], seconds = new float[count] };
            for (int i = 0; i < count; i++)
            {
                clip.frames[i] = src.frames[from + i];
                clip.seconds[i] = src.SecondsAt(from + i) * timeScale;
            }
            return clip;
        }

        private void At(float t, Action a) => _events.Add(new Timed { t = t, a = a });
        private void Over(float t0, float t1, Action<float> a) => _tweens.Add(new Tween { t0 = t0, t1 = t1, a = a });

        private void Tag(string tag)
        {
            switch (tag)
            {
                case "camera_shake_heavy": ScreenHitFeedback.Shake(.32f, .6f); break;
                case "cocoon_crack": ScreenHitFeedback.Shake(.05f, .2f); break;
                case "wind_burst": ScreenHitFeedback.Shake(.2f, .35f); break;
                case "roar_peak": ScreenHitFeedback.Shake(.16f, 1f); break;
                case "ground_quake": ScreenHitFeedback.Shake(.12f, .6f); break;
            }
            Tagged?.Invoke(tag);
        }

        /// <summary>Plays the whole arrival; the boss ends exactly on hover, resting in its idle clip.</summary>
        /// <param name="player">The player's world bounds: its bottom is the floor, and the effects keep clear of it.</param>
        public IEnumerator Run(BossDefinition def, BossVisual visual, Vector3 hover, Bounds player, Func<bool> isPlaying)
        {
            EnsureObjects();
            var anim = def.Animations;
            _visual = visual;
            var body = visual.transform;
            transform.position = hover;
            _restPos = body.position;
            _restScale = body.localScale;
            _hasRest = true;
            visual.ResetVisual(def.Segments[0].restClip);
            visual.SetAlpha(0f);
            visual.SetVisible(false);

            if (anim.Get(FallKey) == null || anim.Get(BreakKey) == null)
            {
                yield return SimpleAppear(def, visual, isPlaying);
                yield break;
            }

            // ---- geometry -------------------------------------------------------------------------------------
            var cam = Camera.main;
            float left = -2.8f, right = 2.8f;
            if (cam != null)
            {
                float depth = Mathf.Abs(cam.transform.position.z);
                left = cam.ViewportToWorldPoint(new Vector3(0f, 0f, depth)).x;
                right = cam.ViewportToWorldPoint(new Vector3(1f, 0f, depth)).x;
            }
            float halfW = (right - left) * .5f;
            float cx = hover.x;
            float ground = player.min.y; // the cocoon's base is at its pivot: it rests right on the player's feet line
            float S = def.Scale;
            float cs = S * _cocoonScale;
            // The shell art stops a few pixels above its pivot: sink it a hair so it rests on the floor, not above it.
            Vector3 groundPoint = new Vector3(cx, ground - .1f, 0f);
            Vector3 mouth = _restPos + (Vector3)(_mouthOffset * S);

            // ---- clips ----------------------------------------------------------------------------------------
            var fall = Slice(anim.Get(FallKey), 0, 8, false);
            float fallPart = 0f;
            for (int i = 0; i < 5; i++) { fall.seconds[i] *= 1.5f; fallPart += fall.seconds[i]; }   // frames 0..4: the fall, a little slower
            var brk = Slice(anim.Get(BreakKey), 0, 8, false);
            float crackStep = 0f;
            for (int i = 0; i < 5; i++) { brk.seconds[i] *= 1.6f; crackStep += brk.seconds[i]; }    // frames 0..4: crack and swell
            brk.seconds[7] = .6f;                                                                   // the last frame lingers, then fades
            var wind = anim.Get(WindKey);
            var pillar = anim.Get(PillarKey);

            // ---- timeline ---------------------------------------------------------------------------------------
            _events.Clear();
            _tweens.Clear();
            float tLight = .6f;                          // phase B starts: a short beat of silence first
            float tImpact = tLight + fallPart;
            float tBreak = tImpact + .45f;
            float tBurst = tBreak + crackStep;           // the shell explodes
            float tReveal = tBurst + brk.SecondsAt(5);   // phase E: debris and smoke only, the boss comes out
            float tArrive = tReveal + _riseSeconds;      // boss is at its idle spot
            float tRoar = tArrive + 1.1f;
            float tSettle = tRoar + 1.5f;
            float tEnd = tSettle + .7f;
            float ws = S * _windScale;

            // Phase B: the cocoon falls from above the screen.
            At(tLight, () =>
            {
                Tag("cocoon_spawn");
                _cocoon.Play(fall, groundPoint + Vector3.up * _fallHeight * (S / 1.5f), cs, false, 0f, false);
            });
            Over(tLight, tImpact, p =>
                _cocoon.transform.position = groundPoint + Vector3.up * (_fallHeight * (S / 1.5f) * (1f - p * p)));

            // Phase C: heavy impact.
            At(tImpact, () =>
            {
                Tag("cocoon_impact");
                Tag("camera_shake_heavy");
                _impact.Play(anim.Get(ImpactKey), groundPoint, Mathf.Min(S * 1.1f, halfW * 2f / CellUnits), false, 0f, false);
            });

            // Phase D: the shell cracks and bursts.
            At(tBreak, () => _cocoon.Play(brk, groundPoint, cs, false, 0f, false));
            At(tBreak + brk.SecondsAt(0), () => Tag("cocoon_crack"));
            At(tBurst, () =>
            {
                Tag("wind_burst");
                PlayWind(wind, groundPoint, ws);
            });
            float fade = tReveal + brk.SecondsAt(6) + brk.SecondsAt(7) * .4f;
            Over(fade, fade + .35f, p => _cocoon.SetAlpha(1f - p));

            // Phase D2: the boss flaps up out of the shell to its idle spot (it ends higher than the player's head).
            Vector3 riseFrom = groundPoint + Vector3.up * (1.6f * cs);
            At(tReveal, () =>
            {
                Tag("boss_reveal");
                body.position = riseFrom;
                body.localScale = _restScale * _riseStartScale;
                visual.SetAlpha(0f);
                visual.SetVisible(true);
            });
            Over(tReveal, tArrive, p =>
            {
                float e = 1f - Mathf.Pow(1f - p, 3f);
                body.position = Vector3.Lerp(riseFrom, _restPos, e);
                body.localScale = Vector3.Lerp(_restScale * _riseStartScale, _restScale, e);
                visual.SetAlpha(Mathf.Clamp01(p * 7f));
            });

            // Phase F: two ground fire pillars frame the boss on either side.
            float ps = S * _pillarScale;
            float[] pillarX = { -.8f, .8f };
            float[] pillarMul = { 1.2f, 1.2f };
            int[] pillarOrder = { 0, 1 };
            float room = player.extents.x + .45f;
            for (int i = 0; i < PillarCount; i++)
            {
                int k = i;
                float x = cx + pillarX[k] * halfW;
                if (Mathf.Abs(x - player.center.x) < room) x = player.center.x + Mathf.Sign(pillarX[k]) * room;
                Vector3 at = new Vector3(x, ground, 0f);
                float scale = ps * pillarMul[k];
                var rise = Slice(pillar, 0, 4, false);
                var hold = Slice(pillar, 3, 2, true);
                float start = tArrive + .15f + .09f * Array.IndexOf(pillarOrder, k);
                At(start, () =>
                {
                    if (k == 0) Tag("fire_pillar_spawn");
                    _pillars[k].Play(rise, at, scale, pillarX[k] > 0f, 0f, false, 0f, -1, null,
                        () => _pillars[k].Play(hold, at, scale, pillarX[k] > 0f, 0f, true));
                });
                At(tRoar + 1.1f + .06f * k, () => _pillars[k].Play(Slice(pillar, 5, 2, false), at, scale, pillarX[k] > 0f, 0f, false));
            }

            // Phase G: the Tyrant roars.
            var roarSound = def.RoarSound;
            At(tRoar, () =>
            {
                Tag("roar_start");
                visual.Play(def.RoarClip);
            });
            At(tRoar + .15f, () =>
            {
                if (roarSound != null && _audio != null) _audio.PlayOneShot(roarSound, PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            });
            At(tRoar + .25f, () => _aura.Play(anim.Get(AuraKey), mouth, S * .8f, false, 0f, false));
            float waveScale = Mathf.Min(S * 1.6f, halfW * 2f * _roarWaveWidth / (CellUnits * .84f));
            At(tRoar + .45f, () =>
            {
                Tag("roar_peak");
                _wave.Play(anim.Get(WaveKey), mouth, waveScale, false, 0f, false);
            });
            At(tRoar + .8f, () =>
            {
                Tag("ground_quake");
                _quake.Play(anim.Get(QuakeKey), groundPoint, Mathf.Min(S * 1.1f, halfW * 2f / CellUnits), false, 0f, false);
            });

            // ---- run it -----------------------------------------------------------------------------------------
            _events.Sort((a, b) => a.t.CompareTo(b.t));
            int next = 0;
            float t = 0f;
            while (t < tEnd)
            {
                if (isPlaying())
                {
                    float dt = Time.unscaledDeltaTime;
                    t += dt;
                    while (next < _events.Count && _events[next].t <= t) _events[next++].a();
                    for (int i = 0; i < _tweens.Count; i++)
                    {
                        var tw = _tweens[i];
                        if (tw.done || t < tw.t0) continue;
                        float p = Mathf.Clamp01((t - tw.t0) / Mathf.Max(.0001f, tw.t1 - tw.t0));
                        tw.a(p);
                        if (p >= 1f) { tw.done = true; _tweens[i] = tw; }
                    }
                    foreach (var fx in _all) fx.Tick(dt);
                }
                yield return null;
            }

            foreach (var fx in _all) fx.Stop();
            RestoreBoss();
            visual.SetVisible(true);
            Tag("entrance_complete");
        }

        private void PlayWind(BossClip clip, Vector3 ground, float scale) =>
            _wind.Play(clip, ground, scale, false, 0f, false);

        /// <summary>The dying boss drops from its hover spot to the player's floor, landing with a thud, while its death clip plays.</summary>
        public IEnumerator Defeat(BossDefinition def, BossVisual visual, Bounds player, float deathSeconds, Func<bool> isPlaying)
        {
            Vector3 from = transform.position;
            float toY = Mathf.Min(from.y, player.min.y + def.DeathFloorOffset * def.Scale);
            float fall = Mathf.Min(.9f, deathSeconds * .7f);
            float t = 0f;
            bool landed = false;
            while (t < deathSeconds)
            {
                if (isPlaying())
                {
                    t += Time.unscaledDeltaTime;
                    float p = Mathf.Clamp01(t / fall);
                    transform.position = new Vector3(from.x, Mathf.Lerp(from.y, toY, p * p), from.z);
                    if (!landed && p >= 1f)
                    {
                        landed = true;
                        ScreenHitFeedback.Shake(.16f, .4f);
                    }
                }
                yield return null;
            }
        }

        /// <summary>Fallback for a boss whose set has no cocoon clips: fade in where it hovers and roar.</summary>
        private IEnumerator SimpleAppear(BossDefinition def, BossVisual visual, Func<bool> isPlaying)
        {
            visual.SetVisible(true);
            float t = 0f;
            while (t < .7f)
            {
                if (isPlaying()) { t += Time.unscaledDeltaTime; visual.SetAlpha(t / .7f); }
                yield return null;
            }
            visual.SetAlpha(1f);
            float roar = Mathf.Max(.6f, visual.Duration(def.RoarClip));
            visual.Play(def.RoarClip);
            if (def.RoarSound != null && _audio != null) _audio.PlayOneShot(def.RoarSound, PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            t = 0f;
            while (t < roar + .2f)
            {
                if (isPlaying()) t += Time.unscaledDeltaTime;
                yield return null;
            }
            RestoreBoss();
        }
    }
}
