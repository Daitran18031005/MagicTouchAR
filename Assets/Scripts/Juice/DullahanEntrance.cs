using System;
using System.Collections;
using System.Collections.Generic;
using MagicTouch.Gameplay;
using UnityEngine;

namespace MagicTouch.Juice
{
    /// <summary>
    /// Mini-boss arrival on a stone stage: the stage rises out of the floor while the bar refills (the screen shakes and rubble
    /// rains off it, like an ancient trap firing), then the boss leaps in from above, lands (the stage shakes, dust and rubble
    /// burst, kept inside the stage) and settles into its idle clip. When the boss dies the stage sinks back into the floor,
    /// corpse and all, with the same shake and rubble. Lives on the boss root. Every effect is built once at runtime from clips
    /// of the boss's animation set; a missing clip is skipped. Nothing here uses SpriteMask (its stencil ranges proved unreliable
    /// when the effects are rebuilt on a second run): the parts that must stay inside the stage or above the floor are cut out of
    /// the sprites themselves, see <see cref="Clip(Sprite, Vector3, float, Rect)"/>.
    /// </summary>
    public sealed class DullahanEntrance : MonoBehaviour, IBossEntrance
    {
        private const string PlatformIdle = "platform_idle", PlatformLanding = "platform_landing", PlatformSettled = "platform_settled";
        private const string DustClip = "landing_dust", DebrisClip = "stone_debris";
        private const string JumpClip = "jump", LandClip = "land";
        private const float MinFall = .5f;
        private const float DustFill = .85f;    // share of the dust cell's width the cloud covers at its peak
        private const float DustBase = .4f;     // the cloud's base sits this far (fraction of its cell height) below the cell centre
        private const float TopInset = .28f;    // the drawn stage starts this far (fraction of its cell height) below the cell's top
        private const float SinkSeconds = 2.6f;
        private const int ChunkCount = 64, PuffCount = 16;
        private const float Gravity = 15f, ChunksPerSecond = 34f, PuffsPerSecond = 8f;

        private sealed class Particle
        {
            public SpriteRenderer renderer;
            public Vector2 velocity;
            public float life, maxLife, spin, size;
        }

        private BossFxSprite _platform, _dust, _debris;
        private readonly Dictionary<(Sprite, int, int, int, int), Sprite> _clipped = new Dictionary<(Sprite, int, int, int, int), Sprite>();
        private Sprite _stageSprite, _square, _round;
        private Transform _rubbleRoot;
        private Particle[] _chunks, _puffs;
        private float _platformScale, _floorY, _platformHeight, _standY, _stageX, _stageWidth, _platformRise;
        private float _chunkDebt, _puffDebt, _shakeTimer;
        private int _layer, _order, _bossOrder;
        private bool _ready, _emitting, _cropped;

        public float StandY => _standY;

        private void EnsureObjects()
        {
            if (_platform != null) return;
            _platform = BuildFx("DullahanStage");
            _dust = BuildFx("DullahanDust");
            _debris = BuildFx("DullahanDebris");
            _square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * .5f, 4f);
            _round = BuildRoundSprite();
            _rubbleRoot = new GameObject("DullahanRubble").transform;
            _chunks = BuildParticles("Chunk", ChunkCount, _square);
            _puffs = BuildParticles("Puff", PuffCount, _round);
        }

        private BossFxSprite BuildFx(string name)
        {
            // Not a child of the boss root: the root moves during the leap and would carry the stage and the dust with it.
            var go = new GameObject(name, typeof(SpriteRenderer), typeof(BossFxSprite));
            go.SetActive(false);
            return go.GetComponent<BossFxSprite>();
        }

        private Particle[] BuildParticles(string name, int count, Sprite sprite)
        {
            var result = new Particle[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(name, typeof(SpriteRenderer));
                go.transform.SetParent(_rubbleRoot, false);
                var renderer = go.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.enabled = false;
                result[i] = new Particle { renderer = renderer };
            }
            return result;
        }

