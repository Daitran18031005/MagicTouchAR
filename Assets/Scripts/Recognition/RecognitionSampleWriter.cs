using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MagicTouch.Recognition
{
    public static class RecognitionSampleWriter
    {
        [Serializable]
        private class Sample
        {
            public int schemaVersion = 1;
            public string utc;
            public string label;
            public string coordinateSystem = "screen-y-up; pixels are row-major top-down";
            public string modelSha256;
            public float brushRadius;
            public Point[] points;
            public float[] pixels;
            public string[] classNames;
            public float[] probabilities;
        }

        public static string Save(IReadOnlyList<Point> points, GestureSymbol label, float radius,
            float[] probabilities, string modelSha256, string directory = null)
        {
            if (points == null || points.Count < 3) throw new ArgumentException("Cần vẽ ít nhất một nét.");
            if (label == GestureSymbol.None) throw new ArgumentException("Chọn nhãn đúng trước khi lưu.");
            if (directory == null)
                directory = Application.isEditor
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Related/Data-Samples"))
                    : Path.Combine(Application.persistentDataPath, "RecognitionSamples");
            Directory.CreateDirectory(directory);
            var copy = new Point[points.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = points[i];
            var labels = new string[SentisRecognizer.ClassToSymbol.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = SentisRecognizer.ClassToSymbol[i].ToString();
            var sample = new Sample {
                utc = DateTime.UtcNow.ToString("O"), label = label.ToString(), modelSha256 = modelSha256,
                brushRadius = radius, points = copy, pixels = GesturePreprocessor.RasterizeTo28x28(points, radius),
                classNames = labels, probabilities = probabilities
            };
            string stem = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "_" + label + "_" + Guid.NewGuid().ToString("N"));
            // A unique name never replaces an earlier sample. JSON retains exact float inputs.
            File.WriteAllText(stem + ".json", JsonUtility.ToJson(sample, true));
            Texture2D texture = GesturePreprocessor.CreateDebugTexture(sample.pixels);
            try { File.WriteAllBytes(stem + ".png", texture.EncodeToPNG()); }
            finally
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
            }
            return stem + ".json";
        }
    }
}
