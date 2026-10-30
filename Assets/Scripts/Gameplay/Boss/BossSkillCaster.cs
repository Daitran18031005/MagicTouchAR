using System;
using MagicTouch.Juice;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    /// <summary>
    /// Picks boss skills at random, warns the player, and applies the hit. A boss balloon popped during the
    /// warning cancels the attack (Interrupt). Effects come from a pool built once; nothing is created while playing.
    /// </summary>
    public sealed class BossSkillCaster : MonoBehaviour
    {
        private enum State { Stopped, Idle, Telegraph, Strike }

        private struct Flight
        {
            public BossFxSprite fx;
            public BossClip clip;
            public Vector3 from, to;
            public float delay, t, duration, scale, frameSeconds, rotation;
            public bool active, started, moving, hitOnEnd;
        }

        private const int FireballPool = 6;
        private const int SpikePool = 1;
        private const int SlashPool = 2;
        // Warning ring: green (aiming) then yellow (preparing) can be cancelled; the last RedSeconds (red, striking) cannot.
        private const float RedSeconds = .4f;
        private const float GreenShare = .45f; // of the cancellable part
        private static readonly Color Green = new Color32(88, 230, 110, 255);
        private static readonly Color Yellow = new Color32(255, 214, 64, 255);
        private static readonly Color Red = new Color32(255, 64, 48, 255);

        private readonly BossSkillBag _bag = new BossSkillBag(Environment.TickCount);
        private BossDefinition _def;
        private BossVisual _visual;
        private GameManager _game;
        private BossSegment _segment;
        private BossSkillDefinition _skill;
        private State _state = State.Stopped;
        private float _timer;
        private float _strikeAt;
        private float _lead = 1.2f; // warning time of the last skill: the next one starts this much earlier
        private float _clock;
        private float _phase;        // seconds into the warning, then into the strike
        private float _strikeSeconds;
        private bool _hitApplied;
        private BossFxSprite[] _fireballs, _spikes, _slashes;
        private readonly Flight[] _flights = new Flight[FireballPool];
        private SpriteRenderer _marker;

        public SpriteRenderer Marker => _marker;
        public bool IsStriking => _state == State.Strike;
        /// <summary>A skill is in its warning or strike: the boss keeps its skill pose instead of flinching.</summary>
        public bool IsCasting => _state == State.Telegraph || _state == State.Strike;

        public void Configure(BossDefinition definition, BossVisual visual, GameManager game)
        {
            _def = definition;
            _visual = visual;
            _game = game;
            if (_fireballs == null)
            {
                _fireballs = BuildPool("Fireball", FireballPool);
                _spikes = BuildPool("GroundSpike", SpikePool);
                _slashes = BuildPool("ClawSlash", SlashPool);
                _marker = BuildMarker();
            }
        }

        public void Begin(int segmentIndex)
        {
            SetSegment(segmentIndex);
            _state = State.Idle;
            _timer = _segment != null ? _segment.castInterval * .25f : 0f;
        }

        public void SetSegment(int segmentIndex)
        {
            if (_def == null || segmentIndex < 0 || segmentIndex >= _def.Segments.Length) return;
            _segment = _def.Segments[segmentIndex];
            _bag.Reset();
            if (_state == State.Idle) _timer = 0f;
        }

        public void Stop()
        {
            _state = State.Stopped;
            _skill = null;
            HideMarker();
            if (_visual != null) { _visual.SetCharge(0f); _visual.CancelPending(); }
            StopAllFx();
        }

        /// <summary>The player popped a boss balloon: a skill still green or yellow is cancelled; red is too late.</summary>
        public bool Interrupt()
        {
            if (_state != State.Telegraph || _phase >= _skill.TelegraphSeconds - RedSeconds) return false;
            HideMarker();
            _visual.SetCharge(0f);
            _visual.CancelPending();
            _state = State.Idle;
            _skill = null;
            _timer = 0f;
            return true;
        }

        public void Tick(float dt)
        {
            _clock += dt;
            TickFlights(dt);
            TickFx(_fireballs, dt);
            TickFx(_spikes, dt);
            TickFx(_slashes, dt);
            if (_state == State.Idle && _segment != null)
            {
                _timer += dt;
                // castInterval is the time from one strike to the next, so the warning time is part of it.
                if (_timer >= Mathf.Max(.5f, _segment.castInterval - _lead)) StartSkill();
            }
            else if (_state == State.Telegraph)
            {
                // Timed here, not by the boss clip: a hurt flinch can replace the clip and its callbacks.
                _phase += dt;
                if (_phase >= _skill.TelegraphSeconds) Release();
                else
                {
                    UpdateMarker();
                    _visual.SetCharge(_phase / _skill.TelegraphSeconds);
                }
            }
            else if (_state == State.Strike)
            {
                _phase += dt;
                if (_phase >= _strikeSeconds)
                {
                    _state = State.Idle;
                    _timer = _clock - _strikeAt;
                }
            }
        }

        private void StartSkill()
        {
            var skills = _segment.skills;
            int index = _bag.Next(skills != null ? skills.Length : 0);
            if (index < 0 || skills[index] == null) { _timer = 0f; return; }
            _skill = skills[index];
            _lead = _skill.TelegraphSeconds;
            _hitApplied = false;
            _phase = 0f;
            _state = State.Telegraph;
            ShowMarker(_skill.Kind == BossSkillKind.GroundSpike ? PlayerFeet() : PlayerCenter());
            _visual.PlaySkill(_skill.BossClip, _skill.TelegraphSeconds, _skill.ImpactFrame, null, null);
        }

        // Time the boss clip needs from its impact frame to its last frame.
        private float StrikeSeconds()
        {
            var clip = _def.Animations.Get(_skill.BossClip);
            if (clip == null || clip.Length == 0) return .3f;
            float seconds = 0f;
            for (int i = Mathf.Min(_skill.ImpactFrame, clip.Length - 1); i < clip.Length; i++) seconds += clip.SecondsAt(i);
            return Mathf.Max(.1f, seconds);
        }

        private void Release()
        {
            HideMarker();
            _visual.SetCharge(0f);
            _state = State.Strike;
            _phase = 0f;
            _strikeSeconds = StrikeSeconds();
            _strikeAt = _clock;
            if (_skill.Sound != null && _game != null && _game.sfxSource != null) _game.sfxSource.PlayOneShot(_skill.Sound);
            switch (_skill.Kind)
            {
                case BossSkillKind.Projectile: LaunchProjectile(); break;
                case BossSkillKind.GroundSpike: LaunchSpike(); break;
                default: LaunchSlash(); break;
            }
        }

        private void LaunchProjectile()
        {
            var clip = _def.Animations.Get(_skill.FxClip);
            Vector3 from = transform.position + (Vector3)(_skill.MouthOffset * _def.Scale);
            Vector3 to = PlayerCenter();
            Vector3 direction = (to - from).normalized;
            float rotation = Vector2.SignedAngle(Vector2.down, direction);
            if (UnityEngine.Random.value >= _skill.ColumnChance)
            {
                StartFlight(clip, from, to, 0f, _skill.FlightSeconds, rotation, true, true);
                return;
            }
            // Fire column: a stream of flames appears along the path, mouth first, and the last one hits.
            const int pieces = 5;
            for (int i = 0; i < pieces; i++)
            {
                Vector3 point = Vector3.Lerp(from, to, (i + 1f) / pieces);
                StartFlight(clip, point, point, i * .07f, .3f, rotation, false, i == pieces - 1);
            }
        }

        private void StartFlight(BossClip clip, Vector3 from, Vector3 to, float delay, float duration,
            float rotation, bool moving, bool hitOnEnd)
        {
            var fx = Acquire(_fireballs);
            if (fx == null || clip == null) { if (hitOnEnd) ApplyHit(); return; }
            for (int i = 0; i < _flights.Length; i++)
            {
                if (_flights[i].active) continue;
                _flights[i] = new Flight
                {
                    fx = fx, clip = clip, from = from, to = to, delay = delay, duration = Mathf.Max(.05f, duration),
                    scale = SkillScale(clip), frameSeconds = _skill.FxFrameSeconds, rotation = rotation,
                    active = true, moving = moving, hitOnEnd = hitOnEnd
                };
                fx.Reserve(); // taken while it waits for its delay
                return;
            }
        }

        private void TickFlights(float dt)
        {
            for (int i = 0; i < _flights.Length; i++)
            {
                if (!_flights[i].active) continue;
                ref Flight f = ref _flights[i];
                f.t += dt;
                if (!f.started && f.t >= f.delay)
                {
                    f.started = true;
                    f.fx.Play(f.clip, f.from, f.scale, false, f.rotation, true, f.frameSeconds);
                }
                if (!f.started) continue;
                float p = (f.t - f.delay) / f.duration;
                if (f.moving) f.fx.transform.position = Vector3.Lerp(f.from, f.to, Mathf.Clamp01(p));
                if (p < 1f) continue;
                f.active = false;
                f.fx.Stop();
                if (f.hitOnEnd) ApplyHit();
            }
        }

        private void LaunchSpike()
        {
            var clip = _def.Animations.Get(_skill.FxClip);
            var fx = Acquire(_spikes);
            float scale = SkillScale(clip);
            Vector3 position = PlayerFeet() + (Vector3)(_skill.FxOffset * scale);
            if (fx == null || clip == null) { ApplyHit(); return; }
            fx.Play(clip, position, scale, false, 0f, false, _skill.FxFrameSeconds, _skill.FxHitFrame, ApplyHit);
        }

        private void LaunchSlash()
        {
            var clip = _def.Animations.Get(_skill.FxClip);
            Vector3 position = PlayerCenter() + (Vector3)(_skill.FxOffset * SkillScale(clip));
            bool left = PlaySlash(clip, position, _skill.SlashSides.HasFlag(BossSlashSides.Left), false);
            bool right = PlaySlash(clip, position, _skill.SlashSides.HasFlag(BossSlashSides.Right), true);
            if (!left && !right) ApplyHit();
        }

        private bool PlaySlash(BossClip clip, Vector3 position, bool wanted, bool flipX)
        {
            if (!wanted || clip == null) return false;
            var fx = Acquire(_slashes);
            if (fx == null) return false;
            fx.Play(clip, position, SkillScale(clip), flipX, 0f, false, _skill.FxFrameSeconds, _skill.FxHitFrame, ApplyHit);
            return true;
        }

        private void ApplyHit()
        {
            if (_hitApplied || _skill == null) return;
            _hitApplied = true;
            // LoseLife can end the round, which stops the caster and clears _skill, so read the damage once.
            int damage = _skill.Damage;
            for (int i = 0; i < damage; i++)
            {
                if (_game == null || _game.currentState != GameState.Playing) return;
                _game.LoseLife();
            }
        }

        /// <summary>Effect scale: a multiple of the player's height when the skill asks for it, else its fixed scale.</summary>
        private float SkillScale(BossClip clip)
        {
            if (_skill.PlayerHeightMultiple <= 0f) return _skill.FxScale;
            var wizard = _game != null ? _game.playerWizardSprite : null;
            float player = wizard != null ? wizard.bounds.size.y : 2f;
            float cell = clip != null && clip.Length > 0 && clip.frames[0] != null ? clip.frames[0].bounds.size.y : 1.28f;
            return _skill.PlayerHeightMultiple * player / (cell * Mathf.Max(.2f, _skill.ContentHeight));
        }

        private Vector3 PlayerCenter()
        {
            var wizard = _game != null ? _game.playerWizardSprite : null;
            return wizard != null ? wizard.bounds.center : new Vector3(0f, -2.2f, 0f);
        }

        private Vector3 PlayerFeet()
        {
            var wizard = _game != null ? _game.playerWizardSprite : null;
            return wizard != null ? new Vector3(wizard.bounds.center.x, wizard.bounds.min.y, 0f) : new Vector3(0f, -3f, 0f);
        }

        private static BossFxSprite Acquire(BossFxSprite[] pool)
        {
            foreach (var item in pool) if (!item.IsPlaying && !item.IsReserved) return item;
            return null;
        }

        private static void TickFx(BossFxSprite[] pool, float dt)
        {
            if (pool == null) return;
            foreach (var item in pool) item.Tick(dt);
        }

        private void StopAllFx()
        {
            for (int i = 0; i < _flights.Length; i++) _flights[i].active = false;
            StopPool(_fireballs);
            StopPool(_spikes);
            StopPool(_slashes);
        }

        private static void StopPool(BossFxSprite[] pool)
        {
            if (pool == null) return;
            foreach (var item in pool) item.Stop();
        }

        private BossFxSprite[] BuildPool(string name, int count)
        {
            var pool = new BossFxSprite[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"{name}_{i}", typeof(SpriteRenderer), typeof(BossFxSprite));
                go.transform.SetParent(transform, false);
                go.GetComponent<SpriteRenderer>().sortingOrder = 6;
                pool[i] = go.GetComponent<BossFxSprite>();
                go.SetActive(false);
            }
            return pool;
        }

        private SpriteRenderer BuildMarker()
        {
            // Not a child of the boss: it must stay where the hit will land, whatever the boss root does.
            var go = new GameObject("WarningMarker", typeof(SpriteRenderer));
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = BuildRingSprite();
            renderer.sortingOrder = 4;
            go.SetActive(false);
            return renderer;
        }

        private static Sprite BuildRingSprite()
        {
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "BossWarningRing", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var ink = new Color32(10, 7, 13, 255);
            var fire = new Color32(255, 255, 255, 255); // tinted per phase by the renderer colour
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f));
                bool cross = (Mathf.Abs(x - 15.5f) < 1.5f || Mathf.Abs(y - 15.5f) < 1.5f) && r < 5.5f;
                texture.SetPixel(x, y, r >= 11.5f && r < 14.5f || cross ? fire
                    : r >= 10.5f && r < 15.5f || (cross && r < 6.5f) ? ink : clear);
            }
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 16f);
        }

        private void ShowMarker(Vector3 position)
        {
            if (_marker == null) return;
            _marker.transform.position = position;
            _marker.gameObject.SetActive(true);
            UpdateMarker();
        }

        private void HideMarker()
        {
            if (_marker != null) _marker.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_marker != null) Destroy(_marker.gameObject);
        }

        // Green (aim) shrinks slowly, yellow (prepare) pulses faster, red (strike, no cancel) is solid and fast.
        private void UpdateMarker()
        {
            if (_marker == null || _skill == null) return;
            float red = _skill.TelegraphSeconds - RedSeconds;
            float green = red * GreenShare;
            Color color;
            float speed, size;
            if (_phase < green) { color = Green; speed = 5f; size = Mathf.Lerp(2.2f, 1.9f, _phase / green); }
            else if (_phase < red) { color = Yellow; speed = 10f; size = Mathf.Lerp(1.9f, 1.6f, (_phase - green) / (red - green)); }
            else { color = Red; speed = 24f; size = 1.5f; }
            float pulse = Mathf.Sin(_clock * speed);
            _marker.transform.localScale = Vector3.one * (size + pulse * .08f);
            color.a = _phase < red ? .8f + pulse * .2f : 1f;
            _marker.color = color;
        }
    }
}
