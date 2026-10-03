using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MagicTouch.Recognition;

namespace MagicTouch.Gameplay
{
    /// <summary>
    /// Spawns falling enemy balloons with random symbols at the top edge of the screen.
    /// Maintains the active list of on-screen enemies.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        public static EnemySpawner Instance { get; private set; }

        /// <summary>
        /// Central registry of all active, living enemies currently on screen.
        /// </summary>
        public static readonly List<EnemyBalloon> ActiveEnemies = new List<EnemyBalloon>();

        [System.Serializable]
        public struct SymbolVisualConfig
        {
            public GestureSymbol symbol;
            public Sprite sprite;
            public Color balloonTint;
        }

        [Header("Prefab & Spawning Config")]
        [Tooltip("Prefab containing the EnemyBalloon component.")]
        [SerializeField] private GameObject _enemyPrefab;

        [Tooltip("Seconds between consecutive enemy spawns.")]
        [SerializeField] private float _spawnInterval = 2.0f;

        [Tooltip("Minimum spawn interval as difficulty increases.")]
        [SerializeField] private float _minSpawnInterval = 0.8f;

        [Tooltip("Interval reduction per spawn to ramp up difficulty.")]
        [SerializeField] private float _intervalDecay = 0.02f;

        [Tooltip("Base downward fall speed for spawned enemies.")]
        [SerializeField] private float _baseFallSpeed = 1.5f;

        [Tooltip("Max fall speed as difficulty scales.")]
        [SerializeField] private float _maxFallSpeed = 3.5f;

        [Header("Spawn Position Boundaries")]
        [Tooltip("If true, automatically computes horizontal range and top edge from Camera.main.")]
        [SerializeField] private bool _autoDetectCameraBounds = true;

        [SerializeField] private float _spawnMinX = -2.2f;
        [SerializeField] private float _spawnMaxX = 2.2f;
        [SerializeField] private float _spawnY = 5.5f;

        [Header("Active Symbols Pool")]
        [Tooltip("List of symbols eligible to be randomly assigned to balloons.")]
        [SerializeField] private GestureSymbol[] _availableSymbols = new GestureSymbol[]
        {
            GestureSymbol.V,
            GestureSymbol.Caret,
            GestureSymbol.HorizontalLine,
            GestureSymbol.VerticalLine,
            GestureSymbol.O,
            GestureSymbol.Z,
            GestureSymbol.W,
            GestureSymbol.S,
            GestureSymbol.E,
            GestureSymbol.X
        };

        [Tooltip("Optional custom visual assets (sprites and colors) per symbol.")]
        [SerializeField] private List<SymbolVisualConfig> _symbolVisuals = new List<SymbolVisualConfig>();

        [Header("Enemy & Balloon Variety Pools (Phase 6)")]
        [Tooltip("Global pool of enemy character sprites assigned to newly spawned enemies (knights, gargoyles, heavy bosses).")]
        public Sprite[] enemySpritePool;

        [Tooltip("Global pool of balloon body sprites assigned to newly spawned enemies.")]
        public Sprite[] balloonSpritePool;

        [Tooltip("Global palette of balloon tint colors assigned to newly spawned enemies.")]
        public Color[] balloonColorPalette;

