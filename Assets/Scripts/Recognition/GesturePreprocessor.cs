using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicTouch.Recognition
{
    /// <summary>
    /// Rasterizes raw screen stroke points into a 28x28 grayscale image float array
    /// for MobileMagicCNN ONNX inference.
    /// Enforces strict Uniform Scaling, Absolute Bounding Box calculation, and Center Padding
    /// to preserve true geometric aspect ratio without distortion.
    /// </summary>
    public static class GesturePreprocessor
    {
        public const int GRID_SIZE = 28;
        public const int TOTAL_PIXELS = GRID_SIZE * GRID_SIZE; // 784

        // The longest dimension of the stroke is scaled to fit within 20 pixels,
        // guaranteeing ~4 pixels of empty black padding around the border.
        public const float TARGET_STROKE_AREA = 20.0f;

        public struct StrokeBounds
        {
            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;
            public float Width;
            public float Height;
            public float Diagonal;
            public Vector2 Center;
        }

        /// <summary>
        /// Calculates absolute physical bounds across all points regardless of drawing direction.
        /// </summary>
        public static StrokeBounds CalculateBounds(IReadOnlyList<Point> points)
        {
            if (points == null || points.Count == 0)
            {
                return default;
            }

            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;

            for (int i = 0; i < points.Count; i++)
            {
                float px = points[i].X;
                float py = points[i].Y;

                if (px < minX) minX = px;
                if (px > maxX) maxX = px;
                if (py < minY) minY = py;
                if (py > maxY) maxY = py;
            }

            float w = Mathf.Abs(maxX - minX);
            float h = Mathf.Abs(maxY - minY);

            return new StrokeBounds
            {
                MinX = minX,
                MaxX = maxX,
                MinY = minY,
                MaxY = maxY,
                Width = w,
                Height = h,
                Diagonal = Mathf.Sqrt(w * w + h * h),
                Center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f)
            };
        }

        /// <summary>
        /// Rasterizes a list of stroke points into a normalized 28x28 float array (values in [0.0, 1.0]).
        /// Performs Uniform Scaling based on the longest dimension and pads the shorter dimension equally.
        /// </summary>
        /// <param name="points">Raw screen stroke points tagged with StrokeID.</param>
        /// <param name="brushRadius">Effective pixel radius for stroke thickness on 28x28 grid (default: 1.3f).</param>
        /// <returns>Flattened 1D float array of size 784.</returns>
        public static float[] RasterizeTo28x28(IReadOnlyList<Point> points, float brushRadius = 1.3f)
        {
            if (points == null || points.Count == 0)
            {
                return null;
            }

            StrokeBounds bounds = CalculateBounds(points);

            // 1. Uniform Scaling: Scale by the LONGEST dimension to preserve authentic aspect ratio
            float maxDim = Mathf.Max(bounds.Width, bounds.Height);
            if (maxDim < 1.0f)
            {
                maxDim = 1.0f; // Guard against divide by zero
            }

            float scale = TARGET_STROKE_AREA / maxDim;

            // 2. Center Padding: Grid center is at (14.0f, 14.0f).
            // Shorter dimension is automatically padded with black pixels equally on both sides.
            const float gridCenter = 14.0f;

            Vector2[] gridPoints = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                // Uniform proportional mapping
                float gx = gridCenter + (points[i].X - bounds.Center.x) * scale;
                // Invert Y: Screen space bottom is Y=0, while 28x28 image top is row=0
                float gy = gridCenter - (points[i].Y - bounds.Center.y) * scale;

                gridPoints[i] = new Vector2(gx, gy);
            }

            float[] grid = new float[TOTAL_PIXELS];

            // 3. Rasterize strokes with anti-aliased circular brush
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 current = gridPoints[i];

                if (i > 0 && points[i].StrokeID == points[i - 1].StrokeID)
                {
                    // Draw connected line segment within the same stroke
                    Vector2 prev = gridPoints[i - 1];
                    RasterizeLine(grid, prev, current, brushRadius);
                }
                else
                {
                    // Single point or start of a new sub-stroke
                    StampBrush(grid, current.x, current.y, brushRadius, 1.0f);
                }
            }

            return grid;
        }

        /// <summary>
        /// Interpolates line segments and stamps anti-aliased brush along the path.
        /// </summary>
        private static void RasterizeLine(float[] grid, Vector2 p0, Vector2 p1, float radius)
        {
            float dist = Vector2.Distance(p0, p1);
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist * 2.5f)); // Sub-pixel step density

            for (int step = 0; step <= steps; step++)
            {
                float t = step / (float)steps;
                float x = Mathf.Lerp(p0.x, p1.x, t);
                float y = Mathf.Lerp(p0.y, p1.y, t);
                StampBrush(grid, x, y, radius, 1.0f);
            }
        }

        /// <summary>
        /// Stamps an anti-aliased circular splat onto the 28x28 grid.
        /// </summary>
        private static void StampBrush(float[] grid, float cx, float cy, float radius, float intensity)
        {
            int minR = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
            int maxR = Mathf.Min(GRID_SIZE - 1, Mathf.CeilToInt(cy + radius));
            int minC = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
            int maxC = Mathf.Min(GRID_SIZE - 1, Mathf.CeilToInt(cx + radius));

            float rSqr = radius * radius;

            for (int r = minR; r <= maxR; r++)
            {
                for (int c = minC; c <= maxC; c++)
                {
                    float dx = c - cx;
                    float dy = r - cy;
                    float dSqr = dx * dx + dy * dy;

                    if (dSqr <= rSqr)
                    {
                        // Soft quadratic falloff for anti-aliasing
                        float alpha = 1.0f - (dSqr / rSqr);
                        float val = intensity * alpha;
                        int idx = r * GRID_SIZE + c;

                        // Maximum blend (like ink on canvas)
                        if (val > grid[idx])
                        {
                            grid[idx] = Mathf.Min(1.0f, val);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a Texture2D from the 28x28 float array for debugging in Unity Inspector.
        /// </summary>
        public static Texture2D CreateDebugTexture(float[] grid)
        {
            if (grid == null || grid.Length != TOTAL_PIXELS) return null;

            Texture2D tex = new Texture2D(GRID_SIZE, GRID_SIZE, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            Color[] colors = new Color[TOTAL_PIXELS];

            for (int r = 0; r < GRID_SIZE; r++)
            {
                for (int c = 0; c < GRID_SIZE; c++)
                {
                    // Invert row index for Texture2D (texture (0,0) is bottom-left)
                    int gridIdx = (GRID_SIZE - 1 - r) * GRID_SIZE + c;
                    float v = grid[gridIdx];
                    colors[r * GRID_SIZE + c] = new Color(v, v, v, 1.0f);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();
            return tex;
        }
    }
}
