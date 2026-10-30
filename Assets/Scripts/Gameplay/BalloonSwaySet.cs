using System;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    /// <summary>
    /// Sway animation frames of every balloon colour (Assets/Sprites/Balloons/sway). Loaded from Resources by CreepCluster;
    /// a balloon whose static sprite is balloon_&lt;colour&gt; plays the frames of that colour, any other sprite stays static.
    /// </summary>
    public sealed class BalloonSwaySet : ScriptableObject
    {
        public const string ResourceName = "BalloonSwaySet";

        [Serializable]
        public struct Entry
        {
            public string color;
            public Sprite[] frames;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [SerializeField, Tooltip("Sideways shift of the balloon body in each frame, in sprite units (symbol glyphs follow it).")]
        private float[] _bodyOffsetX = new float[6];
        [Min(1f), Tooltip("Frames per second of the sway loop (6 frames = one full swing left and right).")]
        public float framesPerSecond = 6f;
        [Range(0f, 8f), Tooltip("Extra degrees the whole balloon + string rocks about the creep's hands.")]
        public float rockDegrees = 1.2f;
        [Range(0f, .3f), Tooltip("How far (world units) the string between a creep and each balloon of a bunch bows sideways.")]
        public float stringBowUnits = .05f;

        public float BodyOffsetX(int frame) => _bodyOffsetX != null && _bodyOffsetX.Length > 0 ? _bodyOffsetX[frame % _bodyOffsetX.Length] : 0f;

        private static BalloonSwaySet _cached;
        private static bool _loaded;

        public static BalloonSwaySet Instance
        {
            get
            {
                if (!_loaded) { _cached = Resources.Load<BalloonSwaySet>(ResourceName); _loaded = true; }
                return _cached;
            }
        }

        /// <summary>Frames for a balloon whose static sprite is named balloon_&lt;colour&gt;[_n], or null.</summary>
        public Sprite[] FramesFor(Sprite rest)
        {
            if (rest == null || !rest.name.StartsWith("balloon_")) return null;
            string rawName = rest.name.Substring(8);
            int cut = rawName.IndexOf('_');
            string color = cut < 0 ? rawName : rawName.Substring(0, cut);
            foreach (var e in _entries)
                if (e.color == color && e.frames != null && e.frames.Length > 0) return e.frames;
            return null;
        }

#if UNITY_EDITOR
        public void Set(Entry[] entries, float[] bodyOffsetX) { _entries = entries; _bodyOffsetX = bodyOffsetX; }
#endif
    }
}
