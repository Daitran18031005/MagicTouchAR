using System;
using UnityEngine;

namespace MagicTouch.Tracking
{
    /// <summary>
    /// Represents tracking state and transformed coordinates for a single hand.
    /// </summary>
    [Serializable]
    public class TrackedHand
    {
        public int HandIndex;
        public bool IsDetected;
        public bool IsDrawing;
        public bool IsPinching;
        // Updated only when a new tracking sample is ingested, not on render frames.
        public uint SampleVersion;
        public float SampleTime;
        public float SampleInterval = 1f / 30f;

        // Screen space pixel coordinates (0..Screen.width, 0..Screen.height)
        public Vector2 IndexTipScreen;
        public Vector2 ThumbTipScreen;
        public Vector2 MiddleTipScreen;

        // World space coordinates calculated from active 2D camera
        public Vector3 IndexTipWorld;

        // Normalized distance between Thumb Tip (4) and Index Tip (8) for pinch gesture (stores SmoothedPinchDistance)
        public float PinchDistance;

        // Raw Euclidean distance calculated this frame
        public float RawPinchDistance;

        // Exponential Moving Average smoothed distance for noise reduction
        public float SmoothedPinchDistance;

        // Screen split designation: Left (0) for Player 1, Right (1) for Player 2
        public int AssignedPlayerId => (IndexTipScreen.x < Screen.width * 0.5f) ? 0 : 1;
    }
}
