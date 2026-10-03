using System;
using UnityEngine;

namespace MagicTouch.Recognition
{
    /// <summary>
    /// Represents a point in a 2D point cloud gesture, tagged with its parent StrokeID.
    /// Used by the $P Point-Cloud Recognizer (Vatavu et al., ICMI 2012).
    /// </summary>
    [Serializable]
    public struct Point
    {
        public float X;
        public float Y;
        public int StrokeID;
        public int StrokeId => StrokeID;

        public Point(float x, float y, int strokeId = 0)
        {
            X = x;
            Y = y;
            StrokeID = strokeId;
        }

        public Point(Vector2 v, int strokeId = 0)
        {
            X = v.x;
            Y = v.y;
            StrokeID = strokeId;
        }

        public Vector2 ToVector2() => new Vector2(X, Y);

        public static float Distance(Point a, Point b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public static float SqrDistance(Point a, Point b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        public override string ToString() => $"Point({X:F3}f, {Y:F3}f, {StrokeID})";
    }
}
