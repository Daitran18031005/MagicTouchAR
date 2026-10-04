using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MagicTouch.Tracking;
using MagicTouch.Drawing;
using MagicTouch.Recognition;
using MagicTouch.Gameplay;

// Opt-in local position-only diagnostics. Never records camera frames.
[InitializeOnLoad]
public static class HandMotionTrace
{
    private static double _until;
    private static string _label;
    private static StringBuilder _csv;
    private static uint[] _versions = new uint[2];
    private static int[] _detected = { -1, -1 };
    private static double _poll;
    private static float _startedSampleTime;
    private static T ReadField<T>(object target, string field)
        => (T)target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static HandMotionTrace() { EditorApplication.update += Update; }
    private static void Update()
    {
        if (EditorApplication.isCompiling) return;
        if (_csv == null)
        {
            if (EditorApplication.timeSinceStartup < _poll) return;
            _poll = EditorApplication.timeSinceStartup + 0.3;
            const string command="Tools/hand-motion-trace.txt";
            if (!File.Exists(command) || !EditorApplication.isPlaying || HandTrackingBridge.Instance==null) return;
            string value=File.ReadAllText(command).Trim();
            if(value=="left-wait")
            {
                var source=HandTrackingBridge.Instance;
                int visible=(source.GetHand(0).IsDetected?1:0)+(source.GetHand(1).IsDetected?1:0);
                bool pinch=source.IsPinching(0)||source.IsPinching(1);
                if(visible!=1 || !pinch || GameManager.Instance==null || GameManager.Instance.currentState!=GameState.Playing) return;
                value="left";
            }
            if(value!="left" && value!="right") return;
            File.WriteAllText(command,""); _label=value;
            _csv=new StringBuilder("time,slot,track,detected,pinch,rawX,rawY,filteredX,filteredY,distance,dt,wristX,wristY,observedTime,palmSpan,pinchRatio,gameState,strokeId,pointCount,active,waitForRelease,frame,pendingRelease\n");
            _versions=new uint[2]; _until=EditorApplication.timeSinceStartup+10;
            _detected=new int[] { -1, -1 };
            _startedSampleTime=Time.realtimeSinceStartup;
            Debug.Log("[HandMotionTrace] Recording " + value + " for 10 seconds.");
        }
        var bridge=HandTrackingBridge.Instance;
        if(bridge!=null) for(int i=0;i<2;i++)
        {
            var h=bridge.GetHand(i);
            if(h.SampleTime < _startedSampleTime) continue;
            int detected=h.IsDetected?1:0;
            if(_versions[i]==h.SampleVersion && _detected[i]==detected) continue;
            _versions[i]=h.SampleVersion;
            _detected[i]=detected;
            float palmSpan=0f;
            // Inspect already received coordinates only; do not access or record video.
            var snapshots=ReadField<Array>(bridge,"_mainSnapshots");
            object snapshot=snapshots.GetValue(i);
            var landmarks=(Vector2[])snapshot.GetType().GetField("landmarks").GetValue(snapshot);
            if(landmarks!=null && landmarks.Length>=21)
                palmSpan=Mathf.Max(Vector2.Distance(landmarks[0],landmarks[9]),Vector2.Distance(landmarks[5],landmarks[17]));
            var drawing=DrawingManager.Instance;
            int stroke=drawing!=null?ReadField<int[]>(drawing,"_currentStrokeId")[i]:-1;
            int count=drawing!=null?ReadField<List<Point>[]>(drawing,"_accumulatedPoints")[i].Count:0;
            bool active=drawing!=null && ReadField<bool[]>(drawing,"_isGestureActive")[i];
            bool wait=drawing!=null && ReadField<bool[]>(drawing,"_waitForRelease")[i];
            string state=GameManager.Instance!=null?GameManager.Instance.currentState.ToString():"none";
            _csv.AppendFormat(CultureInfo.InvariantCulture,"{0:F5},{1},{2},{3},{4},{5:F3},{6:F3},{7:F3},{8:F3},{9:F5},{10:F5},{11:F3},{12:F3},{13:F5},{14:F5},{15:F5},{16},{17},{18},{19},{20},{21},{22}\n",
                h.SampleTime,i,h.TrackId,detected,h.IsPinching?1:0,h.RawIndexTipScreen.x,h.RawIndexTipScreen.y,h.IndexTipScreen.x,h.IndexTipScreen.y,h.RawPinchDistance,h.SampleInterval,h.RawWristScreen.x,h.RawWristScreen.y,Time.realtimeSinceStartup,
                palmSpan,palmSpan>0.0001f?h.RawPinchDistance/palmSpan:0f,state,stroke,count,active?1:0,wait?1:0,Time.frameCount,h.IsPinchReleasePending?1:0);
        }
        if(EditorApplication.timeSinceStartup>=_until)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/hand-motion-"+_label+".csv",_csv.ToString()); _csv=null;
            Debug.Log("[HandMotionTrace] Finished " + _label);
        }
    }
}
