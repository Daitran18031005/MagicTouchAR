using System;
using UnityEngine;
using TMPro;
using MagicTouch.Recognition;
using MagicTouch.UI;

namespace MagicTouch.Gameplay
{
    /// <summary>
    /// Represents a falling enemy held aloft by a Magic Touch gesture balloon.
    /// Handles downward translation, symbol display, ground boundary detection, and pop destruction.
    /// </summary>
    public class EnemyBalloon : MonoBehaviour
    {
        [Header("Symbol & Matching")]
        [Tooltip("The gesture symbol that pops this balloon.")]
        [SerializeField] private GestureSymbol _assignedSymbol = GestureSymbol.V;
        public GestureSymbol AssignedSymbol => _assignedSymbol;

        [Header("Movement Settings")]
        [Tooltip("Speed in units/second at which the enemy falls downward.")]
        [SerializeField] private float _fallSpeed = 1.5f;

        [Tooltip("World Y coordinate below which the enemy touches the ground/castle.")]
        public float groundY = -3.8f;

        [Header("Visual Components (Optional References)")]
        [SerializeField] private SpriteRenderer _symbolRenderer;

        [Tooltip("Fallback text component to show symbol character if no sprite is assigned.")]
        [SerializeField] private TMP_Text _symbolText;

        [Tooltip("Main balloon body sprite renderer (can be tinted per symbol).")]
        [SerializeField] private SpriteRenderer _balloonRenderer;

        [Header("Enemy Character Visual Variety (Phase 6)")]
        [Tooltip("SpriteRenderer for the enemy character body hanging under the balloon.")]
        [SerializeField] private SpriteRenderer _enemyRenderer;

        [Tooltip("Array of enemy sprites for visual variety (knights, gargoyles, heavy bosses, etc.).")]
        public Sprite[] enemySprites;

        [Header("Balloon Visual Variety (Phase 6)")]
        [Tooltip("Array of balloon body sprites for visual variety.")]
        public Sprite[] balloonSprites;

        [Tooltip("Array of balloon tint colors for visual variety.")]
        public Color[] balloonColors;

        [Header("Pop VFX Assets (Phase 6)")]
        [Tooltip("Left piece of popped balloon (balloon_pop_left).")]
        public Sprite popLeftSprite;

        [Tooltip("Right piece of popped balloon (balloon_pop_right).")]
        public Sprite popRightSprite;

        [Tooltip("Sparkle sprite for pop explosion (pop_sparkle).")]
        public Sprite popSparkleSprite;

        [Header("VFX & Audio (Optional References)")]
        [Tooltip("Particle system or coin drop prefab spawned upon popping.")]
        public GameObject popParticlePrefab;

        [Tooltip("Legacy particle system spawned upon popping.")]
        [SerializeField] private GameObject _popParticlePrefab;

        [Tooltip("Sound played upon popping.")]
        [SerializeField] private AudioClip _popAudioClip;

        private bool _isPopped = false;

        public float FallSpeed
        {
            get => _fallSpeed;
            set => _fallSpeed = value;
        }

        private void Awake()
        {
            AutoWireVisualComponents();
            ApplyVisualVariety();
        }

        public void AutoWireVisualComponents()
        {
            if (_enemyRenderer == null)
            {
                // Look for an enemy child SpriteRenderer
                Transform enemyChild = transform.Find("Enemy") ?? transform.Find("Body") ?? transform.Find("Character") ?? transform.Find("Frog");
                if (enemyChild != null)
                {
                    _enemyRenderer = enemyChild.GetComponent<SpriteRenderer>();
                }
                else
                {
                    // Find any SpriteRenderer that isn't the symbol or balloon renderer
                    SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
                    for (int i = 0; i < allRenderers.Length; i++)
                    {
                        if (allRenderers[i] != _symbolRenderer && allRenderers[i] != _balloonRenderer)
                        {
                            _enemyRenderer = allRenderers[i];
                            break;
                        }
                    }
                }
            }

            if (_balloonRenderer == null)
            {
                Transform balloonChild = transform.Find("Balloon");
                if (balloonChild != null)
                {
                    _balloonRenderer = balloonChild.GetComponent<SpriteRenderer>();
                }
            }
        }

        public void ApplyVisualVariety()
        {
            // Task 1: Random enemy sprite selection
            if (enemySprites != null && enemySprites.Length > 0 && _enemyRenderer != null)
            {
                Sprite chosenEnemy = enemySprites[UnityEngine.Random.Range(0, enemySprites.Length)];
                if (chosenEnemy != null)
                {
                    ApplyEnemySprite(chosenEnemy);
                }
            }

            // Task 1: Random balloon sprite selection
            if (balloonSprites != null && balloonSprites.Length > 0 && _balloonRenderer != null)
            {
                Sprite chosenBalloon = balloonSprites[UnityEngine.Random.Range(0, balloonSprites.Length)];
                if (chosenBalloon != null)
                {
                    _balloonRenderer.sprite = chosenBalloon;
                }
            }

            // Task 1: Random balloon color selection (if not overridden by symbol tint)
            if (balloonColors != null && balloonColors.Length > 0 && _balloonRenderer != null)
            {
                Color chosenColor = balloonColors[UnityEngine.Random.Range(0, balloonColors.Length)];
                _balloonRenderer.color = chosenColor;
            }
        }

