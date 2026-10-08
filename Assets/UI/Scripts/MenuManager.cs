using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MagicTouch.Gameplay;

namespace MagicTouch.UI
{
    /// <summary>
    /// Runtime Single-Scene UI and Game State Manager (Phase 7):
    /// Controls navigation across MainMenu, Settings, Loading, GameplayHUD, Pause, and GameOver panels.
    /// Manages audio channels (BGM/SFX), PlayerPrefs persistence, and difficulty settings.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class MenuManager : MonoBehaviour
    {
        public static MenuManager Instance { get; private set; }

        [Header("UI Panel References")]
        [Tooltip("Main title and start menu panel.")]
        public GameObject mainMenuPanel;

        [Tooltip("Audio and gameplay difficulty settings panel.")]
        public GameObject settingsPanel;

        [Tooltip("Loading screen displayed before gameplay begins.")]
        public GameObject loadingPanel;

        [Tooltip("Gameplay HUD overlay (contains Pause button, etc.).")]
        public GameObject gameplayHUDPanel;

        [Tooltip("Pause menu modal popup.")]
        public GameObject pausePanel;

        [Tooltip("Game Over summary and restart modal popup.")]
        public GameObject gameOverPanel;
        public GameObject creditsPanel;
        public GameObject versusSetupPanel;
        public GameObject versusResultPanel;
        public GameObject versusHUDPanel;
        public TextMeshProUGUI versusDurationText;
        public Button[] durationButtons;
        public TextMeshProUGUI versusWinnerText;
        public TextMeshProUGUI versusResultScores;
        public GameObject versusPauseSummary;
        public TextMeshProUGUI versusPauseScores;
        public GameObject[] soloPauseElements;
        private int _versusMinutes = 3;

        public void OpenVersusSetup()
        {
            PlayButtonClick();
            _versusMinutes = VersusMatch.ValidMinutes(PlayerPrefs.GetInt("VersusMinutes", 3));
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (versusSetupPanel != null) versusSetupPanel.SetActive(true);
            RefreshDurationUI();
        }
        public void CloseVersusSetup()
        {
            PlayButtonBack();
            if (versusSetupPanel != null) versusSetupPanel.SetActive(false);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        }
        public void SelectThreeMinutes() => SelectDuration(3);
        public void SelectFiveMinutes() => SelectDuration(5);
        public void SelectSevenMinutes() => SelectDuration(7);
        private void SelectDuration(int minutes)
        {
            PlayButtonClick();
            _versusMinutes = VersusMatch.ValidMinutes(minutes);
            PlayerPrefs.SetInt("VersusMinutes", _versusMinutes);
            PlayerPrefs.Save();
            RefreshDurationUI();
        }
        private void RefreshDurationUI()
        {
            if (versusDurationText != null) versusDurationText.text = $"MATCH TIME: {_versusMinutes} MIN";
            if (durationButtons != null)
                for (int i = 0; i < durationButtons.Length; i++)
                    if (durationButtons[i] != null) durationButtons[i].image.color = _versusMinutes == 3 + i * 2
                        ? new Color32(148, 255, 130, 255) : Color.white;
        }
        public void StartVersusGame()
        {
            if (GameManager.Instance == null || !GameManager.Instance.ConfigureMode(GameMode.Versus, _versusMinutes)) return;
            BeginRound();
        }
        private void HideVersusViews()
        {
            if (versusSetupPanel != null) versusSetupPanel.SetActive(false);
            if (versusResultPanel != null) versusResultPanel.SetActive(false);
            if (versusHUDPanel != null) versusHUDPanel.SetActive(false);
        }

        [Header("Arcade Score Board")]
        public TextMeshProUGUI pauseScoreText;
        public TextMeshProUGUI pauseBestText;
        public TextMeshProUGUI finalScoreText;
        public TextMeshProUGUI highScoreText;

        [Header("Audio Source References")]
        [Tooltip("Dedicated channel for looping background music.")]
        public AudioSource bgmSource;

        [Tooltip("Dedicated channel for one-shot sound effects.")]
        public AudioSource sfxSource;

        [Header("Audio Clips")]
        [Tooltip("Music played on the Main Menu.")]
        public AudioClip menuMusic;

        [Tooltip("Combat background music during gameplay.")]
        public AudioClip combatMusic;

        [Tooltip("Ambient music during Pause.")]
        public AudioClip pauseMusic;

        [Tooltip("Stinger or music upon Game Over.")]
        public AudioClip gameOverMusic;

        [Header("SFX Sound Effects")]
        [Tooltip("Sound played upon clicking UI buttons.")]
        public AudioClip buttonClickSound;

        [Tooltip("Sound played upon confirming action (e.g. Play, Restart).")]
        public AudioClip buttonConfirmSound;

        [Tooltip("Sound played upon back/cancel navigation.")]
        public AudioClip buttonBackSound;

        [Tooltip("Sound played upon balloon pop.")]
        public AudioClip popSound;

        [Tooltip("Sound played upon combo pop.")]
        public AudioClip comboPopSound;

        [Tooltip("Sound played upon losing a life.")]
        public AudioClip lostHealthSound;

        [Tooltip("Sound played upon game over.")]
        public AudioClip gameOverSound;

        [Header("Settings UI Controls (Optional Binding)")]
        public Slider bgmSlider;
        public Slider sfxSlider;
        public TextMeshProUGUI difficultyDisplay;
        public TextMeshProUGUI difficultyHint;
        public Button[] difficultyButtons;

        [Header("Preserved legacy loading views")]
        public GameObject[] legacyLoadingPanels;

        // Retire only explicitly migrated loading views; never disable the active camera gate.
        private void HideLegacyLoadingPanels()
        {
            if (legacyLoadingPanels == null) return;
            foreach (var panel in legacyLoadingPanels)
                if (panel != null && panel != loadingPanel) panel.SetActive(false);
        }

        // PlayerPrefs Storage Keys
        private const string KEY_BGM_VOL = "BGM_Volume";
        private const string KEY_SFX_VOL = "SFX_Volume";
        private const string KEY_DIFFICULTY = "Difficulty";

        // Navigation state tracking
        private GameObject _previousPanel;
        private int _difficultyLevel = 1; // 0 = Easy, 1 = Normal, 2 = Hard
        private Coroutine _loadingRoutine;
        private bool _paused;
        private float _resumeTimeScale = 1f;
        private GameManager _subscribedGameManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            HideLegacyLoadingPanels();

            // Awake runs before GameManager.Start; prevent automatic camera-ready gameplay behind the menu.
            var game = Object.FindAnyObjectByType<GameManager>();
            if (game != null) game.isGameActive = false;
            LoadSettings();
        }

