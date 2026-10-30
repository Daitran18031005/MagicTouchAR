using System;
using System.Collections;
using MagicTouch.Gameplay;
using UnityEngine;

namespace MagicTouch.Juice
{
    /// <summary>
    /// How a boss arrives. The encounter owns the timing: for some bosses the entrance plays first and the bar refills after,
    /// for others the bar refills while <see cref="SetRise"/> lifts a stage, then <see cref="Run"/> plays.
    /// </summary>
    public interface IBossEntrance
    {
        /// <summary>Places the static parts (stage, effects) before anything shows.</summary>
        void Prepare(BossDefinition def, BossVisual visual, Vector3 hover, Bounds player);
        /// <summary>0..1 while the progress bar refills; no-op for entrances without a rising stage.</summary>
        void SetRise(float t);
        IEnumerator Run(BossDefinition def, BossVisual visual, Vector3 hover, Bounds player, Func<bool> isPlaying);
        /// <summary>
        /// Plays beside the death clip (already started): the boss falls to the floor, or its stage sinks away.
        /// Takes at least <paramref name="deathSeconds"/>.
        /// </summary>
        IEnumerator Defeat(BossDefinition def, BossVisual visual, Bounds player, float deathSeconds, Func<bool> isPlaying);
        void Cancel();
    }
}
