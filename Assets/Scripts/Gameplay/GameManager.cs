using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using MagicTouch.Recognition;
using MagicTouch.Drawing;
using MagicTouch.UI;

namespace MagicTouch.Gameplay
{
    public enum GameState
    {
        WaitingForCamera,
        Playing,
        GameOver
    }

    /// <summary>
    /// Central manager for Magic Touch combat loop:
    /// 1. Multi-Pop: Pops ALL balloons matching drawn symbol simultaneously with combo scoring.
    /// 2. 3-Lives System with ground threshold detection.
    /// 3. Game Over handling with PlayerPrefs High Score tracking, scene reloading, and auto-fallback UI.
    /// 4. Camera handshake pause: freezes gameplay until camera tracking data arrives.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Gameplay State (Task 1)")]
        [Tooltip("Current lifecycle state of the game.")]
        public GameState currentState = GameState.WaitingForCamera;

        [Tooltip("Remaining player lives.")]
        public int lives = 3;

        [Tooltip("Current score accumulated.")]
        public int score = 0;

        [Tooltip("Current combo counter.")]
        public int combo = 0;

        [Tooltip("Whether the game is currently in Game Over state.")]
        public bool isGameOver = false;

        [Header("Loading UI (Task 4)")]
        [Tooltip("Loading panel displayed on startup while waiting for camera tracking.")]
        public GameObject loadingPanel;

        [Tooltip("Status text displayed on the loading panel.")]
        public TextMeshProUGUI loadingText;

        [Header("UI References")]
        [Tooltip("Reference to the UIManager handling Heart UI, Game Over, and Floating text (Task 3).")]
        public UIManager uiManager;

        [Tooltip("Top HUD Score display.")]
        public TextMeshProUGUI scoreText;

        [Tooltip("Top HUD Lives display.")]
        public TextMeshProUGUI livesText;

        [Tooltip("Top HUD Combo multiplier display.")]
        public TextMeshProUGUI comboText;

        [Header("Pixel Art Heart UI (Phase 6)")]
        [Tooltip("Array of UI Image components representing player hearts (0 to max lives - 1).")]
        public UnityEngine.UI.Image[] heartIcons;

        [Tooltip("Sprite representing a full, active heart.")]
        public Sprite fullHeartSprite;

        [Tooltip("Sprite representing a lost, empty heart.")]
        public Sprite emptyHeartSprite;

        [Header("Arcade Floating Combo Text (Phase 6)")]
        [Tooltip("Optional custom prefab for floating combo popup (if null, dynamic pixel TMP is generated).")]
        public GameObject floatingTextPrefab;

        [Header("Environment & Pixel Art Hooks (Phase 6)")]
        [Tooltip("SpriteRenderer for the player wizard character at the bottom of the screen.")]
        public SpriteRenderer playerWizardSprite;

        [Tooltip("Direct Sprite asset for Player Wizard (e.g. Wizard.png). Can be dragged directly from Project folder!")]
        public Sprite playerWizardSpriteAsset;

        [Tooltip("SpriteRenderer for the background scenery (castle, sky, mountains).")]
        public SpriteRenderer backgroundSprite;

        [Tooltip("Direct Sprite asset for Background (e.g. CastleBackground.png). Can be dragged directly from Project folder!")]
        public Sprite backgroundSpriteAsset;

        [Header("Game Over UI")]
        [Tooltip("Game Over modal dialog/panel.")]
        public GameObject gameOverPanel;

        [Tooltip("Final Score text inside Game Over panel.")]
        public TextMeshProUGUI finalScoreText;

        [Tooltip("All-time High Score text inside Game Over panel.")]
        public TextMeshProUGUI highScoreText;

