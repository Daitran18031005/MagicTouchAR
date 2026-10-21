using System;
using System.Collections.Generic;
using System.Reflection;
using MagicTouch.Drawing;
using MagicTouch.Gameplay;
using MagicTouch.Recognition;
using UnityEngine;

namespace MagicTouch.EditorTools
{
    // Explicit Play Mode integration check in a scene without gameplay/tracking objects.
    public static class GestureCombatFeedbackChecks
    {
        private static GameObject _root;
        private static DrawingManager _drawing;
        private static float _oldTimeScale;
        private static readonly List<GameObject> Targets = new List<GameObject>();
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static object Field(object obj, string name) => obj.GetType().GetField(name, Flags).GetValue(obj);
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

        public static string Begin()
        {
            Require(Application.isPlaying, "Run in Play Mode.");
            Require(GameManager.Instance == null && DrawingManager.Instance == null && EnemySpawner.ActiveEnemies.Count == 0,
                "Use an isolated scene with no gameplay objects.");
            _oldTimeScale = Time.timeScale;
            try
            {
                _root = new GameObject("Temporary combat-feedback checks");
                _drawing = _root.AddComponent<DrawingManager>();
                _drawing.enabled = false;
                var gmObject = new GameObject("Temporary GameManager");
                gmObject.transform.SetParent(_root.transform);
                var game = gmObject.AddComponent<GameManager>();
                game.enabled = false;
                _drawing.OnGestureRecognized += game.HandleRecognizedGesture;
                game.currentState = GameState.Playing;

                Target(GestureSymbol.V);
                Check(0, true, 1); Require(game.score == 10, "Single-pop score changed.");
                Target(GestureSymbol.Caret);
                Check(0, true, 0); // Recognized V without a matching enemy must be red.
                CleanTargets();
                Check(1, false, 0); // An invalid gesture cannot inherit the other hand's hit.
                Target(GestureSymbol.V); Target(GestureSymbol.V);
                Check(1, true, 2); Require(game.score == 40, "Multi-pop score changed.");
                Check(0, true, 0); // A previous success cannot leak into the next miss.
                Target(GestureSymbol.V);
                game.currentState = GameState.Paused;
                Check(0, true, 0); Require(EnemySpawner.ActiveEnemies.Count == 1, "Paused game popped a target.");
                game.currentState = GameState.MainMenu;
                Require(game.ConfigureMode(GameMode.Versus), "Could not configure isolated versus test.");
                game.VersusRound.Reset(3);
                game.VersusRound.Start(Time.timeAsDouble);
                game.currentState = GameState.Playing;
                Check(1, true, 1);
                Require(game.VersusRound.RightScore == 10, "Versus attribution changed.");
                Time.timeScale = 0; // Feedback must still finish while gameplay is paused.
                return "PASS: hit, recognized miss, invalid gesture, multi-pop, per-hand/previous-hit isolation, paused rejection, versus hit. Waiting for unscaled fade.";
            }
            catch { Cleanup(); throw; }
        }

        private static void Target(GestureSymbol symbol)
        {
            var go = new GameObject("Temporary target " + symbol);
            go.transform.SetParent(_root.transform);
            var enemy = go.AddComponent<EnemyBalloon>();
            enemy.SetSymbol(symbol, null);
            Targets.Add(go);
        }

        private static void CleanTargets()
        {
            foreach (var target in Targets)
                if (target != null) { target.SetActive(false); UnityEngine.Object.Destroy(target); }
            Targets.Clear();
        }

        private static void Check(int hand, bool valid, int expectedDefeats)
        {
            _drawing.ResetForNewRound();
            var points = ((List<Point>[])Field(_drawing, "_accumulatedPoints"))[hand];
            float x = hand == 0 ? Screen.width * .25f : Screen.width * .75f;
            for (int i = 0; i < 9; i++) points.Add(new Point(x + i * 5, 100 + Mathf.Abs(i - 4) * 20));
            ((PRecognitionResult[])Field(_drawing, "_pendingResult"))[hand] = valid
                ? new PRecognitionResult { Symbol = GestureSymbol.V, IsMatch = true, Score = .99f }
                : PRecognitionResult.NoMatch;
            ((bool[])Field(_drawing, "_hasPendingResult"))[hand] = true;
            var lineObject = new GameObject("Temporary gesture line");
            lineObject.transform.SetParent(_root.transform);
            var line = lineObject.AddComponent<LineRenderer>();
            ((List<LineRenderer>[])Field(_drawing, "_activeLineRenderers"))[hand].Add(line);
            typeof(DrawingManager).GetMethod("FinalizeAndRecognize", Flags).Invoke(_drawing, new object[] { hand });
            int actual = ((int[])Field(_drawing, "_gestureDefeats"))[hand];
            Require(actual == expectedDefeats, $"Hand {hand}: expected {expectedDefeats} defeats, got {actual}.");
            Color expected = (Color)Field(_drawing, expectedDefeats > 0 ? "_successColor" : "_failureColor");
            Require(Vector4.Distance(line.startColor, expected) < .01f && Vector4.Distance(line.endColor, expected) < .01f,
                $"Wrong gesture feedback color for hand {hand}, defeats {expectedDefeats}: {line.startColor}, expected {expected}.");
        }

        public static string Finish()
        {
            try
            {
                Require(_drawing != null, "No integration check is running.");
                var lines = (List<LineRenderer>[])Field(_drawing, "_activeLineRenderers");
                Require(lines[0].Count == 0 && lines[1].Count == 0, "Feedback did not disappear while timeScale=0.");
                return "PASS: colored gestures faded and cleared while paused.";
            }
            finally { Cleanup(); }
        }

        private static void Cleanup()
        {
            Time.timeScale = _oldTimeScale;
            CleanTargets();
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _drawing = null;
        }
    }
}
