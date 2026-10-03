using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicTouch.Recognition
{
    /// <summary>
    /// Result returned by the $1 Unistroke Recognizer.
    /// </summary>
    [Serializable]
    public struct RecognitionResult
    {
        public string GestureName;
        public float Score; // Normalized confidence [0.0 .. 1.0]
        public bool IsMatch;

        public static RecognitionResult NoMatch => new RecognitionResult
        {
            GestureName = "Unknown",
            Score = 0f,
            IsMatch = false
        };
    }

    /// <summary>
    /// Implementation of the $1 Unistroke Recognizer (Wobbrock, Wilson, Li - UIST 2007)
    /// optimized for Magic Touch gesture matching (V, ^, -, |, Z, O).
    /// Orientation-sensitive matching is enforced so that 'V' and '^' or '-' and '|' are not confused.
    /// </summary>
    public class OneDollarRecognizer
    {
        private const int NUM_POINTS = 32;               // Equidistant resample count
        private const float SQUARE_SIZE = 250f;          // Normalized bounding box dimension
        private const float ANGLE_RANGE_DEG = 45f;       // Angular tolerance (+/- 45 deg)
        private const float ANGLE_PRECISION_DEG = 2f;    // Golden section search angular step
        private static readonly float Phi = 0.5f * (-1.0f + Mathf.Sqrt(5.0f)); // Golden ratio ~0.6180339887

        public class Template
        {
            public string Name;
            public List<Vector2> Points;

            public Template(string name, List<Vector2> rawPoints)
            {
                Name = name;
                Points = Preprocess(rawPoints);
            }
        }

        private readonly List<Template> _templates = new List<Template>();
        private readonly float _matchScoreThreshold;

        public OneDollarRecognizer(float matchScoreThreshold = 0.70f)
        {
            _matchScoreThreshold = matchScoreThreshold;
            InitializeDefaultTemplates();
        }

        /// <summary>
        /// Populates standard Magic Touch gesture templates.
        /// </summary>
        private void InitializeDefaultTemplates()
        {
            // 1. Symbol "V"
            AddTemplate("V", new List<Vector2>
            {
                new Vector2(0f, 100f),
                new Vector2(50f, 0f),
                new Vector2(100f, 100f)
            });

            // 2. Symbol "^" (Inverted V / Caret)
            AddTemplate("^", new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(50f, 100f),
                new Vector2(100f, 0f)
            });

            // 3. Symbol "-" (Horizontal Stroke)
            AddTemplate("-", new List<Vector2>
            {
                new Vector2(0f, 50f),
                new Vector2(50f, 50f),
                new Vector2(100f, 50f)
            });

            // 4. Symbol "|" (Vertical Stroke)
            AddTemplate("|", new List<Vector2>
            {
                new Vector2(50f, 100f),
                new Vector2(50f, 50f),
                new Vector2(50f, 0f)
            });

            // 5. Symbol "Z" (Zigzag)
            AddTemplate("Z", new List<Vector2>
            {
                new Vector2(0f, 100f),
                new Vector2(100f, 100f),
                new Vector2(0f, 0f),
                new Vector2(100f, 0f)
            });

            // 6. Symbol "O" (Circle)
            List<Vector2> circle = new List<Vector2>();
            int circleSteps = 24;
            for (int i = 0; i <= circleSteps; i++)
            {
                float angle = (i / (float)circleSteps) * Mathf.PI * 2f;
                circle.Add(new Vector2(50f + Mathf.Cos(angle) * 50f, 50f + Mathf.Sin(angle) * 50f));
            }
            AddTemplate("O", circle);
        }

        public void AddTemplate(string name, List<Vector2> rawPoints)
        {
            if (rawPoints == null || rawPoints.Count < 2) return;
            _templates.Add(new Template(name, rawPoints));
        }

        /// <summary>
        /// Classifies candidate stroke points against predefined templates.
        /// </summary>
        public RecognitionResult Recognize(List<Vector2> candidateRawPoints)
        {
            if (candidateRawPoints == null || candidateRawPoints.Count < 3)
            {
                return RecognitionResult.NoMatch;
            }

            // Step 1 - 4: Resample, Scale, and Translate to origin
            List<Vector2> preprocessed = Preprocess(candidateRawPoints);

            float bestDistance = float.MaxValue;
            Template bestMatch = null;

            float angleRangeRad = ANGLE_RANGE_DEG * Mathf.Deg2Rad;
            float anglePrecisionRad = ANGLE_PRECISION_DEG * Mathf.Deg2Rad;

            for (int i = 0; i < _templates.Count; i++)
            {
                Template t = _templates[i];
                float dist = DistanceAtBestAngle(preprocessed, t.Points, -angleRangeRad, angleRangeRad, anglePrecisionRad);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestMatch = t;
                }
            }

            if (bestMatch == null)
            {
                return RecognitionResult.NoMatch;
            }

            // Wobbrock distance-to-score formula:
            // Score = 1.0 - (d / (0.5 * sqrt(2 * SQUARE_SIZE^2)))
            float halfDiagonal = 0.5f * Mathf.Sqrt(2f * SQUARE_SIZE * SQUARE_SIZE);
            float score = Mathf.Clamp01(1.0f - (bestDistance / halfDiagonal));

            return new RecognitionResult
            {
                GestureName = bestMatch.Name,
                Score = score,
                IsMatch = score >= _matchScoreThreshold
            };
        }

        #region Preprocessing Pipeline

        private static List<Vector2> Preprocess(List<Vector2> points)
        {
            List<Vector2> resampled = Resample(points, NUM_POINTS);
            List<Vector2> scaled = ScaleTo(resampled, SQUARE_SIZE);
            List<Vector2> translated = TranslateTo(scaled, Vector2.zero);
            return translated;
        }

        /// <summary>
        /// Resamples polyline into N equidistant points along total arc-length.
        /// </summary>
        private static List<Vector2> Resample(List<Vector2> points, int targetCount)
        {
            float totalLength = PathLength(points);
            float interval = totalLength / (targetCount - 1);
            float accumulatedDistance = 0f;

            List<Vector2> resampled = new List<Vector2>(targetCount) { points[0] };
            List<Vector2> workingList = new List<Vector2>(points);

            for (int i = 1; i < workingList.Count; i++)
            {
                Vector2 p0 = workingList[i - 1];
                Vector2 p1 = workingList[i];
                float segmentDistance = Vector2.Distance(p0, p1);

                if (accumulatedDistance + segmentDistance >= interval)
                {
                    float t = (interval - accumulatedDistance) / segmentDistance;
                    Vector2 newPoint = Vector2.Lerp(p0, p1, t);
                    resampled.Add(newPoint);

                    workingList.Insert(i, newPoint);
                    accumulatedDistance = 0f;
                }
                else
                {
                    accumulatedDistance += segmentDistance;
                }
            }

            // Ensure exact target count due to floating point roundings
            while (resampled.Count < targetCount)
            {
                resampled.Add(workingList[workingList.Count - 1]);
            }

            return resampled;
        }

        /// <summary>
        /// Scales stroke points non-uniformly to a standard square bounding box.
        /// Handles 1D gestures (straight horizontal/vertical lines) to avoid zero-division.
        /// </summary>
        private static List<Vector2> ScaleTo(List<Vector2> points, float size)
        {
            Rect bounds = BoundingBox(points);
            bool is1D = Mathf.Min(bounds.width / bounds.height, bounds.height / bounds.width) <= 0.2f;

            List<Vector2> scaled = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                float qx = is1D ? points[i].x * (size / Mathf.Max(bounds.width, bounds.height))
                                : (bounds.width > 0.001f ? points[i].x * (size / bounds.width) : points[i].x);

                float qy = is1D ? points[i].y * (size / Mathf.Max(bounds.width, bounds.height))
                                : (bounds.height > 0.001f ? points[i].y * (size / bounds.height) : points[i].y);

                scaled.Add(new Vector2(qx, qy));
            }
            return scaled;
        }

        /// <summary>
        /// Translates centroid of points to specified origin point.
        /// </summary>
        private static List<Vector2> TranslateTo(List<Vector2> points, Vector2 destination)
        {
            Vector2 c = Centroid(points);
            List<Vector2> translated = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                translated.Add(points[i] + (destination - c));
            }
            return translated;
        }

        #endregion

        #region Geometric Utilities & Golden Section Search

        private static float PathLength(List<Vector2> points)
        {
            float d = 0f;
            for (int i = 1; i < points.Count; i++)
            {
                d += Vector2.Distance(points[i - 1], points[i]);
            }
            return d;
        }

        private static Vector2 Centroid(List<Vector2> points)
        {
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < points.Count; i++)
            {
                sum += points[i];
            }
            return sum / points.Count;
        }

        private static Rect BoundingBox(List<Vector2> points)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].x < minX) minX = points[i].x;
                if (points[i].x > maxX) maxX = points[i].x;
                if (points[i].y < minY) minY = points[i].y;
                if (points[i].y > maxY) maxY = points[i].y;
            }

            return new Rect(minX, minY, Mathf.Max(0.0001f, maxX - minX), Mathf.Max(0.0001f, maxY - minY));
        }

        /// <summary>
        /// Rotates points around origin by radians angle theta.
        /// </summary>
        private static List<Vector2> RotateBy(List<Vector2> points, float angleRad)
        {
            Vector2 c = Centroid(points);
            float cos = Mathf.Cos(angleRad);
            float sin = Mathf.Sin(angleRad);

            List<Vector2> rotated = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                float dx = points[i].x - c.x;
                float dy = points[i].y - c.y;
                float qx = dx * cos - dy * sin + c.x;
                float qy = dx * sin + dy * cos + c.y;
                rotated.Add(new Vector2(qx, qy));
            }
            return rotated;
        }

        /// <summary>
        /// Golden Section Search to find the rotation angle with minimal average euclidean distance.
        /// Constrained to [a, b] to preserve orientation distinction between symbols.
        /// </summary>
        private static float DistanceAtBestAngle(List<Vector2> points, List<Vector2> template, float a, float b, float threshold)
        {
            float x1 = Phi * a + (1f - Phi) * b;
            float f1 = PathDistance(RotateBy(points, x1), template);
            float x2 = (1f - Phi) * a + Phi * b;
            float f2 = PathDistance(RotateBy(points, x2), template);

            while (Mathf.Abs(b - a) > threshold)
            {
                if (f1 < f2)
                {
                    b = x2;
                    x2 = x1;
                    f2 = f1;
                    x1 = Phi * a + (1f - Phi) * b;
                    f1 = PathDistance(RotateBy(points, x1), template);
                }
                else
                {
                    a = x1;
                    x1 = x2;
                    f1 = f2;
                    x2 = (1f - Phi) * a + Phi * b;
                    f2 = PathDistance(RotateBy(points, x2), template);
                }
            }

            return Mathf.Min(f1, f2);
        }

        private static float PathDistance(List<Vector2> a, List<Vector2> b)
        {
            float dist = 0f;
            int count = Mathf.Min(a.Count, b.Count);
            for (int i = 0; i < count; i++)
            {
                dist += Vector2.Distance(a[i], b[i]);
            }
            return dist / count;
        }

        #endregion
    }
}
