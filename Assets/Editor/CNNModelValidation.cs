using System;
using System.IO;
using System.Linq;
using MagicTouch.Recognition;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

namespace MagicTouch.EditorTools
{
    // Explicit diagnostics only: no automatic execution, scene edits or menus.
    public static class CNNModelValidation
    {
        [Serializable] public class FixtureSet { public Fixture[] cases; }
        [Serializable] public class Fixture
        {
            public string name;
            public int label;
            public Point[] points;
            public float[] pixels;
            public float[] logits;
        }

        public static string Validate(string directory)
        {
            var fixtures = JsonUtility.FromJson<FixtureSet>(File.ReadAllText(Path.Combine(directory, "unity-fixtures.json")));
            var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/Models/magic_touch_model.onnx");
            using var worker = new Worker(ModelLoader.Load(asset), BackendType.CPU);
            float maxPixelError = 0, maxLogitError = 0;
            foreach (var f in fixtures.cases)
            {
                var pixels = GesturePreprocessor.RasterizeTo28x28(f.points);
                for (int i = 0; i < pixels.Length; i++) maxPixelError = Mathf.Max(maxPixelError, Mathf.Abs(pixels[i] - f.pixels[i]));
                using var input = new Tensor<float>(new TensorShape(1, 1, 28, 28), pixels);
                worker.Schedule(input);
                using var output = worker.PeekOutput().ReadbackAndClone() as Tensor<float>;
                var logits = output.DownloadToArray();
                for (int i = 0; i < logits.Length; i++) maxLogitError = Mathf.Max(maxLogitError, Mathf.Abs(logits[i] - f.logits[i]));
                int winner = Array.IndexOf(logits, logits.Max());
                if (winner != f.label) throw new Exception(f.name + " classified as " + SentisRecognizer.ClassToSymbol[winner]);
                if (f.name.StartsWith("caret") && GestureCompletionPolicy.HasRequiredStructure(f.points, GestureSymbol.O))
                    throw new Exception("O closure gate accepted open caret: " + f.name);
                if (f.name == "O" && !GestureCompletionPolicy.HasRequiredStructure(f.points, GestureSymbol.O))
                    throw new Exception("Closed O rejected.");
                if (f.name == "E_missing_middle" && GestureCompletionPolicy.HasRequiredStructure(f.points, GestureSymbol.E))
                    throw new Exception("Incomplete E accepted.");
            }
            if (maxPixelError > .0001f || maxLogitError > .003f)
                throw new Exception($"Parity failed: pixels={maxPixelError}, logits={maxLogitError}");
            var sample = fixtures.cases.First(f => f.name == "caret_two_strokes");
            string saved = RecognitionSampleWriter.Save(sample.points, GestureSymbol.Caret, 1.3f, null,
                "validation-only", Path.Combine(directory, "sample-export-check"));
            string json = File.ReadAllText(saved);
            if (!json.Contains("Caret") || !json.Contains("StrokeID") || !File.Exists(Path.ChangeExtension(saved, ".png")))
                throw new Exception("Sample export incomplete.");
            // Reversed and out-of-order halves of a circle should still join into one loop.
            var circle = fixtures.cases.First(f => f.name == "O").points;
            var multi = circle.Skip(40).Reverse().Select(p => new Point(p.X,p.Y,0))
                .Concat(circle.Take(41).Select(p => new Point(p.X,p.Y,1))).ToArray();
            if (!GestureCompletionPolicy.HasClosedLoop(multi)) throw new Exception("Two-stroke O rejected.");
            return $"PASS {fixtures.cases.Length} fixed model cases, O/E gates, two-stroke O, JSON+PNG save; max pixel error={maxPixelError:G4}, max logit error={maxLogitError:G4}";
        }
    }
}
