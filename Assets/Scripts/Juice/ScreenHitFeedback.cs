using UnityEngine;
using UnityEngine.UI;

namespace MagicTouch.Juice
{
    /// <summary>
    /// Red screen flash plus a short camera shake when the player loses a life, and a flash-less <see cref="Shake"/>
    /// for scripted impacts. Built on first use, then reused; runs on unscaled time and always restores the camera.
    /// </summary>
    public sealed class ScreenHitFeedback : MonoBehaviour
    {
        private const float HitDuration = .35f;
        private const float HitAmplitude = .14f;
        private const float FlashAlpha = .38f;

        private static ScreenHitFeedback _instance;
        private Image _flash;
        private Camera _camera;
        private Vector3 _cameraBase;
        private float _time = -1f;
        private float _duration = HitDuration;
        private float _amplitude = HitAmplitude;
        private bool _flashing;

        public static void Play() => Instance().Begin(HitDuration, HitAmplitude, true);

        /// <summary>Camera shake only (no red flash). A stronger shake already running is not weakened.</summary>
        public static void Shake(float amplitude, float duration) => Instance().Begin(duration, amplitude, false);

        private static ScreenHitFeedback Instance()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ScreenHitFeedback");
            _instance = go.AddComponent<ScreenHitFeedback>();
            _instance.Build();
            return _instance;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300; // above the HUD, below the hand cursor
            var flash = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flash.transform.SetParent(transform, false);
            var rect = (RectTransform)flash.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            _flash = flash.GetComponent<Image>();
            _flash.color = new Color(1f, .05f, .05f, 0f);
            _flash.raycastTarget = false;
        }

        private void Begin(float duration, float amplitude, bool flash)
        {
            bool running = _time >= 0f;
            if (!running)
            {
                _camera = Camera.main;
                if (_camera != null) _cameraBase = _camera.transform.position;
            }
            float remainingStrength = running ? _amplitude * Mathf.Clamp01(1f - _time / _duration) : 0f;
            if (!running || amplitude >= remainingStrength)
            {
                _duration = duration;
                _amplitude = amplitude;
                _time = 0f;
            }
            _flashing |= flash;
        }

        private void Update()
        {
            if (_time < 0f) return;
            _time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(1f - _time / _duration);
            if (_flashing) _flash.color = new Color(1f, .05f, .05f, FlashAlpha * k * k);
            if (_camera != null)
                _camera.transform.position = _time >= _duration
                    ? _cameraBase
                    : _cameraBase + (Vector3)(Random.insideUnitCircle * (_amplitude * k));
            if (_time >= _duration)
            {
                _time = -1f;
                _flashing = false;
                _flash.color = new Color(1f, .05f, .05f, 0f);
            }
        }
    }
}
