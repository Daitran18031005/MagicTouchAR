using System.Collections;
using MagicTouch.Recognition;
using UnityEngine;

namespace MagicTouch.Gameplay
{
    /// <summary>
    /// The bunch of balloons and the pose animation of one creep (owned by its EnemyBalloon).
    /// idle while 2+ balloons hold it, idle_cracked with the last one, hurt on each lost balloon,
    /// death (it keeps falling and fades out) when the last one is gone.
    /// </summary>
    public sealed class CreepCluster
    {
        // Where the creep's strings end, relative to the EnemyBalloon root. The balloon sprite's own string ends 0.92 below its centre.
        private const float AnchorY = -.75f;
        private const float BalloonStringBottom = .92f;
        private const float BodyHeight = 2f;
        private const float FadeGroundMargin = .85f;
        private const int StringSegments = 6;

        // angle (deg), extra string length, balloon scale — one row per balloon, indexed by balloon count.
        private static readonly Vector3[][] Layouts =
        {
            null,
            new[] { new Vector3(0, 0, 1f) },
            new[] { new Vector3(-20, .7f, .85f), new Vector3(20, .7f, .85f) },
            new[] { new Vector3(-30, .6f, .8f), new Vector3(0, 1.2f, .8f), new Vector3(30, .6f, .8f) },
            new[] { new Vector3(-38, .5f, .72f), new Vector3(-14, 1.3f, .72f), new Vector3(14, 1.3f, .72f), new Vector3(38, .5f, .72f) },
        };

        private struct Slot
        {
            public Transform holder;
            public SpriteRenderer balloon;
            public SpriteRenderer symbolRenderer;
            public GestureSymbol symbol;
            public bool alive;
            public Sprite[] swayFrames; // null when the balloon sprite has no sway set
            public float swayPhase;     // in frames, so neighbouring balloons do not swing in lockstep
            public float baseAngle;
            public float stringLength;
            public Transform[] segments; // the string between the creep and the balloon, bent every frame
            public Vector3 symbolBase;
        }

        private static Sprite _lineSprite;
        private static Sprite LineSprite => _lineSprite != null ? _lineSprite : (_lineSprite = MakeLineSprite());

        private readonly Slot[] _slots;
        private readonly BossVisual _visual;
        private readonly bool _startedSingle;
        private readonly float _fadeSeconds;
        private int _alive;
        private float _swayTime;

        public int Alive => _alive;

        public CreepCluster(Transform root, SpriteRenderer body, SpriteRenderer balloon0, SpriteRenderer symbol0,
            Sprite[] balloonSprites, CreepDefinition definition, int count)
        {
            count = Mathf.Clamp(count, 1, Layouts.Length - 1);
            _slots = new Slot[count];
            _alive = count;
            _startedSingle = count == 1;
            _fadeSeconds = definition.deathFadeSeconds;

            var idle = definition.animations.Get("idle");
            var frame = idle != null && idle.Length > 0 ? idle.frames[0] : null;
            if (frame != null && frame.bounds.size.y > .05f)
            {
                float scale = BodyHeight / frame.bounds.size.y;
                body.transform.localScale = new Vector3(scale, scale, 1f);
            }
            body.transform.localPosition = new Vector3(0f, AnchorY - BodyHeight * .5f + .03f, 0f);

            _visual = body.gameObject.AddComponent<BossVisual>();
            _visual.Bind(body, definition.animations);
            _visual.ResetVisual("idle");

            var layout = Layouts[count];
            for (int i = 0; i < count; i++)
            {
                float angle = layout[i].x, stringLength = layout[i].y, scale = layout[i].z;
                var holder = new GameObject("BalloonHolder" + i).transform;
                holder.SetParent(root, false);
                holder.localPosition = new Vector3(0f, AnchorY, 0f);
                holder.localRotation = Quaternion.Euler(0f, 0f, angle);

                SpriteRenderer balloon, symbol;
                if (i == 0) { balloon = balloon0; symbol = symbol0; }
                else
                {
                    balloon = Object.Instantiate(balloon0.gameObject, holder).GetComponent<SpriteRenderer>();
                    symbol = null;
                    foreach (var r in balloon.GetComponentsInChildren<SpriteRenderer>(true))
                        if (r != balloon) { symbol = r; break; }
                    if (balloonSprites != null && balloonSprites.Length > 0)
                        balloon.sprite = balloonSprites[Random.Range(0, balloonSprites.Length)];
                }
                balloon.transform.SetParent(holder, false);
                balloon.transform.localPosition = new Vector3(0f, stringLength + BalloonStringBottom * scale, 0f);
                balloon.transform.localScale = new Vector3(scale, scale, 1f);
                if (symbol != null) symbol.transform.rotation = Quaternion.identity; // glyphs stay upright on a tilted balloon

                Transform[] segments = null;
                if (stringLength > 0f)
                {
                    segments = new Transform[StringSegments];
                    for (int k = 0; k < StringSegments; k++)
                    {
                        var line = new GameObject("String" + k).AddComponent<SpriteRenderer>();
                        line.transform.SetParent(holder, false);
                        line.sprite = LineSprite;
                        line.sharedMaterial = balloon.sharedMaterial;
                        line.color = new Color(.22f, .22f, .28f, 1f);
                        line.sortingLayerID = balloon.sortingLayerID;
                        line.sortingOrder = balloon.sortingOrder - 1;
                        segments[k] = line.transform;
                    }
                }
                var sway = BalloonSwaySet.Instance;
                _slots[i] = new Slot
                {
                    holder = holder, balloon = balloon, symbolRenderer = symbol, alive = true, baseAngle = angle,
                    swayFrames = sway != null ? sway.FramesFor(balloon.sprite) : null, swayPhase = Random.value * 6f,
                    stringLength = stringLength, segments = segments, symbolBase = symbol != null ? symbol.transform.localPosition : Vector3.zero,
                };
                if (segments != null) BendString(_slots[i], 0f, 0f); // straight until the first Tick
            }
        }

