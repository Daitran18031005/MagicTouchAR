using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using MagicTouch.Drawing;
using MagicTouch.Tracking;
using MagicTouch.Recognition;
using MagicTouch.Gameplay;

public static class InteractionRepairChecks
{
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);

    [MenuItem("Tools/MagicTouch/Validate Interaction Repair")]
    public static void Run()
    {
        var pinchTransition = new PinchTransitionFilter();
        pinchTransition.Update(.03221f,.04f,.08f);
        Require(pinchTransition.IsPinching,"pinch entry must remain immediate");
        pinchTransition.Update(.08182f,.04f,.08f);
        Require(pinchTransition.IsPinching && pinchTransition.IsReleasePending,"recorded borderline spike must pause ink pending confirmation");
        pinchTransition.Update(.04293f,.04f,.08f);
        Require(pinchTransition.IsPinching && !pinchTransition.IsReleasePending,"recorded noise recovery must resume without requiring a new pinch");
        pinchTransition.Update(.09f,.04f,.08f);
        pinchTransition.Update(.09f,.04f,.08f);
        Require(!pinchTransition.IsPinching && !pinchTransition.IsReleasePending,"sustained small release stuck pinched");
        pinchTransition.Update(.02f,.04f,.08f);
        pinchTransition.Update(.14f,.04f,.08f);
        Require(!pinchTransition.IsPinching && !pinchTransition.IsReleasePending,"clear open hand must release on the first sample");
        string session = "0123456789abcdef0123456789abcdef";
        Require(TrackingStreamSession.Matches(session,session),"current stream rejected");
        Require(!TrackingStreamSession.Matches(session,null) && !TrackingStreamSession.Matches(session,"old"),"stale landmark packet accepted");
        Require(TrackingStreamSession.Matches("",null),"manual legacy landmarks rejected");
        byte[] jpeg = { 0xff,0xd8,0xff,0xd9 };
        byte[] tagged = new byte[40];
        System.Text.Encoding.ASCII.GetBytes("MTAR"+session).CopyTo(tagged,0);
        jpeg.CopyTo(tagged,36);
        Require(TrackingStreamSession.TryUnwrapVideo(tagged,session,out var decoded) && decoded.Length==4 && decoded[0]==0xff,"current video packet not decoded");
        Require(!TrackingStreamSession.TryUnwrapVideo(tagged,"old",out _) && !TrackingStreamSession.TryUnwrapVideo(jpeg,session,out _),"stale video accepted");
        Require(TrackingStreamSession.TryUnwrapVideo(jpeg,"",out _) && !TrackingStreamSession.TryUnwrapVideo(new byte[]{77,84,65,82},session,out _),"legacy/truncated video handling");
        // Replay jitter and motion identically for both anatomical hands: filter has no side bias.
        var left = new OneEuroPositionFilter(); var right = new OneEuroPositionFilter();
        float rawEnergy=0f, filteredEnergy=0f;
        for(int i=0;i<180;i++)
        {
            var raw=new Vector2(300+(i%2==0?4:-4),400+(i%3-1)*3);
            var a=left.Filter(raw,1f/30f,1.5f,0.025f,1f);
            var b=right.Filter(raw,1f/30f,1.5f,0.025f,1f);
            Require(a==b,"filter state must be isolated and symmetric");
            if(i>30) {rawEnergy+=(raw-new Vector2(300,400)).sqrMagnitude; filteredEnergy+=(a-new Vector2(300,400)).sqrMagnitude;}
        }
        Require(filteredEnergy<rawEnergy*0.35f,"stationary jitter was not attenuated");
        Vector2 fast=left.Filter(new Vector2(650,400),1f/30f,1.5f,0.025f,1f);
        Require(fast.x>600,"fast-motion filter lag too large");
        left.Reset(); Require(left.Filter(new Vector2(50,60),.03f,1.5f,.025f,1)==new Vector2(50,60),"reacquisition retained stale position");
        var palmFilter = new HandPositionFilter();
        Vector2 wrist = new Vector2(100,100), offset = new Vector2(0,100);
        float tipNoise = 0f;
        for(int i=0;i<120;i++)
        {
            Vector2 result=palmFilter.Filter(wrist+offset+new Vector2(i%2==0?18:-18,0),wrist,1f/30f,1.5f,.025f,1f,.8f,.006f);
            if(i>30) tipNoise+=(result-wrist-offset).sqrMagnitude;
        }
        Require(tipNoise/89f < 18f*18f*.15f,"palm-relative filter did not suppress fingertip-only jitter");
        wrist.x+=350;
        Vector2 translated=palmFilter.Filter(wrist+offset,wrist,1f/30f,1.5f,.025f,1f,.8f,.006f);
        Require(translated.x>400f,"whole-hand movement was delayed by fingertip smoothing");

        var diagonal=new List<Point>{new Point(0,0,0),new Point(50,50,0),new Point(100,100,0)};
        var lineMatch=new PRecognitionResult{Symbol=GestureSymbol.VerticalLine,Score=1f,IsMatch=true};
        Require(!GestureCompletionPolicy.CanCommit(diagonal,lineMatch,lineMatch),"high-confidence prefix committed too early");
        var cross=new List<Point>(diagonal){new Point(0,100,1),new Point(50,50,1),new Point(100,0,1)};
        var xMatch=new PRecognitionResult{Symbol=GestureSymbol.X,Score=.99f,IsMatch=true};
        Require(GestureCompletionPolicy.CanCommit(cross,xMatch,xMatch),"completed X should commit at second release");
        var parallel=new List<Point>(diagonal){new Point(20,0,1),new Point(70,50,1),new Point(120,100,1)};
        Require(!GestureCompletionPolicy.HasCross(parallel),"parallel strokes accepted as cross");
        var e=new List<Point>{new Point(0,0,0),new Point(0,100,0),new Point(0,100,1),new Point(60,100,1),new Point(0,50,2),new Point(60,50,2),new Point(0,0,3),new Point(60,0,3)};
        var eMatch=new PRecognitionResult{Symbol=GestureSymbol.E,Score=.99f,IsMatch=true};
        Require(GestureCompletionPolicy.CanCommit(e,eMatch,eMatch),"complete E should commit");
        e.RemoveRange(6,2); Require(!GestureCompletionPolicy.CanCommit(e,eMatch,eMatch),"unfinished E should wait");

        var root=new GameObject("InteractionChecks"){hideFlags=HideFlags.HideAndDontSave}; root.SetActive(false);
        try
        {
            var manager=root.AddComponent<DrawingManager>();
            Action<int,bool,float,bool> input=(hand,pinch,x,fresh)=>Call(manager,"UpdateHandGestureState",hand,pinch,new Vector2(x,20),Vector3.zero,fresh);
            input(0,true,10,true); input(0,true,10,false);
            Require(manager.GetLastGesturePoints().Length==0,"duplicate render frame starts stroke");
            input(0,true,20,true); input(0,true,40,true);
            int count=manager.GetLastGesturePoints().Length;
            ((bool[])Field(manager,"_pinchReleasePending"))[0]=true;
            input(0,true,900,true);
            Require(manager.GetLastGesturePoints().Length==count,"ambiguous release drew a connecting line");
            ((bool[])Field(manager,"_pinchReleasePending"))[0]=false;
            input(0,true,50,true);
            var resumed=manager.GetLastGesturePoints();
            Require(resumed[resumed.Length-1].StrokeID==0,"one noisy release split the stroke");
            count=resumed.Length;
            input(0,false,900,true);
            Require(manager.GetLastGesturePoints().Length==count,"release coordinate appended");
            input(0,true,900,true); input(0,true,60,true);
            var points=manager.GetLastGesturePoints();
            Require(points[points.Length-1].StrokeID==1,"rapid repinch must create a new segment");
            Require(manager.GetLastGesturePoints(1).Length==0,"second player history contaminated");
            ((bool[])Field(manager,"_trackingUnavailable"))[0]=true;
            input(0,false,0,true);
            Require(((bool[])Field(manager,"_isGestureActive"))[0],"short tracking loss falsely finalized");
            ((bool[])Field(manager,"_trackingUnavailable"))[0]=false;
            manager.minDrawDistance=0.1f; // Assert segmentation independently of point decimation.
            int beforeResume=manager.GetLastGesturePoints().Length;
            input(0,true,64,true);
            points=manager.GetLastGesturePoints();
            Require(points.Length>beforeResume && points[points.Length-1].StrokeID==1,"brief dropout fragmented a held pinch");
            // An observed release after a dropout must still separate the next pinch.
            ((bool[])Field(manager,"_trackingUnavailable"))[0]=true;
            input(0,false,0,true);
            ((bool[])Field(manager,"_trackingUnavailable"))[0]=false;
            input(0,false,900,true);
            input(0,true,900,true); input(0,true,70,true);
            points=manager.GetLastGesturePoints();
            Require(points[points.Length-1].StrokeID==2,"open hand after loss was joined to the previous stroke");
            // A far-away reacquisition must not connect across the screen.
            ((bool[])Field(manager,"_trackingUnavailable"))[0]=true;
            input(0,false,0,true);
            ((bool[])Field(manager,"_trackingUnavailable"))[0]=false;
            int beforeJump=manager.GetLastGesturePoints().Length;
            input(0,true,10000,true);
            Require(manager.GetLastGesturePoints().Length==beforeJump,"large reacquisition jump bridged the gap");
            input(0,true,10004,true);
            points=manager.GetLastGesturePoints();
            Require(points[points.Length-1].StrokeID==3,"large reacquisition needs a separate segment");
            ((bool[])Field(manager,"_trackingUnavailable"))[0]=true;
            input(0,false,0,true);
            ((float[])Field(manager,"_pinchDropTimer"))[0]=-1;
            input(0,false,0,true);
            Require(manager.GetLastGesturePoints().Length==0,"long tracking loss must discard unfinished input");
            manager.ResetForNewRound();
            Require(!((bool[])Field(manager,"_isGestureActive"))[0],"restart retained active gesture");
            input(0,true,80,true); input(0,true,90,true);
            Require(manager.GetLastGesturePoints().Length==0,"restart must wait for held pinch to release");
            if (!EditorApplication.isPlaying)
            {
                var bridge=root.AddComponent<HandTrackingBridge>();
                var landmarks=new Vector2[21];
                for(int i=0;i<21;i++) landmarks[i]=new Vector2(.5f,.5f);
                landmarks[4].x=.52f;
                bridge.ProcessHandLandmarks(0,landmarks,1000,101);
                Require(bridge.GetHand(0).IsPinching,"pinch test setup");
                bridge.SetHandLost(0);
                landmarks[4].x=.56f; // Between entry and release thresholds.
                bridge.ProcessHandLandmarks(0,landmarks,1066,101);
                Require(bridge.GetHand(0).IsPinching,"short loss discarded pinch hysteresis");
                bridge.SetHandLost(0);
                landmarks[4].x=.64f;
                bridge.ProcessHandLandmarks(0,landmarks,1132,101);
                Require(!bridge.GetHand(0).IsPinching,"actual open hand must release immediately");
                landmarks[4].x=.52f;
                bridge.ProcessHandLandmarks(0,landmarks,1165,101);
                bridge.SetHandLost(0);
                landmarks[4].x=.56f;
                bridge.ProcessHandLandmarks(0,landmarks,1198,102);
                Require(!bridge.GetHand(0).IsPinching,"new track inherited the previous pinch");
                landmarks[4].x=.52f;
                bridge.ProcessHandLandmarks(0,landmarks,1231,102);
                landmarks[4].x=.58182f;
                bridge.ProcessHandLandmarks(0,landmarks,1264,102);
                Require(bridge.GetHand(0).IsPinchReleasePending && !bridge.GetHand(0).IsDrawing,"ambiguous release must stop ink immediately");
                landmarks[4].x=.54293f;
                bridge.ProcessHandLandmarks(0,landmarks,1297,102);
                Require(bridge.GetHand(0).IsPinching && bridge.GetHand(0).IsDrawing,"noise recovery did not resume drawing");
                bridge.useOneEuroFilter=false; // Isolate the outlier gate from low-pass filtering.
                bridge.fingertipSpikeThreshold=1f;
                bridge.ProcessHandLandmarks(0,landmarks,1330,102);
                Vector2 oldTip=bridge.GetHand(0).IndexTipScreen;
                Vector2 oldWrist=bridge.GetHand(0).RawWristScreen;
                landmarks[0].x+=.01f;
                landmarks[8].x+=.2f;
                bridge.ProcessHandLandmarks(0,landmarks,1363,102);
                Vector2 wristMovement=bridge.GetHand(0).RawWristScreen-oldWrist;
                Require((bridge.GetHand(0).IndexTipScreen-oldTip-wristMovement).sqrMagnitude<.01f,"tip outlier froze whole-hand translation");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log($"[InteractionRepair] PASS: jitter ratio={filteredEnergy/rawEnergy:F3}; identity-independent filter, motion, release, multi-stroke X/E, loss and reset.");
    }

    public static void CheckWarmRestart()
    {
        Require(EditorApplication.isPlaying,"Run warm reset check in Play mode");
        var game=GameManager.Instance; var bridge=HandTrackingBridge.Instance; var model=SentisRecognizer.Instance;
        Require(game!=null && bridge!=null,"missing live game/bridge");
        object worker=model!=null?Field(model,"_worker"):null;
        object process=typeof(HandTrackingBridge).GetField("_pythonProcess",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
        bool ready=bridge.IsTrackingReady;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        for(int i=0;i<3;i++)
        {
            game.GameOver(); game.RestartGame();
            Require(game.score==0 && game.combo==0 && game.lives==3 && !game.isGameOver,"round state not reset");
            Require(ReferenceEquals(GameManager.Instance,game) && ReferenceEquals(HandTrackingBridge.Instance,bridge),"restart reloaded scene");
            Require(model==null || ReferenceEquals(worker,Field(model,"_worker")),"restart recreated model");
            Require(ReferenceEquals(process,typeof(HandTrackingBridge).GetField("_pythonProcess",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null)),"restart recreated Python");
            if(ready) Require(game.currentState==GameState.Playing,"ready camera caused reset wait");
        }
        watch.Stop();
        Debug.Log($"[InteractionRepair] WARM RESET PASS: 3 resets in {watch.ElapsedMilliseconds}ms; cameraReady={ready}; model and Python instances preserved.");
    }
}
