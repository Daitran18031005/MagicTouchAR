using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using MagicTouch.Drawing;
using MagicTouch.Recognition;

// Local, opt-in command file so diagnostics remain usable when MCP is unavailable.
[InitializeOnLoad]
public static class DrawingRepairChecks
{
    private const string CommandPath = "Tools/drawing-editor-command.txt";
    private const string ResultPath = "Logs/drawing-repair-result.json";
    private static double nextCheck;
    static DrawingRepairChecks() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextCheck || EditorApplication.isCompiling) return;
        nextCheck = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists(CommandPath)) return;
        string command = File.ReadAllText(CommandPath).Trim();
        if (command.Length == 0) return;
        File.WriteAllText(CommandPath, "");
        try
        {
            if (command == "stop") EditorApplication.isPlaying = false;
            else if (command == "play") EditorApplication.isPlaying = true;
            else if (command == "check") RunChecks();
            else if (command == "check-interaction") InteractionRepairChecks.Run();
            else if (command == "check-reset") InteractionRepairChecks.CheckWarmRestart();
            else if (command == "configure-tracking")
            {
                if (EditorApplication.isPlaying) throw new Exception("Configure tracking in Edit Mode");
                var bridge = UnityEngine.Object.FindFirstObjectByType<MagicTouch.Tracking.HandTrackingBridge>(FindObjectsInactive.Include);
                if (bridge == null) throw new Exception("No tracking bridge in scene");
                Undo.RecordObject(bridge, "Apply shared hand filter settings");
                bridge.useOneEuroFilter = bridge.usePalmRelativeFiltering = true;
                bridge.fingertipOffsetCutoff = 0.8f;
                bridge.fingertipOffsetBeta = 0.006f;
                bridge.fingertipSpikeThreshold = 45f;
                EditorUtility.SetDirty(bridge);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(bridge.gameObject.scene);
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(bridge.gameObject.scene);
            }
            else if (command == "refresh") AssetDatabase.Refresh();
            else if (command == "capture") ScreenCapture.CaptureScreenshot("Logs/drawing-game.png");
            WriteResult(command, "OK");
        }
        catch (Exception e) { WriteResult(command, e.ToString()); Debug.LogException(e); }
    }

    [Serializable] private class Result
    {
        public string command, result, scene;
        public bool playing, compiling;
        public bool sampleTimedSmoothing, constrainedSpline, bridgeStreamerRunning, receiverStreamerRunning;
        public uint hand0Samples, hand1Samples;
        public float hand0SampleInterval, hand1SampleInterval;
        public float cameraFPS, trackingFPS, inferenceMilliseconds;
        public bool streamSessionActive;
        public int ignoredLandmarkPackets, ignoredVideoPackets;
    }
    private static void WriteResult(string command, string result)
    {
        Directory.CreateDirectory("Logs");
        var bridge = MagicTouch.Tracking.HandTrackingBridge.Instance;
        var drawing = DrawingManager.Instance;
        File.WriteAllText(ResultPath, JsonUtility.ToJson(new Result {
            command = command, result = result, playing = EditorApplication.isPlaying,
            compiling = EditorApplication.isCompiling,
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
            sampleTimedSmoothing = drawing != null && drawing.useSampleTimedSmoothing,
            constrainedSpline = drawing != null && drawing.constrainSplineOvershoot,
            bridgeStreamerRunning = MagicTouch.Tracking.HandTrackingBridge.IsPythonStreamerRunning,
            receiverStreamerRunning = MagicTouch.Tracking.MediaPipeUDPReceiver.IsPythonTrackerRunning,
            hand0Samples = bridge != null ? bridge.GetHand(0).SampleVersion : 0,
            hand1Samples = bridge != null ? bridge.GetHand(1).SampleVersion : 0,
            hand0SampleInterval = bridge != null ? bridge.GetHand(0).SampleInterval : 0,
            hand1SampleInterval = bridge != null ? bridge.GetHand(1).SampleInterval : 0,
            cameraFPS = bridge != null ? bridge.CameraFPS : 0,
            trackingFPS = bridge != null ? bridge.TrackingFPS : 0,
            inferenceMilliseconds = bridge != null ? bridge.InferenceMilliseconds : 0,
            streamSessionActive = !string.IsNullOrEmpty(MagicTouch.Tracking.TrackingStreamSession.ActiveId),
            ignoredLandmarkPackets = MagicTouch.Tracking.TrackingStreamSession.IgnoredLandmarkPackets,
            ignoredVideoPackets = MagicTouch.Tracking.TrackingStreamSession.IgnoredVideoPackets
        }, true));
    }

    private static object Invoke(DrawingManager manager, string method, params object[] args)
        => typeof(DrawingManager).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, args);
    private static void Require(bool condition, string message)
    { if (!condition) throw new Exception("Drawing regression: " + message); }

    [MenuItem("Tools/MagicTouch/Validate Drawing Repair")]
    public static void RunChecks()
    {
        var root = new GameObject("DrawingRepairChecks") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false); // Never register as the live singleton or start gameplay.
        try
        {
            var manager = root.AddComponent<DrawingManager>();
            Require(Mathf.Abs((float)Invoke(manager,"GetPinchDropBuffer",0))<0.001f,"visible release must freeze immediately");
            var missing=(bool[])typeof(DrawingManager).GetField("_trackingUnavailable",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(manager);
            missing[0]=true;
            Require(Mathf.Abs((float)Invoke(manager,"GetPinchDropBuffer",0)-0.14f)<0.001f,"camera loss needs the bounded 140 ms recovery window");
            Require(Mathf.Abs((float)Invoke(manager,"GetPinchDropBuffer",1))<0.001f,"loss grace must be per player");
            missing[0]=false;
            var keys = new List<Vector2>();
            var points = new List<Point> {new Point(0,0,0),new Point(1,0,0),new Point(2,0,0),new Point(100,0,0)};
            Invoke(manager,"ExtractStrokeKeyPoints",points,keys);
            Require(keys.Count == 5, "five landmarks per stroke");
            for (int i=0;i<5;i++) Require(Vector2.Distance(keys[i],new Vector2(i*25,0))<0.001f,"arc-length quarter " + i);
            keys.Clear();
            points = new List<Point> {new Point(8,9,0),new Point(8,9,0),new Point(100,0,1),new Point(200,0,1)};
            Invoke(manager,"ExtractStrokeKeyPoints",points,keys);
            Require(keys.Count==10 && keys[2]==new Vector2(8,9) && keys[7]==new Vector2(150,0),"degenerate and separate strokes");
            var control = new List<Vector3> {Vector3.zero,new Vector3(100,0),new Vector3(100,100),new Vector3(10,100)};
            var curve = new List<Vector3>();
            Invoke(manager,"GenerateCatmullRomSpline",control,5,curve);
            for(int i=0;i<curve.Count-1;i++)
            {
                int s=i/5;
                Vector3 a=control[s], b=control[s+1], v=b-a;
                float t=Vector3.Dot(curve[i]-a,v)/v.sqrMagnitude;
                Require(t>=-0.0001f && t<=1.0001f && Vector3.Distance(curve[i],a+v*t)<0.001f,"spline escaped measured segment");
            }
            Vector2 fast=(Vector2)Invoke(manager,"CalculateSampleTimedSmoothing",Vector2.zero,Vector2.zero,new Vector2(300,0),1f/30f);
            Require(fast.x>280f,"fast movement was capped or delayed");
            Invoke(manager,"UpdateHandGestureState",0,true,new Vector2(900,900),Vector3.zero,true);
            Invoke(manager,"UpdateHandGestureState",0,true,new Vector2(900,900),Vector3.zero,false);
            Require(manager.GetLastGesturePoints(0).Length==0,"duplicate render frame admitted noisy start");
            Invoke(manager,"UpdateHandGestureState",0,true,new Vector2(20,30),Vector3.zero,true);
            var accepted=manager.GetLastGesturePoints(0);
            Require(accepted.Length==1 && accepted[0].X==20 && accepted[0].Y==30,"first noise sample contaminated start");
            Invoke(manager,"UpdateHandGestureState",0,false,Vector2.zero,Vector3.zero,true);
            Invoke(manager,"UpdateHandGestureState",0,true,new Vector2(900,900),Vector3.zero,true);
            Require(manager.GetLastGesturePoints(0).Length==1,"recovery spike appended");
            Require(manager.GetLastGesturePoints(1).Length==0,"player buffers contaminated");
            Debug.Log("[DrawingRepair] PASS: arc length, degenerate strokes, spline bounds, fast motion, fresh samples, start/recovery noise, player isolation.");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
