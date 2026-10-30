using System;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    /// <summary>
    /// Plays a boss's pose clips on its body SpriteRenderer. Driven by Tick so pause is owned by the encounter.
    /// Lives on the boss's EnemyBody child; the boss root is moved by the entrance.
    /// </summary>
    public sealed class BossVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _body;
        [SerializeField] private BossAnimationSet _animations;
        [SerializeField] private Color _hurtTint = new Color(1f, .45f, .35f, 1f);

        private BossClip _clip;
        private string _rest = "idle";
        private bool _restPlaying;
        private bool _dead;
        private int _frame;
        private float _timer;
        private float _flash;
        private float _preSeconds;
        private string _pendingKey;
        private float _pendingDelay;
        private int _pendingImpact;
        private Action _pendingOnImpact, _pendingOnEnd;
        private Vector3 _home;
        private bool _hasHome;
        private int _impact = -1;
        private Action _onImpact;
        private Action _onEnd;

        public SpriteRenderer Body => _body;

        public void Bind(BossAnimationSet animations) => _animations = animations;

        /// <summary>For visuals added at runtime (creeps), where there is no serialized body.</summary>
        public void Bind(SpriteRenderer body, BossAnimationSet animations) { _body = body; _animations = animations; }

        public void SetVisible(bool visible) { if (_body != null) _body.enabled = visible; }

        public void SetAlpha(float alpha)
        {
            if (_body == null) return;
            var color = _body.color;
            color.a = Mathf.Clamp01(alpha);
            _body.color = color;
        }

        public float Duration(string key) => _animations != null && _animations.Get(key) != null ? _animations.Get(key).Duration : 0f;

        public void ResetVisual(string restClip)
        {
            _dead = false;
            _flash = 0f;
            _rest = restClip;
            if (_body != null) _body.color = Color.white;
            PlayRest();
        }

        /// <summary>Changes the clip the boss falls back to (e.g. idle to idle_cracked).</summary>
        public void SetRest(string restClip)
        {
            _rest = restClip;
            if (_restPlaying && !_dead && _pendingKey == null) PlayRest();
        }

        public void Play(string key, Action onEnd = null)
        {
            if (_dead) return;
            Begin(_animations?.Get(key), false, -1f, -1, null, onEnd);
        }

        /// <summary>
        /// Plays a skill so that its impact frame lands exactly preSeconds from now. The wind-up frames keep their own speed
        /// (stretching a handful of frames over a 2 s warning is a slideshow): the boss stays in its rest clip, then the
        /// wind-up starts just in time. Only when the wind-up is longer than the warning is it compressed to fit.
        /// </summary>
        public void PlaySkill(string key, float preSeconds, int impactFrame, Action onImpact, Action onEnd)
        {
            if (_dead) return;
            var clip = _animations?.Get(key);
            if (clip == null || clip.Length == 0) return;
            int impact = Mathf.Clamp(impactFrame, 0, clip.Length - 1);
            float natural = 0f;
            for (int i = 0; i < impact; i++) natural += clip.SecondsAt(i);
            float wait = preSeconds - natural;
            if (preSeconds <= 0f || impact <= 0 || wait <= .02f)
            {
                Begin(clip, false, preSeconds, impact, onImpact, onEnd);
                return;
            }
            _pendingKey = key;
            _pendingDelay = wait;
            _pendingImpact = impact;
            _pendingOnImpact = onImpact;
            _pendingOnEnd = onEnd;
        }

        /// <summary>Drops a skill whose wind-up has not started yet (the warning was cancelled or the boss stopped).</summary>
        public void CancelPending() => _pendingKey = null;

        /// <summary>Red flash, plus the hurt clip unless <paramref name="playClip"/> is false (a skill pose must not be cut).</summary>
        public void Hurt(string key, bool playClip = true)
        {
            if (_dead) return;
            _flash = .25f;
            if (!playClip) return;
            Begin(_animations?.Get(key), false, -1f, -1, null, null);
        }

        /// <summary>
        /// Charging a skill (0..1): the held wind-up pose trembles more and more so a slow warning never looks frozen.
        /// Only the body child moves, never the boss root. 0 puts it back.
        /// </summary>
        public void SetCharge(float charge)
        {
            if (_body == null) return;
            if (!_hasHome) { _home = _body.transform.localPosition; _hasHome = true; }
            if (charge <= 0f) { _body.transform.localPosition = _home; return; }
            float amplitude = .012f + .045f * charge;
            float t = Time.unscaledTime;
            _body.transform.localPosition = _home + new Vector3(Mathf.Sin(t * 47f) * amplitude, Mathf.Sin(t * 61f + 1.3f) * amplitude * .6f, 0f);
        }

        public void Die(string key)
        {
            _dead = true;
            _flash = 0f;
            if (_body != null) _body.color = Color.white;
            Begin(_animations?.Get(key), false, -1f, -1, null, null);
        }

        public void Tick(float dt)
        {
            if (_body == null) return;
            if (_pendingKey != null)
            {
                _pendingDelay -= dt;
                if (_pendingDelay <= 0f)
                {
                    var clip = _animations?.Get(_pendingKey);
                    int impact = _pendingImpact;
                    Action onImpact = _pendingOnImpact, onEnd = _pendingOnEnd;
                    _pendingKey = null;
                    Begin(clip, false, -1f, impact, onImpact, onEnd);
                }
            }
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt);
                _body.color = Color.Lerp(Color.white, _hurtTint, _flash / .25f);
            }
            if (_clip == null || _clip.Length == 0) return;
            _timer += dt;
            float step = FrameSeconds(_frame);
            while (_timer >= step)
            {
                _timer -= step;
                if (!Advance()) return;
                step = FrameSeconds(_frame);
            }
        }

        private void PlayRest() => Begin(_animations?.Get(_rest), true, -1f, -1, null, null);

        private void Begin(BossClip clip, bool rest, float preSeconds, int impact, Action onImpact, Action onEnd)
        {
            if (clip == null || clip.Length == 0) return;
            _pendingKey = null;
            _clip = clip;
            _restPlaying = rest;
            _preSeconds = preSeconds;
            _impact = impact;
            _onImpact = onImpact;
            _onEnd = onEnd;
            _timer = 0f;
            SetFrame(0);
        }

        private float FrameSeconds(int frame) =>
            _preSeconds > 0f && _impact > 0 && frame < _impact ? _preSeconds / _impact : _clip.SecondsAt(frame);

        private bool Advance()
        {
            int next = _frame + 1;
            if (next >= _clip.Length)
            {
                if (_restPlaying || _clip.loop) next = 0;
                else if (_dead) { _timer = 0f; return false; }
                else
                {
                    var end = _onEnd;
                    _onEnd = null;
                    PlayRest();
                    end?.Invoke();
                    return false;
                }
            }
            SetFrame(next);
            return true;
        }

        private void SetFrame(int index)
        {
            _frame = index;
            if (_body != null) _body.sprite = _clip.frames[index];
            if (index == _impact && _onImpact != null)
            {
                var callback = _onImpact;
                _onImpact = null;
                callback();
            }
        }
    }
}
