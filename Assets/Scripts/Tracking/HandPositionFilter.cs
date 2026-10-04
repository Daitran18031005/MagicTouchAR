using UnityEngine;

namespace MagicTouch.Tracking
{
    /// <summary>
    /// Shared cursor/pen estimator. Palm translation stays responsive, while the
    /// fingertip offset (noisier when thumb/index overlap) uses a steadier cutoff.
    /// This is a single position estimate, not two filters cascaded on the same signal.
    /// </summary>
    public sealed class HandPositionFilter
    {
        private readonly OneEuroPositionFilter _palm = new OneEuroPositionFilter();
        private readonly OneEuroPositionFilter _offset = new OneEuroPositionFilter();
        public void Reset() { _palm.Reset(); _offset.Reset(); }

        public Vector2 Filter(Vector2 tip, Vector2 wrist, float dt, float minCutoff, float beta,
                              float derivativeCutoff, float offsetCutoff, float offsetBeta)
        {
            Vector2 palm = _palm.Filter(wrist, dt, minCutoff, beta, derivativeCutoff);
            Vector2 offset = _offset.Filter(tip - wrist, dt, offsetCutoff, offsetBeta, derivativeCutoff);
            return palm + offset;
        }
    }
}