        private void Start()
        {
            Time.timeScale = 1f;

            // Force-hide EVERY panel EXCEPT mainMenuPanel (Phase 8 Task 2)
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (gameplayHUDPanel != null) gameplayHUDPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);

            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.isGameActive = false;
            }

            AutoWireAudioSources();
            HookGameManagerEvents();

            // Initial State: Show Main Menu, hide all other panels
            ReturnToMainMenu();
        }

        private void OnDestroy()
        {
            UnhookGameManagerEvents();
            if (_paused) Time.timeScale = _resumeTimeScale;
            if (Instance == this) Instance = null;
        }

        #region UI Flow & Game State Transitions

        /// <summary>
        /// Starts a round through the existing camera handshake and then reveals the gameplay HUD.
        /// </summary>
        public void StartGame()
        {
            if (GameManager.Instance == null || !GameManager.Instance.ConfigureMode(GameMode.Solo)) return;
            BeginRound();
        }

        private void BeginRound()
        {
            if (_loadingRoutine != null || (gameplayHUDPanel != null && gameplayHUDPanel.activeSelf && !_paused)) return;
            PlayButtonConfirm();
            if (_loadingRoutine != null) StopCoroutine(_loadingRoutine);
            _loadingRoutine = StartCoroutine(StartGameRoutine());
        }

        private IEnumerator StartGameRoutine()
        {
            HideVersusViews();
            Time.timeScale = 1f;
            _paused = false;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isGameActive = true;
                GameManager.Instance.StartNewGame();
            }

            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (gameplayHUDPanel != null) gameplayHUDPanel.SetActive(false);

            if (loadingPanel != null) loadingPanel.SetActive(true);

            // The existing GameManager/bridge handshake is the single authority for readiness.
            // Yield once so a warm start cannot leave a completed coroutine handle behind.
            yield return null;
            while (GameManager.Instance != null && GameManager.Instance.currentState != GameState.Playing)
                yield return null;

            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (gameplayHUDPanel != null) gameplayHUDPanel.SetActive(true);

            // Play Combat BGM
            PlayBGM(combatMusic != null ? combatMusic : menuMusic, loop: true);

            // Phase 8 Task 4: Confirm gameplay only after the existing readiness handshake
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isGameActive = true;
                GameManager.Instance.StartGame();
            }

            if (GameManager.Instance == null && EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.ClearAllEnemies();
                EnemySpawner.Instance.StartSpawning();
            }

            _loadingRoutine = null;
        }

        /// <summary>
        /// Opens the settings panel while recording the previous panel to restore on Back.
        /// </summary>
        public void OpenSettings()
        {
            if (settingsPanel != null && settingsPanel.activeSelf) return;
            if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing) PauseGame();
            PlayButtonClick();
            if (pausePanel != null && pausePanel.activeSelf)
            {
                _previousPanel = pausePanel;
            }
            else if (mainMenuPanel != null && mainMenuPanel.activeSelf)
            {
                _previousPanel = mainMenuPanel;
            }
            else
            {
                _previousPanel = gameplayHUDPanel;
            }

            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(true);

            UpdateSettingsUI();
        }

        /// <summary>
        /// Closes the settings panel and returns to the previously recorded panel.
        /// </summary>
        public void CloseSettings()
        {
            PlayButtonBack();
            if (settingsPanel != null) settingsPanel.SetActive(false);

            if (_previousPanel != null)
            {
                _previousPanel.SetActive(true);
            }
            else if (mainMenuPanel != null)
            {
                mainMenuPanel.SetActive(true);
            }
        }

        public void OpenCredits()
        {
            PlayButtonClick();
            _previousPanel = mainMenuPanel;
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(true);
        }

        public void CloseCredits()
        {
            PlayButtonBack();
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (_previousPanel != null) _previousPanel.SetActive(true);
            else if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        }

        private void RefreshScoreBoard()
        {
            var game = GameManager.Instance;
            bool duel = game != null && game.IsVersus;
            if (soloPauseElements != null) foreach (var element in soloPauseElements) if (element != null) element.SetActive(!duel);
            if (versusPauseSummary != null) versusPauseSummary.SetActive(duel);
            if (duel && versusPauseScores != null)
            {
                int seconds = Mathf.CeilToInt((float)game.RemainingMatchSeconds);
                versusPauseScores.text = $"<color=#82EFFF>P1  {game.VersusRound.LeftScore}</color>\n\n<color=#FFABD8>P2  {game.VersusRound.RightScore}</color>\n\n{seconds / 60:00}:{seconds % 60:00} LEFT";
            }
            int score = GameManager.Instance != null ? GameManager.Instance.score : 0;
            int best = Mathf.Max(score, PlayerPrefs.GetInt("HighScore", 0));
            if (pauseScoreText != null) pauseScoreText.SetText("{0}", score);
            if (pauseBestText != null) pauseBestText.SetText("{0}", best);
            if (finalScoreText != null) finalScoreText.SetText("{0}", score);
            if (highScoreText != null) highScoreText.SetText("BEST {0}", best);
        }

        /// <summary>
        /// Pauses the game: Sets Time.timeScale = 0, shows pausePanel, and switches BGM to pauseMusic.
        /// </summary>
        public void PauseGame()
        {
            if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.Playing) return;
            PlayButtonClick();
            RefreshScoreBoard();
            _paused = true;
            _resumeTimeScale = Time.timeScale;
            GameManager.Instance.currentState = GameState.Paused;
            MagicTouch.Drawing.DrawingManager.Instance?.ResetForNewRound();
            Time.timeScale = 0f;
            if (bgmSource != null) bgmSource.Pause();

            if (pausePanel != null) pausePanel.SetActive(true);

            if (pauseMusic != null)
            {
                PlayBGM(pauseMusic, loop: true);
            }
        }

        /// <summary>
        /// Resumes the game: Sets Time.timeScale = 1, hides pausePanel, and restores combatMusic.
        /// </summary>
        public void ResumeGame()
        {
            if (!_paused || GameManager.Instance == null || GameManager.Instance.currentState != GameState.Paused) return;
            PlayButtonClick();
            _paused = false;
            GameManager.Instance.currentState = GameState.Playing;
            Time.timeScale = _resumeTimeScale;
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (bgmSource != null) bgmSource.UnPause();

            if (pausePanel != null) pausePanel.SetActive(false);

            if (combatMusic != null)
            {
                PlayBGM(combatMusic, loop: true);
            }
        }

        /// <summary>
        /// Restarts the active round: resets score and lives, clears enemies, hides GameOver panel, and restarts combatMusic.
        /// </summary>
        public void RestartGame()
        {
            HideVersusViews();
            if (_loadingRoutine != null) return;
            _paused = false;
            PlayButtonConfirm();
            var bridge = MagicTouch.Tracking.HandTrackingBridge.Instance;
            if (bridge == null || !bridge.IsTrackingReady) { BeginRound(); return; }
            CancelLoading();
            _paused = false;
            Time.timeScale = 1f;

            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
            if (gameplayHUDPanel != null) gameplayHUDPanel.SetActive(true);

            PlayBGM(combatMusic != null ? combatMusic : menuMusic, loop: true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.isGameActive = true;
                GameManager.Instance.RestartGame();
                GameManager.Instance.StartGame();
            }

            if (GameManager.Instance == null && EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.ClearAllEnemies();
                EnemySpawner.Instance.StartSpawning();
            }
        }

        /// <summary>
        /// Returns to Main Menu: clears round data, resets timeScale = 1, shows mainMenuPanel, and plays menuMusic.
        /// </summary>
        public void ReturnToMainMenu()
        {
            HideVersusViews();
            PlayButtonBack();
            CancelLoading();
            _paused = false;
            Time.timeScale = 1f;
            if (MagicTouch.Drawing.DrawingManager.Instance != null)
                MagicTouch.Drawing.DrawingManager.Instance.ResetForNewRound();

            // Phase 8 Task 4: Set isGameActive = false when returning to main menu
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.bgmSource != null && GameManager.Instance.bgmSource != bgmSource)
                    GameManager.Instance.bgmSource.Stop();
                GameManager.Instance.isGameActive = false;
                GameManager.Instance.currentState = GameState.MainMenu;
            }

            if (loadingPanel != null) loadingPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (creditsPanel != null) creditsPanel.SetActive(false);
            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            if (gameplayHUDPanel != null) gameplayHUDPanel.SetActive(false);

            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);

            if (EnemySpawner.Instance != null)
            {
                EnemySpawner.Instance.StopSpawning();
                EnemySpawner.Instance.ClearAllEnemies();
            }

            PlayBGM(menuMusic != null ? menuMusic : combatMusic, loop: true);
        }

        private void CancelLoading()
        {
            HideLegacyLoadingPanels();
            if (_loadingRoutine != null) StopCoroutine(_loadingRoutine);
            _loadingRoutine = null;
            if (loadingPanel != null) loadingPanel.SetActive(false);
        }

        /// <summary>
        /// Quits the application.
        /// </summary>
        public void QuitGame()
        {
            PlayButtonBack();
            Debug.Log("[MenuManager] Application Quit requested.");
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        #endregion

        #region Audio & Settings Logic

        public void SetBGMVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(KEY_BGM_VOL, volume);
            PlayerPrefs.Save();

            if (bgmSource != null)
            {
                bgmSource.volume = volume;
            }
            SynchronizeAudioVolumes();
        }

        public void SetSFXVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(KEY_SFX_VOL, volume);
            PlayerPrefs.Save();

            if (sfxSource != null)
            {
                sfxSource.volume = volume;
            }
            SynchronizeAudioVolumes();
        }

        public void SetDifficulty(int level)
        {
            PlayButtonClick();
            _difficultyLevel = Mathf.Clamp(level, 0, 2);
            EnemySpawner.Instance?.ApplyDifficulty(_difficultyLevel);
            PlayerPrefs.SetInt(KEY_DIFFICULTY, _difficultyLevel);
            PlayerPrefs.Save();

            UpdateSettingsUI();
            Debug.Log($"[MenuManager] Difficulty set to: {GetDifficultyName()} ({_difficultyLevel})");
        }

        public void SetDifficultyEasy() => SetDifficulty(0);
        public void SetDifficultyMedium() => SetDifficulty(1); // Retained for existing bindings.
        public void SetDifficultyNormal() => SetDifficulty(1);
        public void SetDifficultyHard() => SetDifficulty(2);

        /// <summary>
        /// Returns the chosen difficulty level for EnemySpawner (0 = Easy, 1 = Normal, 2 = Hard).
        /// </summary>
        public int GetDifficultyLevel() => _difficultyLevel;

        public string GetDifficultyName()
        {
            switch (_difficultyLevel)
            {
                case 0: return "Easy";
                case 2: return "Hard";
                default: return "Normal";
            }
        }

        private void LoadSettings()
        {
            float bgmVol = PlayerPrefs.GetFloat(KEY_BGM_VOL, 0.35f);
            float sfxVol = PlayerPrefs.GetFloat(KEY_SFX_VOL, 1.0f);
            _difficultyLevel = Mathf.Clamp(PlayerPrefs.GetInt(KEY_DIFFICULTY, 1), 0, 2);

            if (bgmSource != null) bgmSource.volume = bgmVol;
            if (sfxSource != null) sfxSource.volume = sfxVol;
            SynchronizeAudioVolumes();

            UpdateSettingsUI();
        }

        private void UpdateSettingsUI()
        {
            if (bgmSlider != null) bgmSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(KEY_BGM_VOL, 0.35f));
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(KEY_SFX_VOL, 1.0f));
            if (difficultyDisplay != null) difficultyDisplay.text = $"DIFFICULTY: {GetDifficultyName().ToUpperInvariant()}";
            if (difficultyHint != null)
                difficultyHint.text = _difficultyLevel == 0 ? "SLOWER FALL / FEWER ENEMIES" :
                    (_difficultyLevel == 2 ? "FASTER FALL / MORE ENEMIES" : "CLASSIC SPEED / CLASSIC SPAWNS");
            if (difficultyButtons != null)
                for (int i = 0; i < difficultyButtons.Length; i++)
                    if (difficultyButtons[i] != null)
                        difficultyButtons[i].image.color = i == _difficultyLevel
                            ? new Color32(148, 255, 130, 255) : Color.white;
        }

        private void PlayBGM(AudioClip clip, bool loop = true)
        {
            if (bgmSource == null || clip == null) return;
            if (GameManager.Instance != null && GameManager.Instance.bgmSource != null && GameManager.Instance.bgmSource != bgmSource)
                GameManager.Instance.bgmSource.Stop();

            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            bgmSource.clip = clip;
            bgmSource.loop = loop;
            bgmSource.volume = PlayerPrefs.GetFloat(KEY_BGM_VOL, 0.35f);
            bgmSource.Play();
        }

        private void AutoWireAudioSources()
        {
            if (bgmSource == null || sfxSource == null)
            {
                if (GameManager.Instance != null)
                {
                    if (bgmSource == null) bgmSource = GameManager.Instance.bgmSource;
                    if (sfxSource == null) sfxSource = GameManager.Instance.sfxSource;
                }

                AudioSource[] sources = GetComponents<AudioSource>();
                if (bgmSource == null && sources.Length > 0) bgmSource = sources[0];
                if (sfxSource == null && sources.Length > 1) sfxSource = sources[1];

                if (bgmSource == null)
                {
                    bgmSource = gameObject.AddComponent<AudioSource>();
                    bgmSource.playOnAwake = false;
                    bgmSource.loop = true;
                    bgmSource.volume = PlayerPrefs.GetFloat(KEY_BGM_VOL, 0.35f);
                }

                if (sfxSource == null)
                {
                    sfxSource = gameObject.AddComponent<AudioSource>();
                    sfxSource.playOnAwake = false;
                    sfxSource.loop = false;
                    sfxSource.volume = PlayerPrefs.GetFloat(KEY_SFX_VOL, 1.0f);
                }
            }

            // Fallback sync with GameManager
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.sfxSource == null) GameManager.Instance.sfxSource = sfxSource;
                if (GameManager.Instance.bgmSource == null) GameManager.Instance.bgmSource = bgmSource;
                if (buttonClickSound == null) buttonClickSound = GameManager.Instance.soundPop;
                if (popSound == null) popSound = GameManager.Instance.soundPop;
                if (comboPopSound == null) comboPopSound = GameManager.Instance.soundComboPop;
                if (buttonConfirmSound == null) buttonConfirmSound = GameManager.Instance.rightSymbolSound;
                if (buttonBackSound == null) buttonBackSound = GameManager.Instance.wrongSymbolSound;
                if (lostHealthSound == null) lostHealthSound = GameManager.Instance.lostHealthSound;
                if (gameOverSound == null) gameOverSound = GameManager.Instance.gameOverSound;
            }
        }

        public void PlayCombatMusic() => PlayBGM(combatMusic != null ? combatMusic : menuMusic, true);

        /// <summary>
        /// Plays a sound effect via sfxSource with PlayerPrefs volume scaling.
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip == null) return;
            AudioSource source = sfxSource != null ? sfxSource 
                : (GameManager.Instance != null ? GameManager.Instance.sfxSource : null);
            if (source != null)
            {
                float vol = Mathf.Clamp01(volumeScale); // Source volume already applies the saved setting.
                source.PlayOneShot(clip, vol);
            }
        }

        public void PlayButtonClick() => PlaySFX(buttonClickSound != null ? buttonClickSound : popSound);
        public void PlayButtonConfirm() => PlaySFX(buttonConfirmSound != null ? buttonConfirmSound : (buttonClickSound != null ? buttonClickSound : popSound));
        public void PlayButtonBack() => PlaySFX(buttonBackSound != null ? buttonBackSound : (buttonClickSound != null ? buttonClickSound : popSound));

        private void SynchronizeAudioVolumes()
        {
            GameManager.Instance?.ApplyAudioSettings(PlayerPrefs.GetFloat(KEY_BGM_VOL, .35f), PlayerPrefs.GetFloat(KEY_SFX_VOL, 1f));
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (versusSetupPanel != null && versusSetupPanel.activeSelf) CloseVersusSetup();
            else if (settingsPanel != null && settingsPanel.activeSelf) CloseSettings();
            else if (creditsPanel != null && creditsPanel.activeSelf) CloseCredits();
            else if (loadingPanel != null && loadingPanel.activeSelf) ReturnToMainMenu();
            else if (_paused) ResumeGame();
            else if (GameManager.Instance != null && GameManager.Instance.currentState == GameState.Playing) PauseGame();
        }

        private void HookGameManagerEvents()
        {
            if (GameManager.Instance != null)
            {
                _subscribedGameManager = GameManager.Instance;
                _subscribedGameManager.OnGameOver += HandleGameOverEvent;
            }
        }

        private void UnhookGameManagerEvents()
        {
            if (_subscribedGameManager != null)
            {
                _subscribedGameManager.OnGameOver -= HandleGameOverEvent;
            }
        }

        private void HandleGameOverEvent()
        {
            CancelLoading();
            _paused = false;
            Time.timeScale = 1f;
            RefreshScoreBoard();
            if (pausePanel != null) pausePanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false);
            // Phase 8 Task 4: Set isGameActive = false on Game Over
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isGameActive = false;
            }

            if (gameplayHUDPanel != null) gameplayHUDPanel.SetActive(false);
            bool duel = GameManager.Instance != null && GameManager.Instance.IsVersus;
            if (gameOverPanel != null) gameOverPanel.SetActive(!duel);
            if (versusHUDPanel != null) versusHUDPanel.SetActive(false);
            if (versusResultPanel != null) versusResultPanel.SetActive(duel);
            if (duel)
            {
                var match = GameManager.Instance.VersusRound;
                if (versusWinnerText != null) versusWinnerText.text = match.Winner < 0 ? "IT'S A DRAW!" : $"PLAYER {match.Winner + 1} WINS!";
                if (versusResultScores != null) versusResultScores.text = $"<color=#82EFFF>PLAYER 1\n{match.LeftScore}</color>\n\n<color=#FFABD8>PLAYER 2\n{match.RightScore}</color>";
            }

            if (bgmSource != null) bgmSource.Stop();
            AudioClip goClip = gameOverSound != null ? gameOverSound : gameOverMusic;
            if (!duel && goClip != null && (GameManager.Instance == null || GameManager.Instance.gameOverSound == null))
            {
                PlaySFX(goClip);
            }
        }

        #endregion
    }
}
