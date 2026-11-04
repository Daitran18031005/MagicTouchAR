using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.InferenceEngine;
using MagicTouch.Recognition;
using MagicTouch.Tracking;
using Debug = UnityEngine.Debug;

// Edit Mode checks for the streamer pinch protocol and packet timing, plus recognizer cost and
// an optional air-drawing benchmark (Train/air_stroke_benchmark.py). Never opens or saves a scene.
// Batch: Unity -batchmode -quit -projectPath <root> -executeMethod TrackingStreamChecks.RunBatch [-airStrokes <json>]
// (a failed check throws, which makes Unity exit with code 1).
public static class TrackingStreamChecks
{
    private const string ResultPath = "Logs/tracking-stream-checks.json";
    private const string DefaultBenchmark = "Logs/air-strokes.json";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable] private class BenchmarkSet { public string[] classNames; public BenchmarkSample[] samples; }
    [Serializable] private class BenchmarkSample { public int label; public int fps; public string noisy; public Point[] points; }
    [Serializable] private class BackendTiming { public string requested, actual; public float meanMs, p50Ms, p95Ms; public int calls; }
    [Serializable] private class ConditionResult
    {
        public string noisy; public int fps, symbols, unknowns;
        public float correct, wrong, rejected, tooFewPoints, earlyCommitted, unknownAccepted;
    }
    [Serializable] private class Report
    {
        public string result, benchmark;
        public float maxProbabilityDifference;
        public BackendTiming[] backends;
        public ConditionResult[] conditions;
    }

    private static void Require(bool ok, string message) { if (!ok) throw new Exception("Tracking stream regression: " + message); }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetField(name, Flags).GetValue(target);

    [MenuItem("Tools/MagicTouch/Validate Tracking Stream")]
    public static void Run()
    {
        CheckStreamProtocol();
        var report = new Report { result = "PASS" };
        MeasureRecognizer(report);
        Directory.CreateDirectory("Logs");
        File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
        Debug.Log($"[TrackingStream] PASS: pinchPending protocol, coalesced release, stale-packet timeout; recognizer timing and benchmark in {ResultPath}.");
    }

    public static void RunBatch()
    {
        Run();
        InteractionRepairChecks.Run();
        DrawingRepairChecks.RunChecks();
    }

    private static void CheckStreamProtocol()
    {
        // Wire format: the optional field parses, and older streamers default to "not pending".
        const string withPending = "{\"sessionId\":\"\",\"timestamp\":5,\"hands\":[{\"index\":0,\"pinch\":true,\"pinchPending\":true,\"pinchVersion\":1,\"releaseCount\":3}]}";
        const string legacy = "{\"sessionId\":\"\",\"timestamp\":5,\"hands\":[{\"index\":0,\"pinch\":true,\"pinchVersion\":1}]}";
        Require(JsonUtility.FromJson<MediaPipeUDPReceiver.MediaPipePacket>(withPending).hands[0].pinchPending, "receiver ignored pinchPending");
        Require(!JsonUtility.FromJson<MediaPipeUDPReceiver.MediaPipePacket>(legacy).hands[0].pinchPending, "legacy packet became pending");
        var packetType = typeof(HandTrackingBridge).GetNestedType("UdpPacket", BindingFlags.NonPublic);
        object packet = JsonUtility.FromJson(withPending, packetType);
        object hand = ((Array)packetType.GetField("hands").GetValue(packet)).GetValue(0);
        Require((bool)hand.GetType().GetField("pinchPending").GetValue(hand), "bridge ignored pinchPending");

        Require(Mathf.Approximately(HandTrackingBridge.StalePacketTimeout(1f / 30f), 0.25f), "normal packet rate must keep the 0.25 s timeout");
        Require(Mathf.Approximately(HandTrackingBridge.StalePacketTimeout(0.2f), 0.6f), "5 FPS tracker must not drop hands between packets");
        Require(Mathf.Approximately(HandTrackingBridge.StalePacketTimeout(2f), 0.75f), "stale timeout must stay bounded");

        var root = new GameObject("TrackingStreamChecks") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false); // No Awake: no UDP socket, no Python launch, no singleton.
        try
        {
            var bridge = root.AddComponent<HandTrackingBridge>();
            var landmarks = new Vector2[21];
            for (int i = 0; i < 21; i++) landmarks[i] = new Vector2(.5f, .5f);
            long t = 1000;
            void Feed(bool pinch, bool pending, int releases)
                => bridge.ProcessHandLandmarks(0, landmarks, t += 33, 7, pinch, releases, .1f, pending);
            Feed(true, false, 0);
            Require(bridge.GetHand(0).IsPinching && bridge.GetHand(0).IsDrawing, "stream pinch did not start ink");
            Feed(true, true, 0);
            Require(bridge.GetHand(0).IsPinchReleasePending && !bridge.GetHand(0).IsDrawing, "pending release must freeze ink");
            Feed(true, false, 0);
            Require(bridge.GetHand(0).IsDrawing && bridge.GetHand(0).ReleaseCount == 0, "noise recovery must resume the same stroke");
            Feed(false, false, 1);
            Require(!bridge.GetHand(0).IsPinching && !bridge.GetHand(0).IsDrawing && bridge.GetHand(0).ReleaseCount == 1, "confirmed release");
            Feed(true, false, 2); // OPEN packet coalesced away: the counter still marks the pen-up.
            Require(bridge.GetHand(0).IsDrawing && bridge.GetHand(0).ReleaseCount == 2, "coalesced release lost");
            Feed(false, true, 2);
            Require(!bridge.GetHand(0).IsPinchReleasePending, "an open hand cannot be pending");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static SentisRecognizer CreateRecognizer(GameObject root, BackendType backend, bool keepDefaults = false)
    {
        var recognizer = root.AddComponent<SentisRecognizer>();
        Set(recognizer, "_modelAsset", AssetDatabase.LoadAssetAtPath<ModelAsset>("Assets/Models/magic_touch_model.onnx"));
        if (!keepDefaults) Set(recognizer, "_preferCpuInference", false); // Measure exactly the requested backend.
        Set(recognizer, "_backendType", backend);
        Set(recognizer, "_logDetailedProbabilities", false);
        typeof(SentisRecognizer).GetMethod("InitializeSentisWorker", Flags).Invoke(recognizer, null);
        Require(recognizer.IsModelLoaded, "model failed to load on " + backend);
        return recognizer;
    }

    private static void MeasureRecognizer(Report report)
    {
        string path = DefaultBenchmark;
        string[] args = Environment.GetCommandLineArgs();
        int arg = Array.IndexOf(args, "-airStrokes");
        if (arg >= 0 && arg + 1 < args.Length) path = args[arg + 1];
        BenchmarkSet set = File.Exists(path) ? JsonUtility.FromJson<BenchmarkSet>(File.ReadAllText(path)) : null;
        report.benchmark = set != null ? path : "not found: " + path;
        // Timing inputs: benchmark gestures when present, otherwise a fixed O/Z/X trio.
        var inputs = set != null
            ? set.samples.Where(s => s.points.Length >= 6).Take(240).Select(s => s.points.ToList()).ToList()
            : FixedShapes();
        if (inputs.Count == 0) inputs = FixedShapes();

        var root = new GameObject("TrackingStreamChecks Recognizer") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        try
        {
            // SampleScene serializes GPUCompute; with the component defaults the CNN must still run on CPU.
            var sceneSetup = CreateRecognizer(root, BackendType.GPUCompute, keepDefaults: true);
            Require((BackendType)Get(sceneSetup, "_backendType") == BackendType.CPU, "the 28x28 CNN must run on CPU by default");
            typeof(SentisRecognizer).GetMethod("DisposeSentisWorker", Flags).Invoke(sceneSetup, null);
            var gpu = CreateRecognizer(root, BackendType.GPUCompute);
            var cpu = CreateRecognizer(root, BackendType.CPU);
            report.backends = new[] { MeasureBackend(gpu, "GPUCompute", inputs), MeasureBackend(cpu, "CPU", inputs) };
            float maxDifference = 0f;
            foreach (var points in inputs.Take(60))
            {
                var a = gpu.Recognize(points); var b = cpu.Recognize(points);
                Require(a.IsMatch == b.IsMatch && a.Symbol == b.Symbol, "GPU and CPU disagree on a gesture");
                maxDifference = Mathf.Max(maxDifference, Mathf.Abs(a.Score - b.Score));
            }
            report.maxProbabilityDifference = maxDifference;
            if (set != null) report.conditions = Evaluate(set, cpu);
            typeof(SentisRecognizer).GetMethod("DisposeSentisWorker", Flags).Invoke(gpu, null);
            typeof(SentisRecognizer).GetMethod("DisposeSentisWorker", Flags).Invoke(cpu, null);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static BackendTiming MeasureBackend(SentisRecognizer recognizer, string requested, List<List<Point>> inputs)
    {
        for (int i = 0; i < 20; i++) recognizer.Recognize(inputs[i % inputs.Count]);
        var samples = new List<double>();
        var watch = new Stopwatch();
        foreach (var points in inputs)
        {
            watch.Restart();
            recognizer.Recognize(points);
            samples.Add(watch.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        return new BackendTiming
        {
            requested = requested, actual = Get(recognizer, "_backendType").ToString(), calls = samples.Count,
            meanMs = (float)samples.Average(), p50Ms = (float)samples[samples.Count / 2],
            p95Ms = (float)samples[(int)(samples.Count * .95f)]
        };
    }

    // Same decisions as DrawingManager: <6 points is a tap, every pen-up may commit early, then structure gates.
    private static ConditionResult[] Evaluate(BenchmarkSet set, SentisRecognizer recognizer)
    {
        var geometry = new PRecognizer(0.68f, 0.85f);
        PRecognitionResult Recognize(List<Point> points) => points.Count < 6 ? PRecognitionResult.NoMatch : recognizer.Recognize(points);
        var results = new List<ConditionResult>();
        foreach (var group in set.samples.GroupBy(s => (s.noisy, s.fps)))
        {
            var r = new ConditionResult { noisy = group.Key.noisy, fps = group.Key.fps };
            foreach (var sample in group)
            {
                var points = sample.points.ToList();
                GestureSymbol expected = SentisRecognizer.ClassToSymbol[sample.label];
                PRecognitionResult final = PRecognitionResult.NoMatch;
                bool tap = points.Count < 6, early = false;
                for (int i = 1; !tap && i < points.Count && !early; i++)
                {
                    if (points[i].StrokeID == points[i - 1].StrokeID) continue;
                    var prefix = points.GetRange(0, i);
                    var candidate = Recognize(prefix);
                    var shape = prefix.Count >= 6 ? geometry.Recognize(prefix.ToArray()) : PRecognitionResult.NoMatch;
                    if (GestureCompletionPolicy.CanCommit(prefix, candidate, shape)) { final = candidate; early = true; points = prefix; }
                }
                if (!tap && !early) final = Recognize(points);
                if (final.IsMatch && !GestureCompletionPolicy.HasRequiredStructure(points, final.Symbol)) final = PRecognitionResult.NoMatch;

                if (expected == GestureSymbol.Unknown) { r.unknowns++; if (final.IsMatch) r.unknownAccepted++; continue; }
                r.symbols++;
                if (tap) r.tooFewPoints++;
                if (early) r.earlyCommitted++;
                if (!final.IsMatch) r.rejected++;
                else if (final.Symbol == expected) r.correct++;
                else r.wrong++;
            }
            float n = Mathf.Max(1, r.symbols);
            r.correct /= n; r.wrong /= n; r.rejected /= n; r.tooFewPoints /= n; r.earlyCommitted /= n;
            r.unknownAccepted /= Mathf.Max(1, r.unknowns);
            results.Add(r);
        }
        return results.ToArray();
    }

    private static List<List<Point>> FixedShapes()
    {
        var o = new List<Point>();
        for (int i = 0; i <= 32; i++) o.Add(new Point(300 + 120 * Mathf.Cos(i * Mathf.PI / 16), 300 + 120 * Mathf.Sin(i * Mathf.PI / 16)));
        var z = new List<Point> { new Point(0, 200), new Point(100, 200), new Point(200, 200), new Point(100, 100), new Point(0, 0), new Point(100, 0), new Point(200, 0) };
        var x = new List<Point> { new Point(0, 0, 0), new Point(100, 100, 0), new Point(200, 200, 0), new Point(0, 200, 1), new Point(100, 100, 1), new Point(200, 0, 1) };
        return new List<List<Point>> { o, z, x };
    }
}