        [Header("Audio Feedback (Optional)")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _matchSuccessSound;
        [SerializeField] private AudioClip _missSound;
        [SerializeField] private AudioClip _gameOverSound;

        // Events for listeners
        public event Action<int> OnScoreChanged;
        public event Action<int> OnLivesChanged;
        public event Action OnGameOver;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Maintain 60 FPS and prevent game pause when window loses focus (e.g. clicking Python debug window)
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            AutoWireReferences();
        }

        private void Start()
        {
            // Subscribe to gesture recognizer events
            if (DrawingManager.Instance != null)
            {
                DrawingManager.Instance.OnGestureRecognized += HandleGestureRecognized;
            }

            StartNewGame();
        }

        private void OnDestroy()
        {
            if (DrawingManager.Instance != null)
            {
                DrawingManager.Instance.OnGestureRecognized -= HandleGestureRecognized;
            }
        }

        private void AutoWireReferences()
        {
            // Auto-locate HUD text references if not assigned in Inspector
            if (scoreText == null)
            {
                GameObject obj = GameObject.Find("ScoreText");
                if (obj != null) scoreText = obj.GetComponent<TextMeshProUGUI>();
            }
            if (livesText == null)
            {
                GameObject obj = GameObject.Find("LivesText");
                if (obj != null) livesText = obj.GetComponent<TextMeshProUGUI>();
            }
            if (comboText == null)
            {
                GameObject obj = GameObject.Find("ComboText");
                if (obj != null) comboText = obj.GetComponent<TextMeshProUGUI>();
            }

            // Auto-locate LoadingPanel if not assigned in Inspector
            if (loadingPanel == null)
            {
                GameObject uiCanvas = GameObject.Find("GameUI_Canvas");
                if (uiCanvas != null)
                {
                    Transform panelTransform = uiCanvas.transform.Find("LoadingPanel");
                    if (panelTransform != null)
                    {
                        loadingPanel = panelTransform.gameObject;
                    }
                }
            }

            if (loadingPanel != null && loadingText == null)
            {
                Transform lt = loadingPanel.transform.Find("LoadingText");
                if (lt != null) loadingText = lt.GetComponent<TextMeshProUGUI>();
            }

            // Auto-locate GameOverPanel if not assigned in Inspector
            if (gameOverPanel == null)
            {
                GameObject uiCanvas = GameObject.Find("GameUI_Canvas");
                if (uiCanvas != null)
                {
                    Transform panelTransform = uiCanvas.transform.Find("GameOverPanel");
                    if (panelTransform != null)
                    {
                        gameOverPanel = panelTransform.gameObject;
                    }
                }
            }

            if (gameOverPanel != null)
            {
                if (finalScoreText == null)
                {
                    Transform fst = gameOverPanel.transform.Find("FinalScoreText");
                    if (fst != null) finalScoreText = fst.GetComponent<TextMeshProUGUI>();
                }
                if (highScoreText == null)
                {
                    Transform hst = gameOverPanel.transform.Find("HighScoreText");
                    if (hst != null) highScoreText = hst.GetComponent<TextMeshProUGUI>();
                }
            }

            // Task 2: Auto-wire heart icons if not assigned
            if (heartIcons == null || heartIcons.Length == 0)
            {
                GameObject heartsObj = GameObject.Find("HeartContainer") ?? GameObject.Find("Hearts") ?? GameObject.Find("LivesContainer");
                if (heartsObj != null)
                {
                    UnityEngine.UI.Image[] foundImages = heartsObj.GetComponentsInChildren<UnityEngine.UI.Image>();
                    if (foundImages != null && foundImages.Length > 0)
                    {
                        heartIcons = foundImages;
                    }
                }
            }

            // Task 4: Auto-wire Player Wizard and Background SpriteRenderers if not assigned
            if (playerWizardSprite == null)
            {
                GameObject wizObj = GameObject.Find("PlayerWizard") ?? GameObject.Find("Wizard") ?? GameObject.Find("Player");
                if (wizObj != null) playerWizardSprite = wizObj.GetComponent<SpriteRenderer>();
            }

            if (playerWizardSpriteAsset != null)
            {
                if (playerWizardSprite == null)
                {
                    GameObject wizObj = new GameObject("PlayerWizard");
                    playerWizardSprite = wizObj.AddComponent<SpriteRenderer>();
                    wizObj.transform.position = new Vector3(0, -3.8f, 0);
                }
                playerWizardSprite.sprite = playerWizardSpriteAsset;
                playerWizardSprite.sortingOrder = 5;
            }

            if (backgroundSprite == null)
            {
                GameObject bgObj = GameObject.Find("Background") ?? GameObject.Find("Castle") ?? GameObject.Find("CastleBackground");
                if (bgObj != null) backgroundSprite = bgObj.GetComponent<SpriteRenderer>();
            }

            if (backgroundSpriteAsset != null)
            {
                if (backgroundSprite == null)
                {
                    GameObject bgObj = new GameObject("BackgroundScenery");
                    backgroundSprite = bgObj.AddComponent<SpriteRenderer>();
                    bgObj.transform.position = new Vector3(0, 0, 5);
                }
                backgroundSprite.sprite = backgroundSpriteAsset;
                backgroundSprite.sortingOrder = -10;
            }

            // Task 3: Auto-wire UIManager if not assigned
            if (uiManager == null)
            {
                uiManager = UIManager.Instance ?? FindAnyObjectByType<UIManager>();
            }

            // Synchronize references with UIManager if present
            if (uiManager != null)
            {
                if (heartIcons == null || heartIcons.Length == 0) heartIcons = uiManager.heartIcons;
                if (fullHeartSprite == null) fullHeartSprite = uiManager.fullHeartSprite;
                if (emptyHeartSprite == null) emptyHeartSprite = uiManager.emptyHeartSprite;
                if (gameOverPanel == null) gameOverPanel = uiManager.gameOverPanel;
            }
        }

