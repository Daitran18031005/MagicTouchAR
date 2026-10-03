using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace MagicTouch.UI
{
    /// <summary>
    /// Central UI Manager for Magic Touch:
    /// 1. Health/Lives: Manages Heart Image icons (full vs empty heart sprites).
    /// 2. Game Over: Displays Game Over panel with scores and handles restart.
    /// 3. Floating Score/Combo Text: Instantiates rising, fading arcade text when enemies are defeated.
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Health / Lives System (Hearts)")]
        [Tooltip("Array of UI Image components representing player hearts (0 to max lives - 1).")]
        public Image[] heartIcons;

        [Tooltip("Sprite representing an active, full heart.")]
        public Sprite fullHeartSprite;

        [Tooltip("Sprite representing a depleted, empty heart.")]
        public Sprite emptyHeartSprite;

        [Header("Game Over UI")]
        [Tooltip("Game Over panel / modal.")]
        public GameObject gameOverPanel;

        [Tooltip("Final Score text in Game Over panel.")]
        public TextMeshProUGUI finalScoreText;

        [Tooltip("High Score text in Game Over panel.")]
        public TextMeshProUGUI highScoreText;

        [Tooltip("Restart Button in Game Over panel.")]
        public Button restartButton;

        [Header("Arcade Floating Score / Combo Text")]
        [Tooltip("Optional custom prefab for floating combo popup (if null, dynamic pixel TMP is generated).")]
        public GameObject floatingTextPrefab;

        [Tooltip("Font size for floating combat text.")]
        [SerializeField] private float _floatingTextFontSize = 6.5f;

        [Tooltip("Duration in seconds the floating text rises and fades out.")]
        [SerializeField] private float _floatingTextDuration = 1.0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            AutoWireReferences();
        }

        private void Start()
        {
            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestartButtonClicked);
                restartButton.onClick.AddListener(OnRestartButtonClicked);
            }
        }

        /// <summary>
        /// Auto-wires UI components from the scene hierarchy if not assigned in Inspector.
        /// </summary>
        public void AutoWireReferences()
        {
            // Auto-locate Hearts
            if (heartIcons == null || heartIcons.Length == 0)
            {
                GameObject heartsObj = GameObject.Find("HeartContainer") ?? GameObject.Find("Hearts") ?? GameObject.Find("LivesContainer");
                if (heartsObj != null)
                {
                    heartIcons = heartsObj.GetComponentsInChildren<Image>();
                }
            }

            // Auto-locate GameOverPanel
            if (gameOverPanel == null)
            {
                GameObject canvasObj = GameObject.Find("GameUI_Canvas");
                if (canvasObj != null)
                {
                    Transform goPanel = canvasObj.transform.Find("GameOverPanel");
                    if (goPanel != null) gameOverPanel = goPanel.gameObject;
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

                if (restartButton == null)
                {
                    Transform rb = gameOverPanel.transform.Find("RestartButton") ?? gameOverPanel.transform.Find("Btn_Restart");
                    if (rb != null) restartButton = rb.GetComponent<Button>();
                }
            }
        }

        #region Health / Heart Display

        /// <summary>
        /// Updates the graphical heart icons: indices below currentLives show fullHeartSprite,
        /// while indices >= currentLives swap to emptyHeartSprite.
        /// </summary>
        public void UpdateHearts(int currentLives, int maxLives = 3)
        {
            if (heartIcons == null || heartIcons.Length == 0) return;

            for (int i = 0; i < heartIcons.Length; i++)
            {
                if (heartIcons[i] == null) continue;
                heartIcons[i].sprite = (i < currentLives) ? fullHeartSprite : emptyHeartSprite;
                heartIcons[i].color = Color.white;
            }
        }

        #endregion

        #region Game Over UI

        /// <summary>
        /// Displays the Game Over panel with final and all-time high scores.
        /// </summary>
        public void ShowGameOver(int score, int highScore)
        {
            AutoWireReferences();

            if (finalScoreText != null)
            {
                finalScoreText.text = $"Score: {score}";
            }

            if (highScoreText != null)
            {
                highScoreText.text = $"High Score: {highScore}";
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(true);
            }
            else
            {
                CreateFallbackGameOverUI(score, highScore);
            }
        }

        public void HideGameOver()
        {
            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
            }
        }

        public void OnRestartButtonClicked()
        {
            Debug.Log("[UIManager] Restart Button Clicked. Reloading active scene...");
            Scene activeScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(activeScene.name);
        }

        private void CreateFallbackGameOverUI(int score, int highScore)
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

            gameOverPanel = new GameObject("GameOverPanel");
            gameOverPanel.transform.SetParent(canvasObj.transform, false);

            Image panelBg = gameOverPanel.AddComponent<Image>();
            panelBg.color = new Color(0f, 0f, 0f, 0.85f);
            RectTransform panelRt = gameOverPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(gameOverPanel.transform, false);
            TextMeshProUGUI title = titleObj.AddComponent<TextMeshProUGUI>();
            title.text = "GAME OVER";
            title.fontSize = 52;
            title.alignment = TextAlignmentOptions.Center;
            title.color = new Color(1f, 0.25f, 0.25f, 1f);
            RectTransform titleRt = titleObj.GetComponent<RectTransform>();
            titleRt.anchoredPosition = new Vector2(0, 100);

            GameObject scoreObj = new GameObject("FinalScoreText");
            scoreObj.transform.SetParent(gameOverPanel.transform, false);
            finalScoreText = scoreObj.AddComponent<TextMeshProUGUI>();
            finalScoreText.text = $"Score: {score}";
            finalScoreText.fontSize = 32;
            finalScoreText.alignment = TextAlignmentOptions.Center;
            RectTransform scoreRt = scoreObj.GetComponent<RectTransform>();
            scoreRt.anchoredPosition = new Vector2(0, 30);

            GameObject hsObj = new GameObject("HighScoreText");
            hsObj.transform.SetParent(gameOverPanel.transform, false);
            highScoreText = hsObj.AddComponent<TextMeshProUGUI>();
            highScoreText.text = $"High Score: {highScore}";
            highScoreText.fontSize = 26;
            highScoreText.alignment = TextAlignmentOptions.Center;
            highScoreText.color = new Color(1f, 0.85f, 0.2f, 1f);
            RectTransform hsRt = hsObj.GetComponent<RectTransform>();
            hsRt.anchoredPosition = new Vector2(0, -20);

            GameObject btnObj = new GameObject("RestartButton");
            btnObj.transform.SetParent(gameOverPanel.transform, false);
            restartButton = btnObj.AddComponent<Button>();
            Image btnImg = btnObj.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.7f, 1f, 1f);
            RectTransform btnRt = btnObj.GetComponent<RectTransform>();
            btnRt.anchoredPosition = new Vector2(0, -90);
            btnRt.sizeDelta = new Vector2(220, 60);

            GameObject btnTxtObj = new GameObject("BtnText");
            btnTxtObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI btnTxt = btnTxtObj.AddComponent<TextMeshProUGUI>();
            btnTxt.text = "PLAY AGAIN";
            btnTxt.fontSize = 24;
            btnTxt.alignment = TextAlignmentOptions.Center;
            btnTxt.color = Color.white;
            RectTransform btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
            btnTxtRt.anchorMin = Vector2.zero;
            btnTxtRt.anchorMax = Vector2.one;
            btnTxtRt.offsetMin = Vector2.zero;
            btnTxtRt.offsetMax = Vector2.zero;

            restartButton.onClick.AddListener(OnRestartButtonClicked);
        }

        #endregion

        #region Floating Score & Combo Text

        /// <summary>
        /// Task 2 & 3: Instantiates temporary floating arcade text at the specified world position.
        /// Floats upward and fades out over the configured duration (default: 1.0s).
        /// </summary>
        public void SpawnFloatingText(Vector3 worldPos, string text, Color? color = null)
        {
            StartCoroutine(FloatingTextRoutine(worldPos, text, color ?? new Color(1f, 0.88f, 0.15f, 1f)));
        }

        private IEnumerator FloatingTextRoutine(Vector3 worldPos, string text, Color color)
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
                tmp.fontSize = _floatingTextFontSize;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                tmp.sortingOrder = 200; // Above balloons and drawing trails
            }

            if (tmp != null)
            {
                tmp.text = text;
                tmp.color = color;
            }

            float elapsed = 0f;
            Vector3 startPos = popupObj.transform.position;
            Vector3 targetPos = startPos + Vector3.up * 1.2f;

            while (elapsed < _floatingTextDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _floatingTextDuration);

                if (popupObj != null)
                {
                    popupObj.transform.position = Vector3.Lerp(startPos, targetPos, Mathf.SmoothStep(0f, 1f, t));

                    // Arcade sinusoidal scale bounce
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

        #endregion
    }
}
