using System;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    /// <summary>One layer of the boss health bar. Layers are fought in order; the first one is drawn on top.</summary>
    [Serializable]
    public sealed class BossSegment
    {
        public string name = "Red";
        [Min(1)] public int health = 100;
        public Color barColor = new Color(.94f, .24f, .12f);
        [Tooltip("Clip the boss rests in while this layer is being fought.")]
        public string restClip = "idle";
        public BossSkillDefinition[] skills = Array.Empty<BossSkillDefinition>();
        [Min(1f)] public float castInterval = 5.2f;
    }

    /// <summary>
    /// Everything that is specific to one boss. The encounter, visual, skill caster and entrance are generic;
    /// a new boss (mini boss, map boss) is new art plus one of these assets.
    /// </summary>
    [CreateAssetMenu(menuName = "Magic Touch/Boss/Definition", fileName = "Boss")]
    public sealed class BossDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _displayName = "BOSS";
        [SerializeField] private BossAnimationSet _animations;
        [SerializeField] private string _minionText = "ROAD TO THE BOSS";
        [SerializeField] private string _introText = "THE BOSS APPROACHES!";
        [SerializeField] private string _defeatedText = "BOSS DEFEATED!";

        [Header("Encounter")]
        [Tooltip("Unused since the stage runs on a timer (Survive Seconds); kept so saved assets keep their value.")]
        [SerializeField, Min(1)] private int _minionsBeforeBoss = 20;
        [Tooltip("Seconds of ordinary minions to survive before this boss arrives. The progress bar fills in real time.")]
        [SerializeField, Min(1f)] private float _surviveSeconds = 60f;
        [Tooltip("Seconds the bar takes to refill with the boss health (a stage platform rises during it).")]
        [SerializeField, Min(.2f)] private float _refillSeconds = 1.2f;
        [Tooltip("True: the entrance plays first, then the bar refills (Ember). False: the bar refills while the entrance prepares, then the entrance plays.")]
        [SerializeField] private bool _entranceBeforeRefill = true;
        [Tooltip("Distance from the sprite pivot down to the feet at scale 1, in world units. 0 when the sprites are pivoted at the feet.")]
        [SerializeField, Min(0f)] private float _feetOffset;
        [SerializeField, Min(1)] private int _damagePerBalloon = 10;
        [SerializeField, Range(1, 8)] private int _balloonsAround = 5;
        [SerializeField, Min(1f)] private float _balloonRerollSeconds = 6f;
        [SerializeField] private Color _minionBarColor = new Color(.45f, .78f, .22f);
        [SerializeField] private BossSegment[] _segments = Array.Empty<BossSegment>();
        [SerializeField] private string _hurtClip = "hurt";
        [SerializeField] private string _deathClip = "death";

        [Header("Presentation")]
        [Tooltip("Where the boss hovers, in viewport coordinates.")]
        [SerializeField] private Vector2 _hoverViewport = new Vector2(.5f, .7f);
        [SerializeField, Min(.1f)] private float _scale = 1f;
        [SerializeField] private int _sortingOrder = 2;
        [Tooltip("Viewport x of the left balloon lane (the right lane mirrors it).")]
        [SerializeField, Range(.05f, .45f)] private float _balloonLaneX = .17f;
        [Tooltip("Viewport y range the balloon lanes span.")]
        [SerializeField] private Vector2 _balloonLaneY = new Vector2(.40f, .72f);

        [Header("Stone stage (DullahanEntrance only)")]
        [Tooltip("Width of the drawn boss at scale 1, in world units; the landing dust is sized from it.")]
        [SerializeField, Min(0f)] private float _bodyWidth = 3.4f;
        [Tooltip("Stage sprite size multiplier.")]
        [SerializeField, Min(.1f)] private float _platformScale = 1.5f;
        [Tooltip("Height of the standing surface as a fraction of the stage sprite (feet go there).")]
        [SerializeField, Range(0f, 1f)] private float _platformStand = .6f;
        [Tooltip("Landing dust width as a multiple of the boss's width.")]
        [SerializeField, Min(.1f)] private float _dustScaleMultiple = 1.3f;

        [Header("Death")]
        [Tooltip("Distance from the sprite pivot down to the lowest drawn pixel of the death pose at scale 1, in world units. A boss without a stage falls until this touches the player's floor.")]
        [SerializeField, Min(0f)] private float _deathFloorOffset = 1.73f;

        [Header("Entrance (optional)")]
        [SerializeField] private bool _hasEntrance = true;
        [SerializeField] private string _whirlwindClip = "whirlwind";
        [SerializeField] private string _pillarClip = "pillar";
        [SerializeField] private Vector2 _pillarOffset = Vector2.zero;
        [SerializeField] private string _roarClip = "roar";
        [SerializeField] private AudioClip _roarSound;
        [Tooltip("Whirlwind size as a multiple of the boss scale.")]
        [SerializeField, Min(.1f)] private float _whirlwindScaleMultiple = 1.4f;
        [Tooltip("Size of the two edge fire pillars as a multiple of the boss scale.")]
        [SerializeField, Min(.1f)] private float _pillarScaleMultiple = 2f;
        [Tooltip("Width of each edge pillar relative to its height (1 keeps the sprite's own proportions).")]
        [SerializeField, Range(.1f, 1f)] private float _pillarWidthFactor = .54f;
        [Tooltip("Free space kept between each edge pillar and the player, as a multiple of the player's width.")]
        [SerializeField, Min(0f)] private float _pillarClearance = 1.2f;
        [Tooltip("Half the height of the drawn boss at scale 1, in world units. The hover point is kept this far above the player's head.")]
        [SerializeField, Min(0f)] private float _bodyHalfHeight = 1.94f;

        public string DisplayName => _displayName;
        public BossAnimationSet Animations => _animations;
        public string MinionText => _minionText;
        public string IntroText => _introText;
        public string DefeatedText => _defeatedText;
        public int MinionsBeforeBoss => _minionsBeforeBoss;
        public float SurviveSeconds => _surviveSeconds;
        public float RefillSeconds => _refillSeconds;
        public bool EntranceBeforeRefill => _entranceBeforeRefill;
        public float FeetOffset => _feetOffset;
        public float BodyWidth => _bodyWidth;
        public float PlatformScale => _platformScale;
        public float PlatformStand => _platformStand;
        public float DustScaleMultiple => _dustScaleMultiple;
        public int DamagePerBalloon => _damagePerBalloon;
        public int BalloonsAround => _balloonsAround;
        public float BalloonRerollSeconds => _balloonRerollSeconds;
        public Color MinionBarColor => _minionBarColor;
        public BossSegment[] Segments => _segments;
        public string HurtClip => _hurtClip;
        public string DeathClip => _deathClip;
        public Vector2 HoverViewport => _hoverViewport;
        public float Scale => _scale;
        public int SortingOrder => _sortingOrder;
        public float BalloonLaneX => _balloonLaneX;
        public Vector2 BalloonLaneY => _balloonLaneY;
        public bool HasEntrance => _hasEntrance;
        public string WhirlwindClip => _whirlwindClip;
        public string PillarClip => _pillarClip;
        public Vector2 PillarOffset => _pillarOffset;
        public string RoarClip => _roarClip;
        public AudioClip RoarSound => _roarSound;
        public float WhirlwindScaleMultiple => _whirlwindScaleMultiple;
        public float PillarScaleMultiple => _pillarScaleMultiple;
        public float PillarWidthFactor => _pillarWidthFactor;
        public float PillarClearance => _pillarClearance;
        public float BodyHalfHeight => _bodyHalfHeight;
        public float DeathFloorOffset => _deathFloorOffset;

        public int[] SegmentHealths()
        {
            var result = new int[_segments.Length];
            for (int i = 0; i < result.Length; i++) result[i] = Mathf.Max(1, _segments[i].health);
            return result;
        }
    }
}