        private Coroutine _spawnCoroutine;
        private float _currentInterval;
        private float _currentFallSpeed;
        private readonly Dictionary<GestureSymbol, SymbolVisualConfig> _visualLookup = new Dictionary<GestureSymbol, SymbolVisualConfig>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildVisualLookup();
        }

        private void Start()
        {
            if (_autoDetectCameraBounds && Camera.main != null)
            {
                CalculateCameraBounds();
            }

            if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing)
            {
                StartSpawning();
            }
        }

        private void BuildVisualLookup()
        {
            _visualLookup.Clear();
            for (int i = 0; i < _symbolVisuals.Count; i++)
            {
                var cfg = _symbolVisuals[i];
                _visualLookup[cfg.symbol] = cfg;
            }
        }

        public void CalculateCameraBounds()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float depth = Mathf.Abs(cam.transform.position.z);
            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0.08f, 0f, depth));
            Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(0.92f, 1.08f, depth));

            _spawnMinX = bottomLeft.x;
            _spawnMaxX = topRight.x;
            _spawnY = topRight.y;
        }

        public void StartSpawning()
        {
            StopSpawning();
            _currentInterval = _spawnInterval;
            _currentFallSpeed = _baseFallSpeed;
            _spawnCoroutine = StartCoroutine(SpawnRoutine());
        }

        public void StopSpawning()
        {
            if (_spawnCoroutine != null)
            {
                StopCoroutine(_spawnCoroutine);
                _spawnCoroutine = null;
            }
        }

        public void ClearAllEnemies()
        {
            for (int i = ActiveEnemies.Count - 1; i >= 0; i--)
            {
                if (ActiveEnemies[i] != null)
                {
                    Destroy(ActiveEnemies[i].gameObject);
                }
            }
            ActiveEnemies.Clear();
        }

        private IEnumerator SpawnRoutine()
        {
            // Initial warm-up delay
            yield return new WaitForSeconds(1.0f);

            while (true)
            {
                // Task 2: Strictly wrap spawning timer logic inside if (GameManager.Instance.currentState == GameState.Playing)
                if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing)
                {
                    SpawnEnemy();

                    // Gradually scale difficulty
                    _currentInterval = Mathf.Max(_minSpawnInterval, _currentInterval - _intervalDecay);
                    _currentFallSpeed = Mathf.Min(_maxFallSpeed, _currentFallSpeed + 0.015f);

                    yield return new WaitForSeconds(_currentInterval);
                }
                else
                {
                    yield return null;
                }
            }
        }

        public EnemyBalloon SpawnEnemy()
        {
            if (_enemyPrefab == null)
            {
                Debug.LogWarning("[EnemySpawner] Enemy Prefab is not assigned in the Inspector!");
                return null;
            }

            // Pick random X within screen bounds
            float randomX = UnityEngine.Random.Range(_spawnMinX, _spawnMaxX);
            Vector3 spawnPos = new Vector3(randomX, _spawnY, 0f);

            GameObject enemyObj = Instantiate(_enemyPrefab, spawnPos, Quaternion.identity);
            EnemyBalloon enemy = enemyObj.GetComponent<EnemyBalloon>();

            if (enemy == null)
            {
                enemy = enemyObj.AddComponent<EnemyBalloon>();
            }

            // Pick random gesture symbol
            GestureSymbol chosenSymbol = PickRandomSymbol();

            // Look up sprite & color
            Sprite symbolSprite = null;
            Color? tintColor = null;

            if (_visualLookup.TryGetValue(chosenSymbol, out var visual))
            {
                symbolSprite = visual.sprite;
                if (visual.balloonTint != default && visual.balloonTint.a > 0.01f)
                {
                    tintColor = visual.balloonTint;
                }
            }

            enemy.FallSpeed = _currentFallSpeed;
            enemy.SetSymbol(chosenSymbol, symbolSprite, tintColor);

            // Task 1: Pass variety pools if the prefab does not define its own
            if ((enemy.enemySprites == null || enemy.enemySprites.Length == 0) && enemySpritePool != null && enemySpritePool.Length > 0)
            {
                enemy.enemySprites = enemySpritePool;
            }
            if ((enemy.balloonSprites == null || enemy.balloonSprites.Length == 0) && balloonSpritePool != null && balloonSpritePool.Length > 0)
            {
                enemy.balloonSprites = balloonSpritePool;
            }
            if ((enemy.balloonColors == null || enemy.balloonColors.Length == 0) && balloonColorPalette != null && balloonColorPalette.Length > 0)
            {
                enemy.balloonColors = balloonColorPalette;
            }

            enemy.AutoWireVisualComponents();
            enemy.ApplyVisualVariety();

            return enemy;
        }

        private GestureSymbol PickRandomSymbol()
        {
            if (_availableSymbols == null || _availableSymbols.Length == 0)
            {
                return GestureSymbol.V;
            }

            int index = UnityEngine.Random.Range(0, _availableSymbols.Length);
            return _availableSymbols[index];
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(_spawnMinX, _spawnY, 0), new Vector3(_spawnMaxX, _spawnY, 0));
        }
    }
}