        public void Tick(float dt)
        {
            _visual.Tick(dt);
            var sway = BalloonSwaySet.Instance;
            if (sway == null) return;
            _swayTime += dt;
            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (!slot.alive) continue;
                float f = _swayTime * sway.framesPerSecond + slot.swayPhase; // one loop = 6 frames
                float angle = f * Mathf.PI / 3f, lean = Mathf.Sin(angle);
                int frame = (int)f % 6;
                if (slot.swayFrames != null) slot.balloon.sprite = slot.swayFrames[frame % slot.swayFrames.Length];
                // the frames lean right first (clockwise), so the whole bunch rocks the same way about the creep's hands
                slot.holder.localRotation = Quaternion.Euler(0f, 0f, slot.baseAngle - lean * sway.rockDegrees);
                if (slot.symbolRenderer != null)
                {
                    slot.symbolRenderer.transform.rotation = Quaternion.identity; // glyph stays upright, but rides on the swinging balloon body
                    if (slot.swayFrames != null) slot.symbolRenderer.transform.localPosition = slot.symbolBase + new Vector3(sway.BodyOffsetX(frame), 0f, 0f);
                }
                if (slot.segments != null) BendString(slot, angle, sway.stringBowUnits);
            }
        }

        /// <summary>Bows the creep-to-balloon string like a rope: both ends stay put, the middle trails behind the swing.</summary>
        private static void BendString(Slot slot, float angle, float bow)
        {
            int n = slot.segments.Length;
            Vector2 previous = Vector2.zero;
            for (int k = 0; k < n; k++)
            {
                float t = (k + 1f) / n;
                float lateral = -bow * (Mathf.Sin(Mathf.PI * t) * Mathf.Sin(angle) + .35f * Mathf.Sin(2f * Mathf.PI * t) * Mathf.Sin(2f * angle + 1f));
                var next = new Vector2(lateral, slot.stringLength * t);
                var d = next - previous;
                var seg = slot.segments[k];
                seg.localPosition = previous;
                seg.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(-d.x, d.y) * Mathf.Rad2Deg);
                seg.localScale = new Vector3(.03f, d.magnitude + .012f, 1f); // a little overlap hides the joints
                previous = next;
            }
        }

        public void SetSymbol(int index, GestureSymbol symbol, Sprite symbolSprite, Color? tint)
        {
            if (index < 0 || index >= _slots.Length) return;
            var slot = _slots[index];
            slot.symbol = symbol;
            _slots[index] = slot;
            if (slot.symbolRenderer != null)
            {
                slot.symbolRenderer.sprite = symbolSprite;
                slot.symbolRenderer.gameObject.SetActive(symbolSprite != null);
            }
            if (tint.HasValue) slot.balloon.color = tint.Value;
        }

        public int IndexOf(GestureSymbol symbol)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].alive && _slots[i].symbol == symbol) return i;
            return -1;
        }

        public bool Has(GestureSymbol symbol) => IndexOf(symbol) >= 0;
        public Vector3 BalloonPosition(int index) => _slots[index].balloon.transform.position;
        public Color BalloonColor(int index) => _slots[index].balloon.color;

        public GestureSymbol FirstSymbol(GestureSymbol fallback)
        {
            for (int i = 0; i < _slots.Length; i++) if (_slots[i].alive) return _slots[i].symbol;
            return fallback;
        }

        /// <summary>Takes one balloon off. Returns true when it was the last one.</summary>
        public bool Remove(int index)
        {
            _slots[index].alive = false;
            _slots[index].holder.gameObject.SetActive(false);
            _alive--;
            if (_alive == 0) return true;
            if (_alive == 1) _visual.SetRest("idle_cracked");
            _visual.Play("hurt");
            return false;
        }

        /// <summary>Hurt first when it only ever had one balloon, then death: keep falling and fade out before the ground.</summary>
        public IEnumerator Die(EnemyBalloon owner)
        {
            if (_startedSingle)
            {
                bool hurtDone = false;
                _visual.Play("hurt", () => hurtDone = true); // the callback runs right after the clip's last frame, so no idle frame slips in
                while (!hurtDone)
                {
                    _visual.Tick(Time.deltaTime);
                    yield return null;
                }
            }
            _visual.Die("death");
            float speed = Mathf.Max(.1f, owner.FallSpeed);
            float toGround = Mathf.Max(0f, owner.transform.position.y - owner.groundY);
            float fade = Mathf.Clamp(toGround / speed * FadeGroundMargin, .15f, _fadeSeconds);
            for (float t = 0f; t < fade; t += Time.deltaTime)
            {
                _visual.Tick(Time.deltaTime);
                owner.transform.Translate(Vector3.down * (speed * Time.deltaTime), Space.World);
                _visual.SetAlpha(1f - t / fade);
                yield return null;
            }
            Object.Destroy(owner.gameObject);
        }

        private static Sprite MakeLineSprite()
        {
            var texture = Texture2D.whiteTexture;
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, 0f), texture.width);
        }
    }
}
