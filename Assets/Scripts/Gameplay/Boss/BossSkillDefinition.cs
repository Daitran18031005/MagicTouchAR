using System;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    public enum BossSkillKind
    {
        /// <summary>Fire leaves the boss mouth and flies (or streams) to the player.</summary>
        Projectile,
        /// <summary>A spike erupts from the ground at the player.</summary>
        GroundSpike,
        /// <summary>One or two claw marks appear at the player.</summary>
        Slash
    }

    [Flags]
    public enum BossSlashSides { Left = 1, Right = 2 }

    /// <summary>
    /// One boss attack. New bosses reuse the kinds above; only the clips and numbers change.
    /// </summary>
    [CreateAssetMenu(menuName = "Magic Touch/Boss/Skill", fileName = "BossSkill")]
    public sealed class BossSkillDefinition : ScriptableObject
    {
        [Header("Boss animation")]
        [SerializeField] private string _bossClip = "skill_a";
        [Tooltip("Frame of the boss clip on which the attack is released.")]
        [SerializeField, Min(0)] private int _impactFrame = 3;
        [Tooltip("Warning time shown at the player; a boss balloon popped in this window cancels the attack.")]
        [SerializeField, Min(0.2f)] private float _telegraphSeconds = 1.2f;

        [Header("Effect")]
        [SerializeField] private BossSkillKind _kind = BossSkillKind.Slash;
        [SerializeField] private string _fxClip = "claw_slash";
        [SerializeField] private BossSlashSides _slashSides = BossSlashSides.Left;
        [Tooltip("Effect frame on which the player is hit.")]
        [SerializeField, Min(0)] private int _fxHitFrame = 1;
        [SerializeField, Min(0.1f)] private float _fxScale = 1f;
        [Tooltip("World offset from the player's feet/centre to the effect sprite centre (before scale).")]
        [SerializeField] private Vector2 _fxOffset = Vector2.zero;
        [SerializeField, Min(0.05f)] private float _fxFrameSeconds = 0.08f;
        [Tooltip("When above 0 the effect is drawn this many times taller than the player (overrides Fx Scale).")]
        [SerializeField, Min(0f)] private float _playerHeightMultiple;
        [Tooltip("Share of the effect cell height that holds the drawn picture (the rest is padding).")]
        [SerializeField, Range(.2f, 1f)] private float _contentHeight = .7f;

        [Header("Projectile only")]
        [SerializeField, Min(0.1f)] private float _flightSeconds = 0.55f;
        [Tooltip("Chance that the attack is a fire column along the path instead of one fireball.")]
        [SerializeField, Range(0f, 1f)] private float _columnChance;
        [Tooltip("Mouth position relative to the boss centre, in world units at scale 1.")]
        [SerializeField] private Vector2 _mouthOffset = new Vector2(0f, -0.35f);

        [Header("Audio / damage")]
        [SerializeField] private AudioClip _sound;
        [SerializeField, Min(1)] private int _damage = 1;

        public string BossClip => _bossClip;
        public int ImpactFrame => _impactFrame;
        public float TelegraphSeconds => _telegraphSeconds;
        public BossSkillKind Kind => _kind;
        public string FxClip => _fxClip;
        public BossSlashSides SlashSides => _slashSides;
        public int FxHitFrame => _fxHitFrame;
        public float FxScale => _fxScale;
        public Vector2 FxOffset => _fxOffset;
        public float FxFrameSeconds => _fxFrameSeconds;
        public float PlayerHeightMultiple => _playerHeightMultiple;
        public float ContentHeight => _contentHeight;
        public float FlightSeconds => _flightSeconds;
        public float ColumnChance => _columnChance;
        public Vector2 MouthOffset => _mouthOffset;
        public AudioClip Sound => _sound;
        public int Damage => _damage;
    }
}
