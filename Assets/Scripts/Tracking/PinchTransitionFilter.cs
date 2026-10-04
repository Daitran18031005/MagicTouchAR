namespace MagicTouch.Tracking
{
    /// <summary>
    /// One ambiguous open sample pauses ink; the next sample confirms release or
    /// resumes the held pinch. A clearly open pose releases immediately.
    /// </summary>
    public sealed class PinchTransitionFilter
    {
        public bool IsPinching { get; private set; }
        public bool IsReleasePending { get; private set; }

        public void Reset(bool pinching = false)
        {
            IsPinching = pinching;
            IsReleasePending = false;
        }

        public void Update(float distance, float enterThreshold, float exitThreshold)
        {
            if (!IsPinching)
            {
                IsReleasePending = false;
                IsPinching = distance < enterThreshold;
                return;
            }
            if (distance <= exitThreshold)
            {
                IsReleasePending = false;
                return;
            }
            if (distance >= exitThreshold * 1.5f || IsReleasePending)
            {
                Reset();
                return;
            }
            IsReleasePending = true;
        }
    }
}
