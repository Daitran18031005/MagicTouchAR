using System.Collections.Generic;
using UnityEngine;

namespace MagicTouch.Recognition
{
    /// <summary>Geometry gate for early commit. Confidence alone cannot complete a prefix.</summary>
    public static class GestureCompletionPolicy
    {
        public static bool IsStraight(IReadOnlyList<Point> points)
        {
            if (points == null || points.Count < 2) return false;
            int stroke = points[0].StrokeID;
            float length = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                if (points[i].StrokeID != stroke) return false;
                length += Vector2.Distance(Pos(points[i - 1]), Pos(points[i]));
            }
            return length > 1f && Vector2.Distance(Pos(points[0]), Pos(points[points.Count - 1])) / length > 0.90f;
        }

        public static bool CanCommit(IReadOnlyList<Point> points, PRecognitionResult result, PRecognitionResult geometry)
        {
            if (!result.IsMatch || !geometry.IsMatch || result.Symbol != geometry.Symbol || IsStraight(points)) return false;
            var bounds = GesturePreprocessor.CalculateBounds(points);
            if (bounds.Diagonal < 35f) return false;
            switch (result.Symbol)
            {
                case GestureSymbol.X: return HasCross(points);
                case GestureSymbol.E: return HasThreeArms(points);
                case GestureSymbol.O:
                    return points[0].StrokeID == points[points.Count - 1].StrokeID &&
                        Vector2.Distance(Pos(points[0]), Pos(points[points.Count - 1])) < bounds.Diagonal * 0.22f;
                case GestureSymbol.V:
                case GestureSymbol.Caret:
                case GestureSymbol.W:
                case GestureSymbol.Z:
                case GestureSymbol.S:
                    // A partially drawn multi-stroke E must not finalize as a different letter.
                    return points[0].StrokeID == points[points.Count - 1].StrokeID;
                default: return false;
            }
        }

        private static Vector2 Pos(Point p) => new Vector2(p.X, p.Y);
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        public static bool HasCross(IReadOnlyList<Point> points)
        {
            int split = 1;
            while (split < points.Count && points[split].StrokeID == points[0].StrokeID) split++;
            if (split < 2 || split >= points.Count - 1) return false;
            for (int i = split; i < points.Count; i++)
                if (points[i].StrokeID != points[split].StrokeID) return false;
            Vector2 a = Pos(points[0]), b = Pos(points[split - 1]);
            Vector2 c = Pos(points[split]), d = Pos(points[points.Count - 1]);
            Vector2 u = b - a, v = d - c;
            if (u.magnitude < 20f || v.magnitude < 20f) return false;
            float cross = Cross(u, v);
            if (Mathf.Abs(cross) < u.magnitude * v.magnitude * 0.60f) return false;
            float t = Cross(c - a, v) / cross, s = Cross(c - a, u) / cross;
            if (t < 0.15f || t > 0.85f || s < 0.15f || s > 0.85f) return false;
            // Both segments must actually be approximately straight, not just crossed endpoints.
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 start = i < split ? a : c, direction = i < split ? u : v;
                if (Mathf.Abs(Cross(Pos(points[i]) - start, direction)) / direction.magnitude > direction.magnitude * 0.12f) return false;
            }
            return true;
        }

        public static bool HasThreeArms(IReadOnlyList<Point> points)
        {
            if (points == null || points.Count < 6) return false;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (Point p in points) { minX = System.Math.Min(minX,p.X); maxX = System.Math.Max(maxX,p.X); minY = System.Math.Min(minY,p.Y); maxY = System.Math.Max(maxY,p.Y); }
            float width = maxX-minX, height = maxY-minY;
            if (width < 15f || height < 25f) return false;
            // Coverage, not one tiny horizontal segment or number of pen lifts.
            // Normalize size and sample actual segments only (never bridge separate strokes).
            const int bins = 20;
            bool[,] arms = new bool[3, bins];
            bool[] spine = new bool[bins];
            for (int i=1;i<points.Count;i++)
            {
                Point a=points[i-1], b=points[i];
                if (a.StrokeID != b.StrokeID) continue;
                float dx=System.Math.Abs(b.X-a.X), dy=System.Math.Abs(b.Y-a.Y);
                if (dx + dy < .001f) continue;
                int steps = System.Math.Max(1, (int)System.Math.Ceiling(
                    System.Math.Max(dx / width, dy / height) * bins * 2));
                for (int j=0;j<=steps;j++)
                {
                    float t=(float)j/steps;
                    float x=(a.X+(b.X-a.X)*t-minX)/width;
                    float y=(a.Y+(b.Y-a.Y)*t-minY)/height;
                    int ix=System.Math.Max(0,System.Math.Min(bins-1,(int)(x*bins)));
                    int iy=System.Math.Max(0,System.Math.Min(bins-1,(int)(y*bins)));
                    if (dy > dx*1.5f && x <= .25f) spine[iy]=true;
                    if (dx <= dy*1.5f) continue;
                    if (y <= .22f) arms[0,ix]=true;
                    else if (y >= .35f && y <= .65f) arms[1,ix]=true;
                    else if (y >= .78f) arms[2,ix]=true;
                }
            }
            // Allow one small sampling gap; require a substantial spine and left-connected arms.
            int missingSpine=0;
            for (int i=2;i<=17;i++) if (!spine[i]) missingSpine++;
            if (missingSpine > 1) return false;
            for (int arm=0;arm<3;arm++)
            {
                int missing=0;
                int end=arm==1 ? 9 : 13; // Middle arm may be shorter than top/bottom.
                for (int x=3;x<=end;x++) if (!arms[arm,x]) missing++;
                if (missing > 1) return false;
            }
            return true;
        }

        /// <summary>Mandatory validity gate, including timeout and fallback recognition.</summary>
        public static bool HasRequiredStructure(IReadOnlyList<Point> points, GestureSymbol symbol)
        {
            return symbol != GestureSymbol.E || HasThreeArms(points);
        }
    }
}
