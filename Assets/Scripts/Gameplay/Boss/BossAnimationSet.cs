using System;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    /// <summary>One named animation (boss pose clip or effect clip). Frame times are in seconds.</summary>
    [Serializable]
    public sealed class BossClip
    {
        public string key;
        public Sprite[] frames = Array.Empty<Sprite>();
        public float[] seconds = Array.Empty<float>();
        public bool loop;

        public int Length => frames == null ? 0 : frames.Length;

        public float SecondsAt(int index) =>
            seconds != null && index >= 0 && index < seconds.Length && seconds[index] > 0f ? seconds[index] : .1f;

        public float Duration
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < Length; i++) total += SecondsAt(i);
                return total;
            }
        }
    }

    /// <summary>Every clip a boss needs, keyed by name (idle, hurt, skill_a, whirlwind, ...). Built by the importer.</summary>
    [CreateAssetMenu(menuName = "Magic Touch/Boss/Animation Set", fileName = "BossAnimations")]
    public sealed class BossAnimationSet : ScriptableObject
    {
        [SerializeField] private BossClip[] _clips = Array.Empty<BossClip>();

        public BossClip[] Clips => _clips;

        public BossClip Get(string key)
        {
            if (string.IsNullOrEmpty(key) || _clips == null) return null;
            for (int i = 0; i < _clips.Length; i++)
                if (_clips[i] != null && _clips[i].key == key) return _clips[i];
            return null;
        }

#if UNITY_EDITOR
        public void SetClips(BossClip[] clips) => _clips = clips;
#endif
    }
}