        public void StartNewGame()
        {
            score = 0;
            lives = 3;
            combo = 0;
            currentState = GameState.WaitingForCamera;
            isGameOver = false;

            AutoWireReferences();

            if (uiManager != null)
            {
                uiManager.UpdateHearts(lives);
                uiManager.HideGameOver();
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
                if (loadingText != null)
                {
                    loadingText.text = "Waiting for Camera Setup...\n<size=22><color=#66CCFF>Start Python Streamer or Pinch in Editor</color></size>";
                }
            }
            else
            {
                CreateFallbackLoadingUI();
            }

            UpdateUI();

            if (EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.ClearAllEnemies();
                // Spawning begins strictly in StartGame() once handshake is confirmed
            }
        }

        /// <summary>
        /// Starts the gameplay loop once the camera and hand tracking handshake succeeds (Task 1).
        /// </summary>
        public void StartGame()
        {
            if (currentState == GameState.Playing) return;

            Debug.Log("<color=green>[GameManager] Camera handshake received! Starting game loop.</color>");
            currentState = GameState.Playing;
            isGameOver = false;

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            if (EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.ClearAllEnemies();
                EnemySpawner.Instance.StartSpawning();
            }
        }

        /// <summary>
        /// Callback invoked when DrawingManager finishes recognizing a gesture.
        /// </summary>
        private void HandleGestureRecognized(int handIndex, PRecognitionResult result)
        {
            if (currentState != GameState.Playing)
            {
                Debug.Log("[GameManager] Gesture ignored: Game is waiting for camera setup or over.");
                return;
            }

            if (result.IsMatch && result.Symbol != GestureSymbol.Unknown && result.Symbol != GestureSymbol.None)
            {
                OnSymbolRecognized(result.Symbol);
            }
            else
            {
                // Ambiguous or unrecognized stroke resets combo
                combo = 0;
                UpdateComboUI();
            }
        }

