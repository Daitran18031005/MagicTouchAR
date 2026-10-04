using UnityEngine;

namespace MagicTouch.Tracking
{
    /// <summary>One Euro filter in reference-height pixels; one instance per tracked hand.</summary>
    public sealed class OneEuroPositionFilter
    {
        private bool _initialized;
        private Vector2 _raw, _value, _derivative;
        public void Reset() { _initialized = false; }

        public Vector2 Filter(Vector2 raw, float dt, float minCutoff, float beta, float derivativeCutoff)
        {
            if (!_initialized)
            {
                _initialized = true;
                _raw = _value = raw;
                _derivative = Vector2.zero;
                return raw;
            }
            dt = Mathf.Clamp(dt, 0.001f, 0.25f);
            Vector2 velocity = (raw - _raw) / dt;
            _raw = raw;
            _derivative = Vector2.Lerp(_derivative, velocity, Alpha(derivativeCutoff, dt));
            // Independent cutoffs prevent motion/jitter on Y from disabling smoothing on X.
            // Small derivative fluctuations remain in the low-cutoff range instead of being
            // mistaken for an intentional fast stroke (units: reference-height pixels/sec).
            float baseCutoff = Mathf.Max(0.01f, minCutoff);
            float gain = Mathf.Max(0f, beta);
            float cutoffX = baseCutoff + gain * Mathf.Max(0f, Mathf.Abs(_derivative.x) - 60f);
            float cutoffY = baseCutoff + gain * Mathf.Max(0f, Mathf.Abs(_derivative.y) - 60f);
            _value = new Vector2(Mathf.Lerp(_value.x, raw.x, Alpha(cutoffX, dt)),
                                 Mathf.Lerp(_value.y, raw.y, Alpha(cutoffY, dt)));
            return _value;
        }

        private static float Alpha(float cutoff, float dt)
            => 1f / (1f + 1f / (2f * Mathf.PI * Mathf.Max(0.01f, cutoff) * dt));
    }
}
