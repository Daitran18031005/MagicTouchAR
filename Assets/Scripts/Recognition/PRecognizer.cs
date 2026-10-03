using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicTouch.Recognition
{
    [Serializable]
    public struct PRecognitionResult
    {
        public GestureSymbol Symbol;
        public string MatchedVariant;
        public float Score;     // Normalized confidence [0.0 .. 1.0]
        public float Distance;  // Raw average cloud distance
        public bool IsMatch;

        public static PRecognitionResult NoMatch => new PRecognitionResult
        {
            Symbol = GestureSymbol.None,
            MatchedVariant = "None",
            Score = 0f,
            Distance = float.MaxValue,
            IsMatch = false
        };
    }

    /// <summary>
    /// Implementation of the $P Point-Cloud Recognizer (Vatavu, Anthony, Wobbrock - ICMI 2012)
    /// upgraded with:
    /// 1. Uniform / Proportional Scaling for all shapes (preserves true aspect ratio).
    /// 2. High-fidelity resampling (N = 64 points) preserving sharp corners in W, Z, Caret, etc.
    /// 3. Multi-Template Nearest-Neighbor (1-NN / k-NN) supporting 3-5 variations per symbol.
    /// 4. Calibrated, forgiving distance-to-score normalization for human hand-drawing variation.
    /// </summary>
    public class PRecognizer
    {
        // High-fidelity point count to preserve sharp corners
        public const int NUM_POINTS = 64;

        public class GestureTemplate
        {
            public GestureSymbol Symbol;
            public string VariantTag;
            public Point[] Points;

            public GestureTemplate(GestureSymbol symbol, Point[] rawPoints, string variantTag = "default")
            {
                Symbol = symbol;
                VariantTag = string.IsNullOrEmpty(variantTag) ? "default" : variantTag;
                Points = PRecognizer.NormalizePointCloud(rawPoints, NUM_POINTS);
            }

            public GestureTemplate(GestureSymbol symbol, Point[] preNormalizedPoints, string variantTag, bool isPreNormalized)
            {
                Symbol = symbol;
                VariantTag = string.IsNullOrEmpty(variantTag) ? "default" : variantTag;
                Points = preNormalizedPoints;
            }
        }

        private readonly List<GestureTemplate> _templates = new List<GestureTemplate>();
        private readonly float _matchThreshold;
        private readonly float _distanceNormalizationFactor;

        public int TotalTemplatesCount => _templates.Count;

        /// <summary>
        /// Constructor for $P Recognizer.
        /// </summary>
        /// <param name="matchThreshold">Minimum normalized score (0.0 to 1.0) to register a match (default: 0.60f).</param>
        /// <param name="distanceNormalizationFactor">Distance scale factor for score normalization (default: 0.85f).</param>
        public PRecognizer(float matchThreshold = 0.60f, float distanceNormalizationFactor = 0.85f)
        {
            _matchThreshold = matchThreshold;
            _distanceNormalizationFactor = distanceNormalizationFactor;
            InitializeDefaultTemplates();
            // ==========================================================================
            // Template: GestureSymbol.V | Variant: "var20" (32 Points)
            // ==========================================================================
            AddTemplate(GestureSymbol.V, new Point[]
            {
                new Point(-0.2669f, 0.4411f, 0),
                new Point(-0.2654f, 0.3733f, 0),
                new Point(-0.2542f, 0.3062f, 0),
                new Point(-0.2430f, 0.2392f, 0),
                new Point(-0.2307f, 0.1724f, 0),
                new Point(-0.2174f, 0.1058f, 0),
                new Point(-0.2033f, 0.0394f, 0),
                new Point(-0.1873f, -0.0266f, 0),
                new Point(-0.1673f, -0.0915f, 0),
                new Point(-0.1484f, -0.1568f, 0),
                new Point(-0.1277f, -0.2214f, 0),
                new Point(-0.1015f, -0.2841f, 0),
                new Point(-0.0694f, -0.3439f, 0),
                new Point(-0.0341f, -0.4019f, 0),
                new Point(0.0032f, -0.4587f, 0),
                new Point(0.0418f, -0.5090f, 0),
                new Point(0.0590f, -0.4435f, 0),
                new Point(0.0746f, -0.3776f, 0),
                new Point(0.0906f, -0.3117f, 0),
                new Point(0.1039f, -0.2451f, 0),
                new Point(0.1204f, -0.1792f, 0),
                new Point(0.1358f, -0.1130f, 0),
                new Point(0.1483f, -0.0463f, 0),
                new Point(0.1579f, 0.0210f, 0),
                new Point(0.1741f, 0.0869f, 0),
                new Point(0.1844f, 0.1539f, 0),
                new Point(0.1912f, 0.2215f, 0),
                new Point(0.1948f, 0.2892f, 0),
                new Point(0.2080f, 0.3553f, 0),
                new Point(0.2095f, 0.4231f, 0),
                new Point(0.2095f, 0.4910f, 0),
                new Point(0.2095f, 0.4910f, 0)
            }, "var20");

        }

        #region Template Management (Multi-Template Nearest Neighbor)

        public void AddTemplate(GestureSymbol symbol, Point[] rawPoints, string variantTag = "default")
        {
            if (rawPoints == null || rawPoints.Length < 2) return;
            _templates.Add(new GestureTemplate(symbol, rawPoints, variantTag));
        }

        public void AddPreNormalizedTemplate(GestureSymbol symbol, Point[] normalizedPoints, string variantTag = "default")
        {
            if (normalizedPoints == null || normalizedPoints.Length == 0) return;
            _templates.Add(new GestureTemplate(symbol, normalizedPoints, variantTag, true));
        }

        public void ClearTemplates(GestureSymbol symbol)
        {
            _templates.RemoveAll(t => t.Symbol == symbol);
        }

        public void ClearAllTemplates()
        {
            _templates.Clear();
        }

        public int GetTemplateCount(GestureSymbol symbol)
        {
            int count = 0;
            for (int i = 0; i < _templates.Count; i++)
            {
                if (_templates[i].Symbol == symbol) count++;
            }
            return count;
        }

        #endregion

        /// <summary>
        /// Compares the candidate stroke against all template variations using Multi-Template 1-NN.
        /// Returns the GestureSymbol and variant tag with the highest matching confidence score.
        /// </summary>
        public PRecognitionResult Recognize(Point[] candidatePoints)
        {
            if (candidatePoints == null || candidatePoints.Length < 4 || _templates.Count == 0)
            {
                return PRecognitionResult.NoMatch;
            }

            // Step 1: Uniform Resample (N=64), Uniform Scale, and Centroid Translation
            Point[] normalizedCandidate = NormalizePointCloud(candidatePoints, NUM_POINTS);

            float bestDistance = float.MaxValue;
            GestureTemplate bestMatch = null;

            // Multi-Template Nearest-Neighbor Search: Compare across all variations of all symbols
            for (int i = 0; i < _templates.Count; i++)
            {
                GestureTemplate template = _templates[i];
                float dist = GreedyCloudMatch(normalizedCandidate, template.Points);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestMatch = template;
                }
            }

            if (bestMatch == null)
            {
                return PRecognitionResult.NoMatch;
            }

            // Calibrated, forgiving distance-to-score normalization:
            // Hand drawings naturally exhibit micro-jitter and speed variations.
            // Using a calibrated denominator (0.85f) yields a smooth, reliable [0.0, 1.0] score.
            float score = Mathf.Clamp01(1.0f - (bestDistance / _distanceNormalizationFactor));

            return new PRecognitionResult
            {
                Symbol = bestMatch.Symbol,
                MatchedVariant = bestMatch.VariantTag,
                Score = score,
                Distance = bestDistance,
                IsMatch = score >= _matchThreshold
            };
        }

        #region Preprocessing ($P Resample, Uniform Proportional Scale, Translate)

        public static Point[] NormalizePointCloud(Point[] rawPoints, int targetPointCount = NUM_POINTS)
        {
            Point[] resampled = Resample(rawPoints, targetPointCount);
            Point[] scaled = ScaleUniform(resampled);
            Point[] translated = TranslateToOrigin(scaled);
            return translated;
        }

        /// <summary>
        /// Multi-stroke aware high-fidelity resampling (N = 64):
        /// Allocates points across strokes proportional to stroke arc-length, preserving corners and endpoints.
        /// </summary>
        private static Point[] Resample(Point[] points, int n)
        {
            if (points == null || points.Length == 0) return Array.Empty<Point>();

            // Group points by stroke ID
            Dictionary<int, List<Point>> strokes = new Dictionary<int, List<Point>>();
            for (int i = 0; i < points.Length; i++)
            {
                int sid = points[i].StrokeID;
                if (!strokes.ContainsKey(sid))
                {
                    strokes[sid] = new List<Point>();
                }
                strokes[sid].Add(points[i]);
            }

            // Calculate stroke arc lengths
            float totalLength = 0f;
            Dictionary<int, float> lengths = new Dictionary<int, float>();
            foreach (var kvp in strokes)
            {
                float len = StrokeLength(kvp.Value);
                lengths[kvp.Key] = len;
                totalLength += len;
            }

            List<Point> result = new List<Point>(n);

            if (totalLength <= 0.0001f)
            {
                for (int i = 0; i < n; i++)
                {
                    result.Add(points[Mathf.Min(i, points.Length - 1)]);
                }
                return result.ToArray();
            }

            // Distribute points proportionally across strokes
            int strokesProcessed = 0;
            int strokesTotal = strokes.Count;

            foreach (var kvp in strokes)
            {
                int sid = kvp.Key;
                List<Point> strokePts = kvp.Value;
                if (strokePts.Count == 0) continue;

                strokesProcessed++;

                int strokePointsCount;
                if (strokesProcessed == strokesTotal)
                {
                    // Last stroke receives all remaining quota to ensure exact N count
                    strokePointsCount = Mathf.Max(2, n - result.Count);
                }
                else
                {
                    float proportion = lengths[sid] / totalLength;
                    strokePointsCount = Mathf.Max(2, Mathf.RoundToInt(proportion * n));
                }

                float strokeLen = lengths[sid];
                float interval = strokeLen / Mathf.Max(1, strokePointsCount - 1);
                float accumulated = 0f;

                result.Add(strokePts[0]);
                for (int i = 1; i < strokePts.Count; i++)
                {
                    Point p0 = strokePts[i - 1];
                    Point p1 = strokePts[i];
                    float segDist = Point.Distance(p0, p1);

                    if (accumulated + segDist >= interval)
                    {
                        float t = (interval - accumulated) / Mathf.Max(segDist, 0.0001f);
                        Point newP = new Point(Mathf.Lerp(p0.X, p1.X, t), Mathf.Lerp(p0.Y, p1.Y, t), sid);
                        result.Add(newP);
                        strokePts.Insert(i, newP);
                        accumulated = 0f;
                    }
                    else
                    {
                        accumulated += segDist;
                    }
                }
            }

            // Ensure exact target count
            while (result.Count < n && result.Count > 0)
            {
                result.Add(result[result.Count - 1]);
            }
            if (result.Count > n)
            {
                result.RemoveRange(n, result.Count - n);
            }

            return result.ToArray();
        }

        private static float StrokeLength(List<Point> stroke)
        {
            float d = 0f;
            for (int i = 1; i < stroke.Count; i++)
            {
                d += Point.Distance(stroke[i - 1], stroke[i]);
            }
            return d;
        }

        /// <summary>
        /// Uniform / Proportional Scaling: Scales points uniformly by max(width, height)
        /// to preserve the exact geometric aspect ratio of the user's drawn shape.
        /// </summary>
        private static Point[] ScaleUniform(Point[] points)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].X < minX) minX = points[i].X;
                if (points[i].X > maxX) maxX = points[i].X;
                if (points[i].Y < minY) minY = points[i].Y;
                if (points[i].Y > maxY) maxY = points[i].Y;
            }

            float width = maxX - minX;
            float height = maxY - minY;
            float maxDim = Mathf.Max(width, height);
            if (maxDim <= 0.0001f) maxDim = 1f;

            // Uniform division by maxDim preserves aspect ratio exactly
            Point[] scaled = new Point[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                float sx = (points[i].X - minX) / maxDim;
                float sy = (points[i].Y - minY) / maxDim;
                scaled[i] = new Point(sx, sy, points[i].StrokeID);
            }

            return scaled;
        }

        private static Point[] TranslateToOrigin(Point[] points)
        {
            float sumX = 0f, sumY = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                sumX += points[i].X;
                sumY += points[i].Y;
            }

            float cx = sumX / points.Length;
            float cy = sumY / points.Length;

            Point[] translated = new Point[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                translated[i] = new Point(points[i].X - cx, points[i].Y - cy, points[i].StrokeID);
            }

            return translated;
        }

        #endregion

        #region $P Greedy Cloud Matching Algorithm

        public static float GreedyCloudMatch(Point[] points, Point[] templatePoints)
        {
            float e = 0.50f;
            int step = Mathf.FloorToInt(Mathf.Pow(points.Length, 1f - e));
            if (step < 1) step = 1;

            float minDistance = float.MaxValue;
            for (int i = 0; i < points.Length; i += step)
            {
                float d1 = CloudDistance(points, templatePoints, i);
                float d2 = CloudDistance(templatePoints, points, i);
                minDistance = Mathf.Min(minDistance, Mathf.Min(d1, d2));
            }
            return minDistance;
        }

        private static float CloudDistance(Point[] pts1, Point[] pts2, int startIndex)
        {
            int n = pts1.Length;
            bool[] matched = new bool[n];
            float sum = 0f;
            int i = startIndex;

            do
            {
                int index = -1;
                float minDistance = float.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (!matched[j])
                    {
                        float dist = Point.SqrDistance(pts1[i], pts2[j]);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            index = j;
                        }
                    }
                }

                if (index >= 0)
                {
                    matched[index] = true;
                    float weight = 1f - ((i - startIndex + n) % n) / (float)n;
                    sum += weight * Mathf.Sqrt(minDistance);
                }

                i = (i + 1) % n;
            } while (i != startIndex);

            return sum;
        }

        #endregion

        #region Baseline Seed Templates (64 Points, Proportional Ratio)

        private void InitializeDefaultTemplates()
        {
            // 1. V
            AddTemplate(GestureSymbol.V, new Point[]
            {
                new Point(0.0f, 1.0f, 0), new Point(0.5f, 0.0f, 0), new Point(1.0f, 1.0f, 0)
            }, "v_seed");

            // 2. Caret (^)
            AddTemplate(GestureSymbol.Caret, new Point[]
            {
                new Point(0.0f, 0.0f, 0), new Point(0.5f, 1.0f, 0), new Point(1.0f, 0.0f, 0)
            }, "caret_seed");

            // 3. HorizontalLine (-)
            AddTemplate(GestureSymbol.HorizontalLine, new Point[]
            {
                new Point(0.0f, 0.5f, 0), new Point(1.0f, 0.5f, 0)
            }, "hline_seed");

            // 4. VerticalLine (|)
            AddTemplate(GestureSymbol.VerticalLine, new Point[]
            {
                new Point(0.5f, 0.0f, 0), new Point(0.5f, 1.0f, 0)
            }, "vline_seed");

            // 5. Teardrop
            AddTemplate(GestureSymbol.Teardrop, GenerateTeardropPoints(), "teardrop_seed");

            // 6. O (Circle)
            AddTemplate(GestureSymbol.O, GenerateCirclePoints(), "circle_seed");

            // 7. Z
            AddTemplate(GestureSymbol.Z, new Point[]
            {
                new Point(0.0f, 1.0f, 0), new Point(1.0f, 1.0f, 0),
                new Point(0.0f, 0.0f, 0), new Point(1.0f, 0.0f, 0)
            }, "z_seed");

            // 8. VerticalLoop (Ribbon loop)
            AddTemplate(GestureSymbol.VerticalLoop, GenerateVerticalLoopPoints(), "vloop_seed");

            // 9. PretzelKnot
            AddTemplate(GestureSymbol.PretzelKnot, GeneratePretzelPoints(), "pretzel_seed");

            // 10. W
            AddTemplate(GestureSymbol.W, new Point[]
            {
                new Point(0.0f, 1.0f, 0), new Point(0.25f, 0.0f, 0),
                new Point(0.5f, 0.8f, 0), new Point(0.75f, 0.0f, 0),
                new Point(1.0f, 1.0f, 0)
            }, "w_seed");

            // 11. Figure8
            AddTemplate(GestureSymbol.Figure8, GenerateFigure8Points(), "figure8_seed");

            // 12. E
            AddTemplate(GestureSymbol.E, new Point[]
            {
                new Point(0.9f, 1.0f, 0), new Point(0.2f, 1.0f, 0),
                new Point(0.2f, 0.5f, 0), new Point(0.75f, 0.5f, 0),
                new Point(0.2f, 0.5f, 0), new Point(0.2f, 0.0f, 0),
                new Point(0.9f, 0.0f, 0)
            }, "e_seed");

            // 13. X (Multi-stroke: Stroke 0 and Stroke 1)
            AddTemplate(GestureSymbol.X, new Point[]
            {
                new Point(0.0f, 1.0f, 0), new Point(1.0f, 0.0f, 0),
                new Point(1.0f, 1.0f, 1), new Point(0.0f, 0.0f, 1)
            }, "x_seed");

            // 14. SwirlG
            AddTemplate(GestureSymbol.SwirlG, GenerateSwirlGPoints(), "swirlg_seed");

            // 15. Checkmark
            AddTemplate(GestureSymbol.Checkmark, new Point[]
            {
                new Point(0.0f, 0.4f, 0), new Point(0.35f, 0.0f, 0), new Point(1.0f, 1.0f, 0)
            }, "check_seed");

            // 16. S
            AddTemplate(GestureSymbol.S, new Point[]
            {
                new Point(0.9f, 0.9f, 0), new Point(0.2f, 1.0f, 0),
                new Point(0.1f, 0.6f, 0), new Point(0.9f, 0.4f, 0),
                new Point(0.8f, 0.0f, 0), new Point(0.1f, 0.1f, 0)
            }, "s_seed");
        }

        private static Point[] GenerateCirclePoints()
        {
            Point[] pts = new Point[32];
            for (int i = 0; i < 32; i++)
            {
                float a = (i / 32f) * Mathf.PI * 2f;
                pts[i] = new Point(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f, 0);
            }
            return pts;
        }

        private static Point[] GenerateTeardropPoints()
        {
            return new Point[]
            {
                new Point(0.35f, 0.9f, 0), new Point(0.65f, 0.9f, 0), new Point(0.85f, 0.4f, 0),
                new Point(0.5f, 0.05f, 0), new Point(0.15f, 0.4f, 0), new Point(0.35f, 0.9f, 0),
                new Point(0.5f, 1.0f, 0)
            };
        }

        private static Point[] GenerateVerticalLoopPoints()
        {
            return new Point[]
            {
                new Point(0.5f, 0.0f, 0), new Point(0.5f, 0.5f, 0), new Point(0.85f, 0.75f, 0),
                new Point(0.5f, 1.0f, 0), new Point(0.2f, 0.75f, 0), new Point(0.5f, 0.5f, 0),
                new Point(0.5f, 0.1f, 0)
            };
        }

        private static Point[] GenerateFigure8Points()
        {
            Point[] pts = new Point[32];
            for (int i = 0; i < 32; i++)
            {
                float t = (i / 32f) * Mathf.PI * 2f;
                float x = 0.5f + Mathf.Sin(t) * 0.4f;
                float y = 0.5f + Mathf.Sin(2f * t) * 0.45f;
                pts[i] = new Point(x, y, 0);
            }
            return pts;
        }

        private static Point[] GeneratePretzelPoints()
        {
            return new Point[]
            {
                new Point(0.5f, 0.1f, 0), new Point(0.1f, 0.7f, 0), new Point(0.3f, 1.0f, 0),
                new Point(0.5f, 0.4f, 0), new Point(0.7f, 1.0f, 0), new Point(0.9f, 0.7f, 0),
                new Point(0.5f, 0.1f, 0)
            };
        }

        private static Point[] GenerateSwirlGPoints()
        {
            return new Point[]
            {
                new Point(0.85f, 0.85f, 0), new Point(0.2f, 0.85f, 0), new Point(0.1f, 0.3f, 0),
                new Point(0.6f, 0.05f, 0), new Point(0.85f, 0.4f, 0), new Point(0.45f, 0.4f, 0)
            };
        }

        #endregion
    }
}
