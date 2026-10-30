using System;
using MagicTouch.Gameplay;
using UnityEngine;

namespace MagicTouch.Juice
{
    /// <summary>
    /// A pooled, frame-animated effect sprite. It has no Update: its owner calls Tick, so pause is respected
    /// and nothing is created or destroyed while playing.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossFxSprite : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private BossClip _clip;
        private bool _loop;
        private int _frame;
        private float _timer;
        private float _frameSeconds;
        private int _callbackFrame = -1;
        private Action _onFrame;
        private Action _onDone;

        public bool IsPlaying { get; private set; }
        /// <summary>Taken by a pool owner that will call Play after a delay.</summary>
        public bool IsReserved { get; private set; }
        public int Frame => _frame;

        public void Reserve() => IsReserved = true;
        public SpriteRenderer Renderer => _renderer != null ? _renderer : (_renderer = GetComponent<SpriteRenderer>());

        /// <param name="frameSeconds">Uniform frame time; 0 uses the clip's own times.</param>
        public void Play(BossClip clip, Vector3 position, float scale, bool flipX, float rotationZ, bool loop,
            float frameSeconds = 0f, int callbackFrame = -1, Action onFrame = null, Action onDone = null)
        {
            if (clip == null || clip.Length == 0) { IsReserved = false; onDone?.Invoke(); return; }
            IsReserved = false;
            _clip = clip;
            _loop = loop || clip.loop;
            _frameSeconds = frameSeconds;
            _callbackFrame = callbackFrame;
            _onFrame = onFrame;
            _onDone = onDone;
            _timer = 0f;
            transform.position = position;
            transform.localScale = Vector3.one * scale;
            transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
            Renderer.flipX = flipX;
            Renderer.color = Color.white;
            gameObject.SetActive(true);
            IsPlaying = true;
            SetFrame(0);
        }

        public void SetAlpha(float alpha)
        {
            var color = Renderer.color;
            color.a = Mathf.Clamp01(alpha);
            Renderer.color = color;
        }

        public void Stop()
        {
            IsPlaying = false;
            IsReserved = false;
            _onFrame = null;
            _onDone = null;
            gameObject.SetActive(false);
        }

        public void Tick(float dt)
        {
            if (!IsPlaying) return;
            _timer += dt;
            float step = StepSeconds(_frame);
            while (IsPlaying && _timer >= step)
            {
                _timer -= step;
                int next = _frame + 1;
                if (next >= _clip.Length)
                {
                    if (_loop) next = 0;
                    else
                    {
                        var done = _onDone;
                        Stop();
                        done?.Invoke();
                        return;
                    }
                }
                SetFrame(next);
                step = StepSeconds(_frame);
            }
        }

        private float StepSeconds(int frame) => _frameSeconds > 0f ? _frameSeconds : _clip.SecondsAt(frame);

        private void SetFrame(int index)
        {
            _frame = index;
            Renderer.sprite = _clip.frames[index];
            if (index == _callbackFrame && _onFrame != null)
            {
                var callback = _onFrame;
                _onFrame = null;
                callback();
            }
        }
    }
}
