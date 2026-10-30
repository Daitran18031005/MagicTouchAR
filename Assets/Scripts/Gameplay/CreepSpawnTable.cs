using System;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    [Serializable]
    public sealed class CreepDefinition
    {
        public string id;
        [Tooltip("Clips: idle, idle_cracked, hurt, death.")]
        public BossAnimationSet animations;
        [Min(0f)] public float weight = 1f;
        [Min(.2f), Tooltip("Longest time the dead creep takes to fade out while it keeps falling (shorter if the ground is close).")]
        public float deathFadeSeconds = .9f;
    }

    [Serializable]
    public struct BalloonCountWeight
    {
        [Min(2)] public int balloons;
        [Min(0f)] public float weight;
    }

    /// <summary>Spawn rules of one difficulty level. A creep has 1 balloon, or 2..maxBalloons with the weights below.</summary>
    [Serializable]
    public sealed class CreepDifficultyRule
    {
        public string label;
        [Range(0f, 1f), Tooltip("Chance that a creep spawns with a bunch of balloons (2 or more). The rest spawn with exactly 1.")]
        public float multiBalloonChance = .2f;
        [Min(1), Tooltip("Most balloons one creep can carry on this difficulty.")]
        public int maxBalloons = 2;
        [Tooltip("Relative weights of 2, 3, 4 balloons, used only when the creep gets a bunch.")]
        public BalloonCountWeight[] multiWeights = Array.Empty<BalloonCountWeight>();
    }

    /// <summary>
    /// The one place that controls which creeps spawn and how many balloons they carry, per difficulty
    /// (index 0 Easy, 1 Normal, 2 Hard). Tune the numbers in the asset; nothing is hard-coded in the spawner.
    /// </summary>
    [CreateAssetMenu(menuName = "Magic Touch/Creep Spawn Table", fileName = "CreepSpawnTable")]
    public sealed class CreepSpawnTable : ScriptableObject
    {
        public const string ResourceName = "CreepSpawnTable";

        [SerializeField] private CreepDefinition[] _creeps = Array.Empty<CreepDefinition>();
        [SerializeField] private CreepDifficultyRule[] _rules = Array.Empty<CreepDifficultyRule>();

        public CreepDefinition[] Creeps => _creeps;
        public CreepDifficultyRule[] Rules => _rules;

        public CreepDifficultyRule RuleFor(int difficulty) =>
            _rules == null || _rules.Length == 0 ? null : _rules[Mathf.Clamp(difficulty, 0, _rules.Length - 1)];

        /// <param name="roll">Returns a uniform number in [0,1); injected so checks can seed it.</param>
        public int PickBalloonCount(int difficulty, Func<float> roll)
        {
            var rule = RuleFor(difficulty);
            if (rule == null || roll() >= rule.multiBalloonChance) return 1;
            float total = 0f;
            foreach (var w in rule.multiWeights) if (w.balloons <= rule.maxBalloons) total += w.weight;
            if (total <= 0f) return 1;
            float pick = roll() * total;
            foreach (var w in rule.multiWeights)
            {
                if (w.balloons > rule.maxBalloons) continue;
                pick -= w.weight;
                if (pick < 0f) return w.balloons;
            }
            return Mathf.Max(2, rule.maxBalloons);
        }

        public CreepDefinition PickCreep(Func<float> roll)
        {
            float total = 0f;
            foreach (var c in _creeps) if (c != null && c.animations != null) total += c.weight;
            if (total <= 0f) return null;
            float pick = roll() * total;
            CreepDefinition last = null;
            foreach (var c in _creeps)
            {
                if (c == null || c.animations == null) continue;
                last = c;
                pick -= c.weight;
                if (pick < 0f) return c;
            }
            return last;
        }

        /// <summary>Exact probability of each balloon count (index = count, 0 unused) for a difficulty.</summary>
        public float[] Distribution(int difficulty)
        {
            var rule = RuleFor(difficulty);
            int max = rule == null ? 1 : Mathf.Max(1, rule.maxBalloons);
            var result = new float[max + 1];
            if (rule == null) { result[1] = 1f; return result; }
            float total = 0f;
            foreach (var w in rule.multiWeights) if (w.balloons <= max) total += w.weight;
            float multi = total > 0f ? rule.multiBalloonChance : 0f;
            result[1] = 1f - multi;
            foreach (var w in rule.multiWeights)
                if (w.balloons <= max && w.balloons >= 2) result[w.balloons] += multi * w.weight / total;
            return result;
        }

        /// <summary>Returns an error text, or null when the table is usable.</summary>
        public string Validate()
        {
            if (_creeps == null || _creeps.Length == 0) return "No creeps.";
            foreach (var c in _creeps)
                if (c == null || c.animations == null) return "A creep has no animation set.";
                else foreach (var key in new[] { "idle", "idle_cracked", "hurt", "death" })
                    if (c.animations.Get(key) == null) return $"{c.id}: missing clip '{key}'.";
            if (_rules == null || _rules.Length != 3) return "Need exactly 3 rules (Easy, Normal, Hard).";
            for (int i = 0; i < _rules.Length; i++)
            {
                var r = _rules[i];
                if (r.maxBalloons < 1 || r.maxBalloons > 4) return $"{r.label}: maxBalloons must be 1..4 (layout supports 4).";
                if (r.multiBalloonChance > 0f && r.maxBalloons < 2) return $"{r.label}: bunch chance without maxBalloons >= 2.";
                foreach (var w in r.multiWeights)
                    if (w.balloons > r.maxBalloons) return $"{r.label}: weight for {w.balloons} balloons exceeds maxBalloons.";
                if (i > 0 && r.multiBalloonChance < _rules[i - 1].multiBalloonChance) return $"{r.label}: bunch chance must not drop as difficulty rises.";
            }
            return null;
        }

#if UNITY_EDITOR
        public void Set(CreepDefinition[] creeps, CreepDifficultyRule[] rules) { _creeps = creeps; _rules = rules; }
#endif
    }
}
