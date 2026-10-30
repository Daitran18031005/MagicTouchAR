using System;
using System.Collections.Generic;
using UnityEngine;
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
        GameOver,
        MainMenu,
        Paused
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

        [Header("Optional timed versus mode")]
        [SerializeField] private GameMode _gameMode = GameMode.Solo;
        [SerializeField] private int _versusMinutes = 3;
        public GameObject versusHUD;
        public TextMeshProUGUI leftScoreText;
        public TextMeshProUGUI rightScoreText;
        public TextMeshProUGUI matchTimerText;
        private readonly VersusMatch _versusMatch = new VersusMatch();
        private int _scoringPlayer = -1;
        private int _lastSymbolPopCount;
        private int _shownLeft = -1, _shownRight = -1, _shownSeconds = -1;
        public bool IsVersus => _gameMode == GameMode.Versus;
        public bool SoloBossVictory { get; private set; }
        public event Action RoundReset;
        public event Action RoundStarted;

        public void CompleteSoloBossRound()
        {
            if (IsVersus || currentState != GameState.Playing) return;
            SoloBossVictory = true;
            GameOver();
        }
        public VersusMatch VersusRound => _versusMatch;
        public double RemainingMatchSeconds => _versusMatch.Remaining(Time.timeAsDouble);

        public bool ConfigureMode(GameMode mode, int minutes = 3)
        {
            if (currentState == GameState.Playing || currentState == GameState.Paused) return false;
            _gameMode = mode;
            _versusMinutes = VersusMatch.ValidMinutes(minutes);
            return true;
        }

        private void Update()
        {
            if (!IsVersus || currentState != GameState.Playing) return;
            if (!_versusMatch.CanScore(Time.timeAsDouble)) { FinishVersusRound(); return; }
            RefreshVersusHUD();
        }

        private void RefreshVersusHUD()
        {
            if (leftScoreText != null && _shownLeft != _versusMatch.LeftScore)
            { _shownLeft = _versusMatch.LeftScore; leftScoreText.text = _shownLeft.ToString(); }
            if (rightScoreText != null && _shownRight != _versusMatch.RightScore)
            { _shownRight = _versusMatch.RightScore; rightScoreText.text = _shownRight.ToString(); }
            if (matchTimerText != null)
            {
                int seconds = Mathf.CeilToInt((float)RemainingMatchSeconds);
                if (seconds != _shownSeconds)
                { _shownSeconds = seconds; matchTimerText.text = $"{seconds / 60:00}:{seconds % 60:00}"; }
            }
        }

        private void RefreshModeHUD()
        {
            bool duel = IsVersus;
            if (versusHUD != null) versusHUD.SetActive(duel && (currentState == GameState.Playing || currentState == GameState.Paused));
            if (scoreText != null) scoreText.gameObject.SetActive(!duel);
            if (livesText != null) livesText.gameObject.SetActive(!duel);
            if (comboText != null && duel) comboText.gameObject.SetActive(false);
            if (heartIcons != null) foreach (var heart in heartIcons) if (heart != null) heart.gameObject.SetActive(!duel);
            if (uiManager != null && uiManager.heartIcons != null)
                foreach (var heart in uiManager.heartIcons) if (heart != null) heart.gameObject.SetActive(!duel);
            RefreshVersusHUD();
        }

        private void FinishVersusRound()
        {
            if (!IsVersus || currentState == GameState.GameOver) return;
            _versusMatch.Finish();
            currentState = GameState.GameOver;
            isGameOver = true;
            isGameActive = false;
            DrawingManager.Instance?.ResetForNewRound();
            if (bgmSource != null) bgmSource.Stop();
            if (EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.StopSpawning();
                EnemySpawner.Instance.ClearAllEnemies();
            }
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            RefreshModeHUD();
            // Use the existing lifecycle event without writing solo HighScore or playing a defeat sound.
            OnGameOver?.Invoke();
        }

        /// <summary>Routes a completed gesture by its first accepted screen point, independent of hand ID.</summary>
        public void HandleRecognizedGesture(int handIndex, PRecognitionResult result)
        {
            HandleGestureRecognized(handIndex, result);
        }

        private int PlayerForCompletedGesture(int handIndex)
        {
            var drawing = DrawingManager.Instance;
            if (drawing == null) return -1;
            var points = drawing.GetLastGesturePoints(handIndex);
            if (points.Length == 0) return -1;
            return VersusMatch.PlayerForScreenX(points[0].X, Screen.width);
        }

        public void OnPlayerSymbolRecognized(int player, GestureSymbol symbol)
        {
            if (!IsVersus || currentState != GameState.Playing || player < 0 || player > 1) return;
            if (!_versusMatch.CanScore(Time.timeAsDouble)) { FinishVersusRound(); return; }
            if (symbol == GestureSymbol.Unknown || symbol == GestureSymbol.None) return;
            int before = score;
            // Pop unregisters each enemy immediately; a later gesture cannot claim the same target.
            _scoringPlayer = player;
            try { OnSymbolRecognized(symbol); }
            finally { _scoringPlayer = -1; }
            int earned = score - before;
            if (earned > 0) _versusMatch.TryAward(player, earned, Time.timeAsDouble);
            RefreshVersusHUD();
        }

        // Optional menu gate. Defaults to true so scenes without MenuManager retain their lifecycle.
        [HideInInspector] public bool isGameActive = true;

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

        [Header("Phase 6 Audio Channels & Routing")]
        [Tooltip("Audio channel dedicated to background music (loop = true, volume ~ 0.35f).")]
        public AudioSource bgmSource;

        [Tooltip("Audio channel dedicated to sound effects via PlayOneShot.")]
        public AudioSource sfxSource;

        [Header("Phase 6 Audio Clips")]
        [Tooltip("Combat castle background soundtrack.")]
        public AudioClip combatCastleMusic;

        [Tooltip("Sound played when health/life is lost.")]
        public AudioClip lostHealthSound;

        [Tooltip("Sound played when a gesture stroke completes and matches a valid symbol.")]
        public AudioClip rightSymbolSound;

        [Tooltip("Sound played when a stroke fails to match any valid symbol or misses.")]
        public AudioClip wrongSymbolSound;

        [Tooltip("Sound played when exactly 1 balloon is popped.")]
        public AudioClip soundPop;

        [Tooltip("Sound played when 2 or more balloons are popped simultaneously (Combo Pop).")]
        public AudioClip soundComboPop;

        [Tooltip("Sound played upon Game Over.")]
        public AudioClip gameOverSound;

        [Header("Audio Feedback (Optional / Legacy Preserved)")]
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

            // Task 2: Auto-wire heart icons if not assigned or contains nulls
            bool needHearts = heartIcons == null || heartIcons.Length == 0;
            if (!needHearts)
            {
                needHearts = true;
                for (int i = 0; i < heartIcons.Length; i++)
                {
                    if (heartIcons[i] != null) { needHearts = false; break; }
                }
            }

            if (needHearts)
            {
                GameObject heartsObj = GameObject.Find("HeartContainer") ?? GameObject.Find("Hearts") ?? GameObject.Find("LivesContainer") ?? GameObject.Find("HeartIcons") ?? GameObject.Find("Lives");
                if (heartsObj != null)
                {
                    UnityEngine.UI.Image[] foundImages = heartsObj.GetComponentsInChildren<UnityEngine.UI.Image>();
                    if (foundImages != null && foundImages.Length > 0)
                    {
                        heartIcons = foundImages;
                        needHearts = false;
                    }
                }
            }

            // If still missing, check UIManager or auto-create under Canvas
            if (needHearts && uiManager != null && uiManager.heartIcons != null && uiManager.heartIcons.Length > 0)
            {
                heartIcons = uiManager.heartIcons;
                needHearts = false;
            }

            if (needHearts)
            {
                GameObject canvasObj = GameObject.Find("GameUI_Canvas") ?? GameObject.Find("Canvas");
                if (canvasObj != null)
                {
                    GameObject heartsObj = new GameObject("HeartContainer");
                    heartsObj.transform.SetParent(canvasObj.transform, false);
                    RectTransform containerRt = heartsObj.AddComponent<RectTransform>();
                    containerRt.anchorMin = new Vector2(1f, 1f);
                    containerRt.anchorMax = new Vector2(1f, 1f);
                    containerRt.pivot = new Vector2(1f, 1f);
                    containerRt.anchoredPosition = new Vector2(-40, -40);
                    containerRt.sizeDelta = new Vector2(180, 50);

                    heartIcons = new UnityEngine.UI.Image[3];
                    for (int i = 0; i < 3; i++)
                    {
                        GameObject heartImgObj = new GameObject($"Heart_{i}");
                        heartImgObj.transform.SetParent(heartsObj.transform, false);
                        RectTransform hRt = heartImgObj.AddComponent<RectTransform>();
                        hRt.anchorMin = new Vector2(0f, 0.5f);
                        hRt.anchorMax = new Vector2(0f, 0.5f);
                        hRt.pivot = new Vector2(0.5f, 0.5f);
                        hRt.anchoredPosition = new Vector2(25 + i * 55, 0);
                        hRt.sizeDelta = new Vector2(48, 48);

                        UnityEngine.UI.Image img = heartImgObj.AddComponent<UnityEngine.UI.Image>();
                        img.sprite = fullHeartSprite;
                        heartIcons[i] = img;
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

            // Task 4: Auto-wire Phase 6 Audio Channels (BGM and SFX)
            if (bgmSource == null || sfxSource == null)
            {
                AudioSource[] sources = GetComponents<AudioSource>();
                if (sources.Length >= 2)
                {
                    if (bgmSource == null) bgmSource = sources[0];
                    if (sfxSource == null) sfxSource = sources[1];
                }
                else if (sources.Length == 1)
                {
                    if (sfxSource == null) sfxSource = sources[0];
                    if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
                }
                else
                {
                    if (bgmSource == null) bgmSource = gameObject.AddComponent<AudioSource>();
                    if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();
                }
            }

            if (bgmSource != null)
            {
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                if (bgmSource.volume > 0.4f || bgmSource.volume <= 0.01f)
                {
                    bgmSource.volume = 0.35f;
                }
            }

            if (sfxSource != null)
            {
                sfxSource.loop = false;
                sfxSource.playOnAwake = false;
            }

            // Sync legacy references for backward compatibility
            if (_audioSource == null) _audioSource = sfxSource;
            if (gameOverSound == null && _gameOverSound != null) gameOverSound = _gameOverSound;
            if (rightSymbolSound == null && _matchSuccessSound != null) rightSymbolSound = _matchSuccessSound;
            if (wrongSymbolSound == null && _missSound != null) wrongSymbolSound = _missSound;
        }

        public void StartNewGame()
        {
            SoloBossVictory = false;
            RoundReset?.Invoke();
            if (EnemySpawner.Instance != null) EnemySpawner.Instance.StopSpawning();
            DrawingManager.Instance?.ResetForNewRound();
            _versusMatch.Reset(_versusMinutes);
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
            OnScoreChanged?.Invoke(score);
            OnLivesChanged?.Invoke(lives);
            var bridge = MagicTouch.Tracking.HandTrackingBridge.Instance;
            if (bridge != null && bridge.IsTrackingReady) StartGame();
        }

        /// <summary>
        /// Starts the gameplay loop once the camera and hand tracking handshake succeeds (Task 1).
        /// </summary>
        public void StartGame()
        {
            // A tracking handshake must never dismiss a menu or resume a paused round.
            if (!isGameActive || currentState == GameState.MainMenu || currentState == GameState.Paused) return;
            if (currentState == GameState.Playing) return;

            Debug.Log("<color=green>[GameManager] Camera handshake received! Starting game loop.</color>");
            currentState = GameState.Playing;
            isGameOver = false;
            if (IsVersus) _versusMatch.Start(Time.timeAsDouble);
            RefreshModeHUD();

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            if (EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.ClearAllEnemies();
                EnemySpawner.Instance.StartSpawning();
            }

            // Phase 6 Audio: Gameplay Start / Camera Ready - Start playing combatCastleMusic on bgmSource (looping seamlessly)
            RoundStarted?.Invoke();
            PlayBGM();
        }

        /// <summary>Applies UI audio settings without changing tracking, spawning or physics.</summary>
        public void ApplyAudioSettings(float musicVolume, float effectsVolume)
        {
            if (bgmSource != null) bgmSource.volume = Mathf.Clamp01(musicVolume);
            if (sfxSource != null) sfxSource.volume = Mathf.Clamp01(effectsVolume);
            MusicDirector.Instance?.SetMaster(musicVolume);
        }

        /// <summary>Starts combat music using the saved UI volume.</summary>
        public void PlayBGM()
        {
            if (MusicDirector.Instance != null && MusicDirector.Instance.HasClips)
            {
                if (bgmSource != null && bgmSource.isPlaying) bgmSource.Stop();
                MusicDirector.Instance.PlayCombat();
                return;
            }
            if (bgmSource != null && combatCastleMusic != null)
            {
                bgmSource.clip = combatCastleMusic;
                bgmSource.loop = true;
                bgmSource.volume = PlayerPrefs.GetFloat("BGM_Volume", 0.35f);
                if (!bgmSource.isPlaying)
                {
                    bgmSource.Play();
                }
            }
        }

        /// <summary>
        /// Callback invoked when DrawingManager finishes recognizing a gesture.
        /// </summary>
        private void HandleGestureRecognized(int handIndex, PRecognitionResult result)
        {
            _lastSymbolPopCount = 0;
            if (currentState != GameState.Playing)
            {
                Debug.Log("[GameManager] Gesture ignored: Game is waiting for camera setup or over.");
                return;
            }

            if (result.IsMatch && result.Symbol != GestureSymbol.Unknown && result.Symbol != GestureSymbol.None)
            {
                if (IsVersus) OnPlayerSymbolRecognized(PlayerForCompletedGesture(handIndex), result.Symbol);
                else OnSymbolRecognized(result.Symbol);
                DrawingManager.Instance?.ReportGestureOutcome(handIndex, _lastSymbolPopCount);
            }
            else
            {
                // Ambiguous or unrecognized stroke resets combo
                combo = 0;
                UpdateComboUI();

                // Phase 6 Audio: If the stroke fails to match any valid symbol -> play wrongSymbolSound on sfxSource
                if (wrongSymbolSound != null && sfxSource != null)
                {
                    sfxSource.PlayOneShot(wrongSymbolSound);
                }
                else if (_missSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_missSound, 0.6f);
                }
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
            _lastSymbolPopCount = 0;
            if (currentState != GameState.Playing) return;
            if (IsVersus && (_scoringPlayer < 0 || !_versusMatch.CanScore(Time.timeAsDouble))) return;

            // 1. Gather all matching enemies currently alive on screen
            List<EnemyBalloon> toPop = new List<EnemyBalloon>();
            List<EnemyBalloon> activeList = EnemySpawner.ActiveEnemies;

            for (int i = 0; i < activeList.Count; i++)
            {
                EnemyBalloon candidate = activeList[i];
                if (candidate != null && candidate.HasSymbol(drawnSymbol))
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

                // Phase 6 Audio: When a stroke completes and matches a valid symbol -> Play rightSymbolSound on sfxSource
                if (rightSymbolSound != null && sfxSource != null)
                {
                    sfxSource.PlayOneShot(rightSymbolSound);
                }
                else if (_matchSuccessSound != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(_matchSuccessSound);
                }

                // Phase 6 Audio: Count the number of destroyed balloons from this symbol
                // If destroyed balloons == 1: Also play soundPop on sfxSource.
                // If destroyed balloons >= 2: Play soundComboPop on sfxSource instead of soundPop.
                if (count == 1)
                {
                    if (soundPop != null && sfxSource != null)
                    {
                        sfxSource.PlayOneShot(soundPop);
                    }
                }
                else if (count >= 2)
                {
                    if (soundComboPop != null && sfxSource != null)
                    {
                        sfxSource.PlayOneShot(soundComboPop);
                    }
                    else if (soundPop != null && sfxSource != null)
                    {
                        sfxSource.PlayOneShot(soundPop);
                    }
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

                        toPop[i].PopSymbol(drawnSymbol, scoreStr); // one balloon per creep per gesture
                        _lastSymbolPopCount++;
                    }
                }

                UpdateUI();
                OnScoreChanged?.Invoke(score);
            }
            else
            {
                // Gesture recognized, but no matching balloon currently on screen (miss)
                combo = 0;
                UpdateComboUI();

                // Phase 6 Audio: If the stroke misses -> Play wrongSymbolSound on sfxSource
                if (wrongSymbolSound != null && sfxSource != null)
                {
                    sfxSource.PlayOneShot(wrongSymbolSound);
                }
                else if (_missSound != null && _audioSource != null)
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
            if (IsVersus) return; // Missed targets expire normally; timed matches have no HP.
            if (currentState != GameState.Playing) return;

            lives--;
            combo = 0;
            MagicTouch.Juice.ScreenHitFeedback.Play();

            Debug.Log($"<color=orange>[GameManager] Enemy landed! Remaining lives: {lives}</color>");

            // Phase 6 Audio: Damage Taken - play lostHealthSound on sfxSource
            if (lostHealthSound != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(lostHealthSound);
            }

            // Direct loop through heartIcons array as requested
            int currentLives = lives;
            if (heartIcons != null)
            {
                for (int i = 0; i < heartIcons.Length; i++)
                {
                    if (heartIcons[i] != null)
                    {
                        heartIcons[i].sprite = (i < currentLives) ? fullHeartSprite : emptyHeartSprite;
                    }
                }
            }

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
            if (IsVersus) { FinishVersusRound(); return; }
            if (currentState == GameState.GameOver) return;
            currentState = GameState.GameOver;
            isGameOver = true;
            DrawingManager.Instance?.ResetForNewRound();

            // Phase 6 Audio: Immediately stop background music
            if (bgmSource != null)
            {
                bgmSource.Stop();
            }
            MusicDirector.Instance?.StopAll();

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

            // Phase 6 Audio: Play gameOverSound on sfxSource
            if (!SoloBossVictory && gameOverSound != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(gameOverSound);
            }
            else if (!SoloBossVictory && _gameOverSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_gameOverSound);
            }

            Debug.Log($"<color=red>[GameManager] GAME OVER! Final Score: {score}, High Score: {highScore}</color>");
            OnGameOver?.Invoke();
        }

        /// <summary>
        /// Resets the round in place, preserving camera, Python, tracking and inference workers.
        /// </summary>
        public void RestartGame()
        {
            Debug.Log("[GameManager] Restarting round without reloading tracking or model.");
            isGameActive = true; // Preserve legacy UIManager and external restart entry points.
            Time.timeScale = 1f;
            if (sfxSource != null) sfxSource.Stop();
            StartNewGame();
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
            RefreshModeHUD();
        }

        /// <summary>
        /// Task 2: Pixel Art Heart UI.
        /// Visually swaps heart icon sprites from fullHeartSprite to emptyHeartSprite based on remaining lives.
        /// </summary>
        public void UpdateHeartUI()
        {
            if (heartIcons == null || heartIcons.Length == 0) return;

            int currentLives = lives;
            for (int i = 0; i < heartIcons.Length; i++)
            {
                if (heartIcons[i] == null) continue;
                heartIcons[i].sprite = (i < currentLives) ? fullHeartSprite : emptyHeartSprite;
                heartIcons[i].color = Color.white;
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
