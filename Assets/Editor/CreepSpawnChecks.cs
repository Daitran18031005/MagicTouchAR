using System;
using MagicTouch.Gameplay;
using UnityEditor;
using UnityEngine;

namespace MagicTouch.Editor
{
    /// <summary>Safe check (touches no scene): the creep table is valid and the measured balloon-count odds match its settings.</summary>
    public static class CreepSpawnChecks
    {
        private const int Samples = 200000;
        private const float Tolerance = .01f;

        [MenuItem("Tools/MagicTouch/Validate Creep Spawn Table")]
        public static void Run()
        {
            var table = Resources.Load<CreepSpawnTable>(CreepSpawnTable.ResourceName);
            if (table == null) { Debug.LogError("[CreepSpawnChecks] FAIL: Assets/Resources/CreepSpawnTable.asset is missing."); return; }
            string problem = table.Validate();
            if (problem != null) { Debug.LogError("[CreepSpawnChecks] FAIL: " + problem); return; }

            bool ok = true;
            float[] previousSingle = null;
            for (int level = 0; level < 3; level++)
            {
                var rng = new System.Random(12345 + level);
                Func<float> roll = () => (float)rng.NextDouble();
                var counts = new int[6];
                for (int i = 0; i < Samples; i++) counts[table.PickBalloonCount(level, roll)]++;

                var expected = table.Distribution(level);
                var rule = table.RuleFor(level);
                string line = $"{rule.label} (max {rule.maxBalloons}):";
                for (int n = 1; n < counts.Length; n++)
                {
                    float measured = counts[n] / (float)Samples;
                    float want = n < expected.Length ? expected[n] : 0f;
                    line += $" {n}b={measured:P1}(cfg {want:P1})";
                    if (Mathf.Abs(measured - want) > Tolerance) { ok = false; line += "!"; }
                    if (counts[n] > 0 && n > rule.maxBalloons) { ok = false; line += " OVER-MAX"; }
                }
                Debug.Log("[CreepSpawnChecks] " + line);
                if (previousSingle != null && expected[1] > previousSingle[1] + 1e-4f) { ok = false; Debug.Log("[CreepSpawnChecks] single-balloon odds rose with difficulty"); }
                previousSingle = expected;
            }

            var pickRng = new System.Random(7);
            var perCreep = new int[table.Creeps.Length];
            for (int i = 0; i < Samples; i++) perCreep[Array.IndexOf(table.Creeps, table.PickCreep(() => (float)pickRng.NextDouble()))]++;
            for (int i = 0; i < perCreep.Length; i++) Debug.Log($"[CreepSpawnChecks] {table.Creeps[i].id}: {perCreep[i] / (float)Samples:P1}");

            if (ok) Debug.Log("[CreepSpawnChecks] OK: table valid, measured odds match the configured odds.");
            else Debug.LogError("[CreepSpawnChecks] FAIL: see lines above.");
        }
    }
}