        private void Start()
        {
            // Task 3: On Start(), randomly pick one enemy sprite to display
            if (enemySprites != null && enemySprites.Length > 0 && _enemyRenderer != null)
            {
                Sprite chosen = enemySprites[UnityEngine.Random.Range(0, enemySprites.Length)];
                if (chosen != null)
                {
                    ApplyEnemySprite(chosen);
                }
            }

            // Auto-compute ground threshold dynamically if camera is available
            // Since frog enemy hangs at local y = -1.05, trigger when frog reaches bottom of view
            if (Camera.main != null && (groundY < -4.8f || groundY == -3.8f))
            {
                float bottomViewportY = Camera.main.ViewportToWorldPoint(Vector3.zero).y;
                groundY = bottomViewportY + 1.15f;
            }
        }

        private void OnEnable()
        {
            if (!EnemySpawner.ActiveEnemies.Contains(this))
            {
                EnemySpawner.ActiveEnemies.Add(this);
            }
        }

        private void OnDisable()
        {
            EnemySpawner.ActiveEnemies.Remove(this);
        }

        private void Update()
        {
            if (_isPopped) return;

            // Task 2: Freeze downward movement until camera tracking handshake confirms Playing state
            if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.Playing)
            {
                return;
            }

            // Fall downward uniformly
            transform.Translate(Vector3.down * (_fallSpeed * Time.deltaTime), Space.World);

            // Ground threshold check: lose life if enemy touches ground
            if (transform.position.y <= groundY)
            {
                HandleReachedGround();
            }
        }

        /// <summary>
        /// Configures the symbol and visual appearance of the balloon.
        /// </summary>
        public void SetSymbol(GestureSymbol symbol, Sprite symbolSprite = null, Color? balloonColor = null)
        {
            _assignedSymbol = symbol;

            // Update Sprite display if assigned
            if (_symbolRenderer != null)
            {
                _symbolRenderer.sprite = symbolSprite;
                _symbolRenderer.gameObject.SetActive(symbolSprite != null);
            }

            // Fallback: Display textual representation (e.g., 'V', 'X', '-', '|')
            if (_symbolText != null)
            {
                string displayChar = GetSymbolGlyphString(symbol);
                _symbolText.text = displayChar;
                _symbolText.gameObject.SetActive(symbolSprite == null);
            }

            // Balloon tint
            if (_balloonRenderer != null && balloonColor.HasValue)
            {
                _balloonRenderer.color = balloonColor.Value;
            }
        }

        private void ApplyEnemySprite(Sprite sprite)
        {
            if (_enemyRenderer == null || sprite == null) return;
            _enemyRenderer.sprite = sprite;

            // Normalize enemy size uniformly so all enemy sprites display at 256x256 reference size (2.0 units)
            float targetHeight = 2.0f;
            float actualHeight = sprite.bounds.size.y;
            if (actualHeight > 0.05f)
            {
                float uniformScale = targetHeight / actualHeight;
                _enemyRenderer.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
            }
        }

        /// <summary>
        /// Destroys the balloon with visual & audio feedback, removing it from ActiveEnemies.
        /// </summary>
        public void Pop(string comboText = null)
        {
            if (_isPopped) return;
            _isPopped = true;

            // Task 3: When destroyed, call UIManager to spawn floating text
            if (UIManager.Instance != null)
            {
                string text = !string.IsNullOrEmpty(comboText) ? comboText : "+10";
                Color color = !string.IsNullOrEmpty(comboText) && comboText.Contains("x")
                    ? new Color(1f, 0.88f, 0.15f, 1f)
                    : new Color(0.3f, 1f, 0.5f, 1f);
                UIManager.Instance.SpawnFloatingText(transform.position, text, color);
            }

            // Pop VFX: Popped balloon halves following the balloon's color + bursting sparkle stars
            Color balloonPopColor = (_balloonRenderer != null) ? _balloonRenderer.color : Color.white;
            Vector3 popSpawnPos = (_balloonRenderer != null) ? _balloonRenderer.transform.position : transform.position;
            BalloonPopEffect.Spawn(popSpawnPos, balloonPopColor, popLeftSprite, popRightSprite, popSparkleSprite);

            // Task 3: Spawn custom/legacy pop particle prefab if assigned
            GameObject vfxPrefab = (popParticlePrefab != null) ? popParticlePrefab : _popParticlePrefab;
            if (vfxPrefab != null)
            {
                GameObject vfx = Instantiate(vfxPrefab, transform.position, Quaternion.identity);
                Destroy(vfx, 1.5f);
            }

            // Play audio effect at position
            if (_popAudioClip != null)
            {
                AudioSource.PlayClipAtPoint(_popAudioClip, transform.position, 1.0f);
            }

            // Unregister immediately
            EnemySpawner.ActiveEnemies.Remove(this);

            // Destroy gameobject
            Destroy(gameObject);
        }

        private void HandleReachedGround()
        {
            if (_isPopped) return;
            _isPopped = true;

            EnemySpawner.ActiveEnemies.Remove(this);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoseLife();
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Maps GestureSymbol enum to a clean readable glyph for fallback text display.
        /// </summary>
        public static string GetSymbolGlyphString(GestureSymbol symbol)
        {
            return symbol switch
            {
                GestureSymbol.V => "V",
                GestureSymbol.Caret => "^",
                GestureSymbol.HorizontalLine => "—",
                GestureSymbol.VerticalLine => "|",
                GestureSymbol.O => "O",
                GestureSymbol.Z => "Z",
                GestureSymbol.W => "W",
                GestureSymbol.S => "S",
                GestureSymbol.E => "E",
                GestureSymbol.X => "X",
                _ => "?"
            };
        }
    }
}