        /// <summary>
        /// Multi-Pop Mechanic:
        /// Finds and pops ALL active enemies with balloons matching drawnSymbol simultaneously.
        /// Awards combo points based on number of popped balloons:
        /// 1 balloon = 10 pts, 2 balloons = 30 pts, 3 balloons = 60 pts, etc.
        /// </summary>
        public void OnSymbolRecognized(GestureSymbol drawnSymbol)
        {
            if (currentState != GameState.Playing) return;

            // 1. Gather all matching enemies currently alive on screen
            List<EnemyBalloon> toPop = new List<EnemyBalloon>();
            List<EnemyBalloon> activeList = EnemySpawner.ActiveEnemies;

            for (int i = 0; i < activeList.Count; i++)
            {
                EnemyBalloon candidate = activeList[i];
                if (candidate != null && candidate.AssignedSymbol == drawnSymbol)
                {
                    toPop.Add(candidate);
                }
            }

            // 2. If matching balloons exist, pop them simultaneously
            if (toPop.Count > 0)
            {
                int count = toPop.Count;

                // Triangular combo scoring: 1 -> 10, 2 -> 30, 3 -> 60, 4 -> 100
                int pointsEarned = 10 * count * (count + 1) / 2;
                score += pointsEarned;
                combo += count;

                if (_matchSuccessSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_matchSuccessSound);
                }

                Debug.Log($"[GameManager] Multi-Pop! Popped {count} '{drawnSymbol}' balloons! +{pointsEarned} pts (Score: {score}, Combo: x{combo})");

                // Task 3: Pop all collected enemies with arcade floating combo text via EnemyBalloon & UIManager
                for (int i = 0; i < toPop.Count; i++)
                {
                    if (toPop[i] != null)
                    {
                        string scoreStr = (count > 1)
                            ? $"+{pointsEarned / count} <size=70%><color=#FF66AA>x{combo}!</color></size>"
                            : $"+{pointsEarned}";

                        toPop[i].Pop(scoreStr);
                    }
                }

                UpdateUI();
                OnScoreChanged?.Invoke(score);
            }
            else
            {
                // Gesture recognized, but no matching balloon currently on screen
                combo = 0;
                UpdateComboUI();

                if (_missSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_missSound, 0.6f);
                }