        private static Sprite BuildRoundSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), Vector2.one * size * .5f) / (size * .5f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3f)));
                }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, size);
        }

        public void Prepare(BossDefinition def, BossVisual visual, Vector3 hover, Bounds player)
        {
            EnsureObjects();
            ClearEffects();
            var clip = def.Animations.Get(PlatformIdle);
            _ready = clip != null && clip.Length > 0;
            _floorY = player.min.y;
            _stageX = hover.x;
            _bossOrder = def.SortingOrder;
            _emitting = false;
            _shakeTimer = 0f;
            visual.SetVisible(false);
            if (!_ready)
            {
                // No stage art: the boss simply stands a little above the player's head.
                _platformHeight = Mathf.Max(0f, player.max.y + .35f - _floorY);
                _standY = _floorY + _platformHeight;
                _stageWidth = def.BodyWidth * def.Scale;
                return;
            }
            Vector2 size = clip.frames[0].bounds.size * def.PlatformScale;
            _platformHeight = size.y;
            _stageWidth = size.x;
            _standY = _floorY + _platformHeight * def.PlatformStand;
            _layer = visual.Body != null ? visual.Body.sortingLayerID : 0;
            _order = def.SortingOrder - 1;
            _platform.Renderer.sortingLayerID = _layer;
            _platform.Renderer.sortingOrder = _order;
            _platformScale = def.PlatformScale;
            _stageSprite = clip.frames[0];
            _platformRise = 0f;
            _platform.Play(clip, StagePosition(0f), def.PlatformScale, false, 0f, true);
            ShowStage(0f);
        }

        /// <summary>
        /// Draws the stage lifted to <paramref name="rise"/> (0 = buried, 1 = fully out): only the part above the floor line is
        /// shown. The stage sprite is cut at the floor line, one cached sprite per pixel row.
        /// </summary>
        private void ShowStage(float rise)
        {
            _platformRise = rise;
            var at = StagePosition(rise);
            _platform.transform.position = at;
            var renderer = _platform.Renderer;
            if (rise >= 1f)
            {
                renderer.sprite = _stageSprite;
                renderer.enabled = true;
                return;
            }
            var piece = Clip(_stageSprite, at, _platformScale, new Rect(_stageX - 100f, _floorY, 200f, 100f));
            renderer.sprite = piece;
            renderer.enabled = piece != null;
        }

        /// <summary>
        /// A copy of <paramref name="source"/> showing only what lies inside the world rectangle <paramref name="area"/>, for a sprite
        /// drawn at <paramref name="position"/> with a uniform <paramref name="scale"/> (no rotation or flip). Null when nothing is left.
        /// The pieces share the texture and are cached by pixel box, so a rising stage creates at most one per pixel row.
        /// </summary>
        private Sprite Clip(Sprite source, Vector3 position, float scale, Rect area)
        {
            if (source == null) return null;
            Rect rect = source.textureRect;
            float pixelsPerUnit = source.pixelsPerUnit;
            Vector2 pivot = source.pivot; // pixels from the rect's bottom-left
            float k = pixelsPerUnit / scale;
            int u0 = Mathf.Clamp(Mathf.CeilToInt(pivot.x + (area.xMin - position.x) * k), 0, (int)rect.width);
            int u1 = Mathf.Clamp(Mathf.FloorToInt(pivot.x + (area.xMax - position.x) * k), 0, (int)rect.width);
            int v0 = Mathf.Clamp(Mathf.CeilToInt(pivot.y + (area.yMin - position.y) * k), 0, (int)rect.height);
            int v1 = Mathf.Clamp(Mathf.FloorToInt(pivot.y + (area.yMax - position.y) * k), 0, (int)rect.height);
            if (u1 <= u0 || v1 <= v0) return null;
            if (u0 == 0 && v0 == 0 && u1 == (int)rect.width && v1 == (int)rect.height) return source;
            var key = (source, u0, u1, v0, v1);
            if (_clipped.TryGetValue(key, out var found)) return found;
            var piece = Sprite.Create(source.texture, new Rect(rect.x + u0, rect.y + v0, u1 - u0, v1 - v0),
                new Vector2((pivot.x - u0) / (u1 - u0), (pivot.y - v0) / (v1 - v0)), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            _clipped[key] = piece;
            return piece;
        }

        private BossClip Clip(BossClip source, Vector3 position, float scale, Rect area)
        {
            var result = new BossClip { key = source.key, loop = source.loop, frames = new Sprite[source.Length], seconds = source.seconds };
            for (int i = 0; i < source.Length; i++) result.frames[i] = Clip(source.frames[i], position, scale, area);
            return result;
        }

        private Vector3 StagePosition(float rise) =>
            new Vector3(_stageX, _floorY + _platformHeight * .5f - (1f - Mathf.Clamp01(rise)) * _platformHeight, 0f);

        /// <summary>Called every playing frame while the bar refills: lifts the stage, shakes the screen, drops rubble.</summary>
        public void SetRise(float t)
        {
            if (!_ready) return;
            ShowStage(t);
            _emitting = t > 0f && t < 1f;
            if (_emitting) Rumble(Time.unscaledDeltaTime, .05f + .06f * t);
            TickRubble(Time.unscaledDeltaTime);
        }

        public IEnumerator Run(BossDefinition def, BossVisual visual, Vector3 hover, Bounds player, Func<bool> isPlaying)
        {
            EnsureObjects();
            _emitting = false;
            var anim = def.Animations;
            var cam = Camera.main;
            float top = cam != null ? cam.ViewportToWorldPoint(new Vector3(.5f, 1f, Mathf.Abs(cam.transform.position.z))).y : 5f;
            float restY = _standY + def.FeetOffset * def.Scale; // the root sits this far above the feet
            Vector3 land = new Vector3(_stageX, restY, 0f);
            Vector3 start = new Vector3(_stageX, top + def.BodyHalfHeight * 2f * def.Scale + def.FeetOffset * def.Scale, 0f);

            // 1. The boss leaps in from above the screen (the last rubble still falls).
            transform.position = start;
            visual.SetAlpha(1f);
            visual.SetVisible(true);
            visual.Play(JumpClip);
            float fall = Mathf.Max(MinFall, visual.Duration(JumpClip));
            yield return Step(fall, isPlaying, TickEffects, p => transform.position = Vector3.Lerp(start, land, p * p));
            transform.position = land;

            // 2. It lands: the stage shakes, dust and rubble burst on top of it.
            visual.Play(LandClip);
            float landSeconds = Mathf.Max(.6f, visual.Duration(LandClip));
            StartLandingEffects(def, anim);
            yield return Step(landSeconds + .3f, isPlaying, TickEffects, null);

            // 3. Everything settles.
            if (_ready && !_platform.IsPlaying) PlaySettled(def, anim);
            _dust.Stop();
            _debris.Stop();
            yield return Step(.25f, isPlaying, TickEffects, null);
        }

        /// <summary>The death clip plays on the stage, then the stage sinks into the floor with the corpse on it.</summary>
        public IEnumerator Defeat(BossDefinition def, BossVisual visual, Bounds player, float deathSeconds, Func<bool> isPlaying)
        {
            EnsureObjects();
            yield return Step(deathSeconds, isPlaying, TickEffects, null);
            if (!_ready) yield break;

            // The stage and everything standing on it go down together; the corpse dissolves into dust as it nears the floor.
            Vector3 from = transform.position;
            var settled = def.Animations.Get(PlatformSettled) ?? def.Animations.Get(PlatformIdle);
            _stageSprite = settled.frames[0];
            _platform.Stop(); // the stage is driven by hand from here: no clip may swap its sprite back
            _platform.gameObject.SetActive(true);
            _dust.Stop();
            _debris.Stop();
            _emitting = true;
            yield return Step(SinkSeconds, isPlaying, dt =>
            {
                _dust.Tick(dt);
                TickRubble(dt);
                Rumble(dt, .06f + .05f * (1f - _platformRise));
            }, p =>
            {
                float rise = 1f - p * p * (3f - 2f * p); // eases in and out
                ShowStage(rise);
                transform.position = from - Vector3.up * (1f - rise) * _platformHeight;
                visual.SetAlpha(1f - Mathf.Clamp01((p - .25f) / .55f));
            });
            _emitting = false;
            // The last rubble finishes falling.
            yield return Step(.6f, isPlaying, TickEffects, null);
        }

        private void StartLandingEffects(BossDefinition def, BossAnimationSet anim)
        {
            var landing = anim.Get(PlatformLanding);
            if (_ready && landing != null)
                _platform.Play(landing, StagePosition(1f), def.PlatformScale, false, 0f, false, 0f, -1, null, () => PlaySettled(def, anim));

            var dust = anim.Get(DustClip);
            var debris = anim.Get(DebrisClip);
            int layer = _platform.Renderer.sortingLayerID;
            int order = def.SortingOrder + 1;
            _dust.Renderer.sortingLayerID = _debris.Renderer.sortingLayerID = layer;
            _dust.Renderer.sortingOrder = _debris.Renderer.sortingOrder = order;
            // The dust is kept inside the stage's width and above its top surface.
            var inside = new Rect(_stageX - _stageWidth * .5f, _standY - .4f, _stageWidth, 8.4f);

            // A cloud 1.3 times the boss's width, sitting on the stage surface.
            if (dust != null && dust.Length > 0)
            {
                float wanted = def.DustScaleMultiple * def.BodyWidth * def.Scale;
                float scale = wanted / (dust.frames[0].bounds.size.x * DustFill);
                var position = new Vector3(_stageX, _standY + dust.frames[0].bounds.size.y * scale * DustBase, 0f);
                _dust.Play(Clip(dust, position, scale, inside), position, scale, false, 0f, false);
                if (debris != null && debris.Length > 0)
                    _debris.Play(Clip(debris, position, scale, inside), position, scale, false, 0f, false);
            }
        }

        // The platform keeps the cracks of the impact (the last landing frame) for the rest of the fight.
        private void PlaySettled(BossDefinition def, BossAnimationSet anim)
        {
            var clip = anim.Get(PlatformSettled) ?? anim.Get(PlatformIdle);
            if (clip != null) _platform.Play(clip, StagePosition(1f), def.PlatformScale, false, 0f, true);
        }

        private void TickEffects(float dt)
        {
            _platform.Tick(dt);
            _dust.Tick(dt);
            _debris.Tick(dt);
            TickRubble(dt);
        }

        // ---- shake and rubble --------------------------------------------------------------------------------------

        private void Rumble(float dt, float strength)
        {
            _shakeTimer -= dt;
            if (_shakeTimer > 0f) return;
            _shakeTimer = .28f;
            ScreenHitFeedback.Shake(strength, .34f);
        }

        private float StageTopY() => _floorY + _platformHeight * .5f - (1f - Mathf.Clamp01(_platformRise)) * _platformHeight
            + _platformHeight * (.5f - TopInset);

        private void TickRubble(float dt)
        {
            if (_chunks == null) return;
            if (_emitting && _ready)
            {
                float top = StageTopY();
                if (top > _floorY + .25f)
                {
                    _chunkDebt += dt * ChunksPerSecond;
                    _puffDebt += dt * PuffsPerSecond;
                    while (_chunkDebt >= 1f) { _chunkDebt -= 1f; SpawnChunk(top); }
                    while (_puffDebt >= 1f) { _puffDebt -= 1f; SpawnPuff(); }
                }
            }
            foreach (var chunk in _chunks)
            {
                if (chunk.life <= 0f) continue;
                chunk.velocity.y -= Gravity * dt;
                var t = chunk.renderer.transform;
                t.position += (Vector3)(chunk.velocity * dt);
                t.Rotate(0f, 0f, chunk.spin * dt);
                chunk.life -= dt;
                if (t.position.y <= _floorY + .04f || chunk.life <= 0f)
                {
                    chunk.life = 0f;
                    chunk.renderer.enabled = false;
                    if (UnityEngine.Random.value < .5f) SpawnPuff(t.position.x, .35f);
                }
            }
            foreach (var puff in _puffs)
            {
                if (puff.life <= 0f) continue;
                puff.life -= dt;
                float k = 1f - Mathf.Clamp01(puff.life / puff.maxLife);
                var t = puff.renderer.transform;
                t.position += (Vector3)(puff.velocity * dt);
                t.localScale = Vector3.one * puff.size * (1f + .7f * k);
                var color = puff.renderer.color;
                color.a = .65f * (1f - k) * (1f - k);
                puff.renderer.color = color;
                if (puff.life <= 0f) puff.renderer.enabled = false;
            }
        }

        private static readonly Color[] StoneColors =
        {
            new Color(.50f, .46f, .54f), new Color(.38f, .35f, .44f), new Color(.62f, .58f, .64f), new Color(.28f, .26f, .34f)
        };

        private void SpawnChunk(float top)
        {
            foreach (var chunk in _chunks)
            {
                if (chunk.life > 0f) continue;
                // Most of it shakes loose off the top face, the rest off the two side edges.
                bool side = UnityEngine.Random.value < .35f;
                float x = side
                    ? _stageX + (UnityEngine.Random.value < .5f ? -.5f : .5f) * _stageWidth * .92f
                    : _stageX + UnityEngine.Random.Range(-.46f, .46f) * _stageWidth;
                float y = top - UnityEngine.Random.Range(0f, .9f);
                chunk.size = UnityEngine.Random.Range(.09f, .26f);
                chunk.velocity = new Vector2(UnityEngine.Random.Range(-.5f, .5f) + (side ? Mathf.Sign(x - _stageX) * .6f : 0f), UnityEngine.Random.Range(-.5f, .8f));
                chunk.spin = UnityEngine.Random.Range(-240f, 240f);
                chunk.maxLife = chunk.life = 3f;
                var t = chunk.renderer.transform;
                t.position = new Vector3(x, y, 0f);
                t.localScale = Vector3.one * chunk.size;
                t.rotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 90f));
                chunk.renderer.sortingLayerID = _layer;
                chunk.renderer.sortingOrder = _bossOrder + 1;
                chunk.renderer.color = StoneColors[UnityEngine.Random.Range(0, StoneColors.Length)];
                chunk.renderer.enabled = true;
                return;
            }
        }

        private void SpawnPuff() =>
            SpawnPuff(_stageX + UnityEngine.Random.Range(-.5f, .5f) * _stageWidth, 1f);

        private void SpawnPuff(float x, float scale)
        {
            foreach (var puff in _puffs)
            {
                if (puff.life > 0f) continue;
                puff.size = UnityEngine.Random.Range(.7f, 1.3f) * scale;
                puff.maxLife = puff.life = UnityEngine.Random.Range(.6f, .9f);
                puff.velocity = new Vector2(UnityEngine.Random.Range(-.25f, .25f), UnityEngine.Random.Range(.25f, .55f));
                var t = puff.renderer.transform;
                t.position = new Vector3(x, _floorY + .12f, 0f);
                t.localScale = Vector3.one * puff.size;
                puff.renderer.sortingLayerID = _layer;
                puff.renderer.sortingOrder = _bossOrder + 1;
                puff.renderer.color = new Color(.7f, .66f, .74f, .65f);
                puff.renderer.enabled = true;
                return;
            }
        }

        private void ClearEffects()
        {
            if (_chunks == null) return;
            foreach (var chunk in _chunks) { chunk.life = 0f; chunk.renderer.enabled = false; }
            foreach (var puff in _puffs) { puff.life = 0f; puff.renderer.enabled = false; }
            _chunkDebt = _puffDebt = 0f;
        }

        public void Cancel()
        {
            StopAllCoroutines();
            _emitting = false;
            if (_platform != null) _platform.Stop();
            if (_dust != null) _dust.Stop();
            if (_debris != null) _debris.Stop();
            ClearEffects();
        }

        private static IEnumerator Step(float seconds, Func<bool> isPlaying, Action<float> tick, Action<float> progress)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (isPlaying())
                {
                    float dt = Time.unscaledDeltaTime;
                    elapsed += dt;
                    tick?.Invoke(dt);
                    progress?.Invoke(Mathf.Clamp01(elapsed / seconds));
                }
                yield return null;
            }
            progress?.Invoke(1f);
        }
    }
}
