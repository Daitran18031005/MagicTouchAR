using UnityEngine;

namespace MagicTouch.Gameplay
{
    /// <summary>Minions = survive the timed wave, Clearing = spawning stopped and the field must be emptied, then the boss comes.</summary>
    public enum SoloBossPhase { Minions, Clearing, Summoning, Boss, Dying, Victory }

    public enum SoloBossHit { None, Damaged, SegmentCleared, Defeated }

    /// <summary>
    /// One stage of the solo run: a timed minion wave, then a boss. Counts confirmed boss pops, never score or despawns.
    /// Boss health is a list of segments (health-bar layers) fought in order.
    /// </summary>
    public sealed class SoloBossProgress
    {
        private readonly int[] _segmentMax;
        private readonly int[] _segmentHealth;

        public float RequiredSeconds { get; }
        public float Elapsed { get; private set; }
        public int DamagePerBalloon { get; }
        public int SegmentCount => _segmentMax.Length;
        public int MaximumHealth { get; }
        public int Health { get; private set; }
        public SoloBossPhase Phase { get; private set; }

        /// <summary>First segment that still has health (the last one once the boss is dead).</summary>
        public int CurrentSegment
        {
            get
            {
                for (int i = 0; i < _segmentHealth.Length; i++) if (_segmentHealth[i] > 0) return i;
                return _segmentHealth.Length - 1;
            }
        }

        public float Fill => Phase == SoloBossPhase.Minions || Phase == SoloBossPhase.Clearing || Phase == SoloBossPhase.Summoning
            ? Mathf.Clamp01(Elapsed / RequiredSeconds) : (float)Health / MaximumHealth;

        public SoloBossProgress(float surviveSeconds, int[] segmentHealth, int damagePerBalloon)
        {
            RequiredSeconds = Mathf.Max(1f, surviveSeconds);
            DamagePerBalloon = Mathf.Max(1, damagePerBalloon);
            if (segmentHealth == null || segmentHealth.Length == 0) segmentHealth = new[] { 100 };
            _segmentMax = new int[segmentHealth.Length];
            _segmentHealth = new int[segmentHealth.Length];
            for (int i = 0; i < segmentHealth.Length; i++)
            {
                _segmentMax[i] = Mathf.Max(1, segmentHealth[i]);
                MaximumHealth += _segmentMax[i];
            }
            Reset();
        }

        public int SegmentMax(int index) => _segmentMax[index];
        public int SegmentHealth(int index) => _segmentHealth[index];
        public float SegmentFill(int index) => (float)_segmentHealth[index] / _segmentMax[index];

        public void Reset()
        {
            Elapsed = 0f;
            for (int i = 0; i < _segmentMax.Length; i++) _segmentHealth[i] = _segmentMax[i];
            Health = MaximumHealth;
            Phase = SoloBossPhase.Minions;
        }

        public SoloBossHit RecordPop(bool bossBalloon)
        {
            if (Phase != SoloBossPhase.Boss || !bossBalloon) return SoloBossHit.None;

            int segment = CurrentSegment;
            int damage = Mathf.Min(DamagePerBalloon, _segmentHealth[segment]);
            if (damage <= 0) return SoloBossHit.None;
            _segmentHealth[segment] -= damage;
            Health -= damage;
            if (_segmentHealth[segment] > 0) return SoloBossHit.Damaged;
            if (segment == _segmentHealth.Length - 1)
            {
                Phase = SoloBossPhase.Dying;
                return SoloBossHit.Defeated;
            }
            return SoloBossHit.SegmentCleared;
        }

        /// <summary>Advances the survival timer; returns true on the call that ends the wave.</summary>
        public bool AdvanceSurvive(float dt)
        {
            if (Phase != SoloBossPhase.Minions) return false;
            Elapsed = Mathf.Min(RequiredSeconds, Elapsed + Mathf.Max(0f, dt));
            if (Elapsed < RequiredSeconds) return false;
            Phase = SoloBossPhase.Clearing;
            return true;
        }

        /// <summary>The field is empty: the boss may arrive.</summary>
        public void BeginSummon() { if (Phase == SoloBossPhase.Clearing) Phase = SoloBossPhase.Summoning; }

        public void BeginBoss() { if (Phase == SoloBossPhase.Summoning) Phase = SoloBossPhase.Boss; }
        public void CompleteDeath() { if (Phase == SoloBossPhase.Dying) Phase = SoloBossPhase.Victory; }
    }
}