                Debug.Log($"[GameManager] Symbol '{drawnSymbol}' recognized, but no matching balloon on screen.");
            }
        }

        /// <summary>
        /// Called when an enemy touches the ground threshold.
        /// Decrements player life and triggers Game Over when lives reach 0.
        /// </summary>
        public void LoseLife()
        {
            if (currentState != GameState.Playing) return;

            lives--;
            combo = 0;

            Debug.Log($"<color=orange>[GameManager] Enemy landed! Remaining lives: {lives}</color>");

            if (uiManager != null)
            {
                uiManager.UpdateHearts(lives);
            }

            UpdateUI();
            OnLivesChanged?.Invoke(lives);

            if (lives <= 0)
            {
                GameOver();
            }
        }

        /// <summary>
        /// Triggers Game Over state, stops spawner, updates High Score, and displays Game Over screen.
        /// </summary>
        public void GameOver()
        {
            if (currentState == GameState.GameOver) return;
            currentState = GameState.GameOver;
            isGameOver = true;

            // Stop spawning new enemies and clear existing falling balloons
            if (EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.StopSpawning();
                EnemySpawner.Instance.ClearAllEnemies();
            }

            // High Score calculation & persistence via PlayerPrefs
            int highScore = PlayerPrefs.GetInt("HighScore", 0);
            if (score > highScore)
            {
                highScore = score;
                PlayerPrefs.SetInt("HighScore", highScore);
                PlayerPrefs.Save();
                Debug.Log($"[GameManager] NEW HIGH SCORE: {highScore}!");
            }

            AutoWireReferences();

            // Update Game Over UI elements
            if (finalScoreText != null)
            {
                finalScoreText.text = $"Score: {score}";
            }

            if (highScoreText != null)
            {
                highScoreText.text = $"High Score: {highScore}";
            }

            if (uiManager != null)
            {
                uiManager.ShowGameOver(score, highScore);
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }
            else
            {
                CreateFallbackGameOverUI(score, highScore);
            }

            if (_gameOverSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_gameOverSound);
            }

            Debug.Log($"<color=red>[GameManager] GAME OVER! Final Score: {score}, High Score: {highScore}</color>");
            OnGameOver?.Invoke();
        }

        /// <summary>
        /// Reloads the active scene to restart gameplay.
        /// </summary>
        public void RestartGame()
        {
            Debug.Log("[GameManager] Restarting game...");
            isGameOver = false;
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private void UpdateUI()
        {
            if (scoreText != null)
            {
                scoreText.text = $"Score: {score}";
            }

            if (livesText != null)
            {
                livesText.text = $"Lives: {lives}";
            }

            UpdateHeartUI();
            UpdateComboUI();
        }

        /// <summary>
        /// Task 2: Pixel Art Heart UI.
        /// Visually swaps heart icon sprites from fullHeartSprite to emptyHeartSprite based on remaining lives.
        /// </summary>
        public void UpdateHeartUI()
        {
            if (heartIcons == null || heartIcons.Length == 0) return;

            for (int i = 0; i < heartIcons.Length; i++)
            {
                if (heartIcons[i] == null) continue;

                if (i < lives)
                {
                    if (fullHeartSprite != null)
                    {
                        heartIcons[i].sprite = fullHeartSprite;
                    }
                    heartIcons[i].color = Color.white;
                }
                else
                {
                    if (emptyHeartSprite != null)
                    {
                        heartIcons[i].sprite = emptyHeartSprite;
                        heartIcons[i].color = Color.white;
                    }
                    else
                    {
                        // Fallback if empty heart sprite isn't assigned: dim the lost heart
                        heartIcons[i].color = new Color(1f, 1f, 1f, 0.25f);
                    }
                }
            }
        }

        /// <summary>
        /// Task 3: Instantiates temporary floating arcade text at the enemy's position showing the combo score.
        /// Floats upward and fades out over 1 second.
        /// </summary>
        public void SpawnFloatingScore(Vector3 worldPos, string text, Color? textColor = null)
        {
            if (uiManager != null)
            {
                uiManager.SpawnFloatingText(worldPos, text, textColor);
            }
            else
            {
                StartCoroutine(FloatingScoreRoutine(worldPos, text, textColor ?? new Color(1f, 0.9f, 0.2f, 1f)));
            }
        }

        private System.Collections.IEnumerator FloatingScoreRoutine(Vector3 worldPos, string text, Color color)
        {
            GameObject popupObj;
            TextMeshPro tmp;

            if (floatingTextPrefab != null)
            {
                popupObj = Instantiate(floatingTextPrefab, worldPos, Quaternion.identity);
                tmp = popupObj.GetComponentInChildren<TextMeshPro>();
            }
            else
            {
                popupObj = new GameObject("FloatingComboText");
                popupObj.transform.position = worldPos + Vector3.up * 0.2f;

                tmp = popupObj.AddComponent<TextMeshPro>();
                tmp.text = text;
                tmp.fontSize = 6.5f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                tmp.sortingOrder = 200; // Above balloons (0) and drawing trails (100)
            }

            if (tmp != null)
            {
                tmp.text = text;
                tmp.color = color;
            }

            float duration = 1.0f;
            float elapsed = 0f;
            Vector3 startPos = popupObj.transform.position;
            Vector3 targetPos = startPos + Vector3.up * 1.2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                if (popupObj != null)
                {
                    popupObj.transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t));

                    // Scale bounce animation
                    float scale = 1.0f + 0.35f * Mathf.Sin(t * Mathf.PI);
                    popupObj.transform.localScale = new Vector3(scale, scale, 1f);
                }

                if (tmp != null)
                {
                    Color c = color;
                    c.a = Mathf.Lerp(1.0f, 0.0f, t);
                    tmp.color = c;
                }

                yield return null;
            }

            if (popupObj != null)
            {
                Destroy(popupObj);
            }
        }

        private void UpdateComboUI()
        {
            if (comboText != null)
            {
                if (combo > 1)
                {
                    comboText.text = $"Combo x{combo}!";
                    comboText.gameObject.SetActive(true);
                }
                else
                {
                    comboText.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Bulletproof fallback: dynamically creates a clean Game Over UI overlay
        /// if the scene didn't have one pre-configured in the hierarchy.
        /// </summary>
        private void CreateFallbackGameOverUI(int currentScore, int highScore)
        {
            GameObject canvasObj = GameObject.Find("GameUI_Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("GameUI_Canvas");
                Canvas c = canvasObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 50;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            GameObject panel = new GameObject("GameOverPanel");
            panel.transform.SetParent(canvasObj.transform, false);

            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(650, 750);

            Image pimg = panel.AddComponent<Image>();
            pimg.color = new Color(0.05f, 0.05f, 0.1f, 0.94f);

            // Title
            CreateTextElement(panel.transform, "GameOverTitle", "GAME OVER", 64, new Color(1f, 0.25f, 0.3f), new Vector2(0, 220));
            // Final Score
            CreateTextElement(panel.transform, "FinalScoreText", $"Score: {currentScore}", 46, Color.white, new Vector2(0, 80));
            // High Score
            CreateTextElement(panel.transform, "HighScoreText", $"High Score: {highScore}", 40, new Color(1f, 0.85f, 0.2f), new Vector2(0, -10));

            // Restart Button
            GameObject btnObj = new GameObject("RestartButton");
            btnObj.transform.SetParent(panel.transform, false);
            RectTransform brt = btnObj.AddComponent<RectTransform>();
            brt.sizeDelta = new Vector2(300, 90);
            brt.anchoredPosition = new Vector2(0, -160);

            Image bimg = btnObj.AddComponent<Image>();
            bimg.color = new Color(0.18f, 0.75f, 0.35f);

            Button btn = btnObj.AddComponent<Button>();
            btn.onClick.AddListener(RestartGame);

            CreateTextElement(btnObj.transform, "Text", "RESTART", 36, Color.white, Vector2.zero);

            gameOverPanel = panel;
        }

        /// <summary>
        /// Bulletproof fallback: dynamically creates a clean Waiting for Camera Setup UI overlay
        /// if the scene didn't have one pre-configured in the hierarchy (Task 4).
        /// </summary>
        private void CreateFallbackLoadingUI()
        {
            GameObject canvasObj = GameObject.Find("GameUI_Canvas");
            if (canvasObj == null)
            {
                canvasObj = new GameObject("GameUI_Canvas");
                Canvas c = canvasObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 60;
                canvasObj.AddComponent<CanvasScaler>();
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            GameObject panel = new GameObject("LoadingPanel");
            panel.transform.SetParent(canvasObj.transform, false);

            RectTransform prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(1f, 1f);
            prt.sizeDelta = Vector2.zero;
            prt.anchoredPosition = Vector2.zero;

            Image pimg = panel.AddComponent<Image>();
            pimg.color = new Color(0.04f, 0.05f, 0.09f, 0.88f);

            loadingText = CreateTextElement(panel.transform, "LoadingText",
                "Waiting for Camera Setup...\n<size=22><color=#66CCFF>Start Python Streamer or Pinch in Editor</color></size>",
                34, Color.white, new Vector2(0, 0));

            loadingPanel = panel;
        }

        private TextMeshProUGUI CreateTextElement(Transform parent, string name, string text, float size, Color col, Vector2 pos)
        {
            GameObject obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            RectTransform rt = obj.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(550, 80);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = col;
            tmp.alignment = TextAlignmentOptions.Center;
            return tmp;
        }
    }
}
