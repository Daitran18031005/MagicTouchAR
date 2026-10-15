using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MagicTouch.Gameplay;
using MagicTouch.Drawing;
using MagicTouch.Recognition;
using MagicTouch.Tracking;
using MagicTouch.CameraControl;

namespace MagicTouch.Editor
{
    /// <summary>
    /// Automated Scene & UI generator for Magic Touch:
    /// - Creates/connects EnemySpawner, GameManager.
    /// - Generates complete HUD (Score, Lives, Combo).
    /// - Generates GameOverPanel modal with Title, FinalScoreText, HighScoreText, and wired RestartButton.
    /// - Saves scene when a menu command is run. Nothing here runs automatically on domain reload.
    /// </summary>
    public static class MagicTouchSceneSetup
    {
        /// <summary>
        /// Manual replacement for the former domain-reload autorun. Adds only missing GameOver UI,
        /// tracking components and references; existing values, including the input mode, are kept.
        /// </summary>
        [MenuItem("Tools/Magic Touch/Repair Scene Wiring (manual)")]
        public static void RepairSceneWiring()
        {
            if (Application.isPlaying) return;

            GameManager gm = Object.FindAnyObjectByType<GameManager>();
            if (gm != null && gm.gameOverPanel == null)
            {
                GenerateGameOverUIInternal(gm, true);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log("<color=green>[MagicTouchSceneSetup] Created and wired missing GameOver UI.</color>");
            }

            EnsureMediaPipeConfiguredInternal();
        }

        [MenuItem("Tools/Magic Touch/Setup Full Gameplay Scene")]
        public static void SetupFullScene()
        {
            Debug.Log("<color=cyan>[MagicTouchSceneSetup] Setting up complete Magic Touch gameplay scene...</color>");

            // 1. Force reimport sprites as 2D Sprites
            SpriteImportSettings.ReimportAllSprites();

            // 2. Ensure / Update EnemyBalloon Prefab
            MagicTouchPrefabBuilder.CreateEnemyPrefab();
            GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyBalloon.prefab");

            // 3. Setup EnemySpawner in Scene
            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner == null)
            {
                GameObject spawnerObj = new GameObject("EnemySpawner");
                spawner = spawnerObj.AddComponent<EnemySpawner>();
                Undo.RegisterCreatedObjectUndo(spawnerObj, "Create EnemySpawner");
                Debug.Log("[MagicTouchSceneSetup] Created EnemySpawner GameObject.");
            }

            SerializedObject spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("_enemyPrefab").objectReferenceValue = enemyPrefab;
            spawnerSo.FindProperty("_autoDetectCameraBounds").boolValue = true;
            spawnerSo.FindProperty("_spawnInterval").floatValue = 2.0f;
            spawnerSo.FindProperty("_baseFallSpeed").floatValue = 1.3f;

            // Configure all 10 symbols with sprites and colorful balloon tints
            SerializedProperty visualsProp = spawnerSo.FindProperty("_symbolVisuals");
            visualsProp.ClearArray();

            var symbolConfigs = new (GestureSymbol symbol, string spriteName, Color tint)[]
            {
                (GestureSymbol.V, "symbol_v", new Color(1f, 0.85f, 0.15f)),            // Yellow
                (GestureSymbol.Caret, "symbol_caret", new Color(0.25f, 0.6f, 1f)),     // Blue
                (GestureSymbol.HorizontalLine, "symbol_horizontal", new Color(0.2f, 0.85f, 0.9f)), // Cyan
                (GestureSymbol.VerticalLine, "symbol_vertical", new Color(1f, 0.35f, 0.7f)),       // Pink
                (GestureSymbol.O, "symbol_o", new Color(1f, 0.55f, 0.15f)),           // Orange
                (GestureSymbol.Z, "symbol_z", new Color(0.95f, 0.25f, 0.3f)),          // Red
                (GestureSymbol.W, "symbol_w", new Color(0.7f, 0.3f, 0.95f)),          // Purple
                (GestureSymbol.S, "symbol_s", new Color(0.25f, 0.85f, 0.35f)),        // Green
                (GestureSymbol.E, "symbol_e", new Color(0.85f, 0.3f, 0.5f)),          // Rose
                (GestureSymbol.X, "symbol_x", new Color(0.95f, 0.25f, 0.3f))          // Red
            };

            for (int i = 0; i < symbolConfigs.Length; i++)
            {
                visualsProp.InsertArrayElementAtIndex(i);
                SerializedProperty elem = visualsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("symbol").enumValueIndex = (int)symbolConfigs[i].symbol;

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Symbols/{symbolConfigs[i].spriteName}.png");
                elem.FindPropertyRelative("sprite").objectReferenceValue = sprite;
                elem.FindPropertyRelative("balloonTint").colorValue = symbolConfigs[i].tint;
            }
            spawnerSo.ApplyModifiedProperties();

            // 4. Setup GameManager in Scene
            GameManager gm = Object.FindAnyObjectByType<GameManager>();
            if (gm == null)
            {
                GameObject gmObj = new GameObject("GameManager");
                gm = gmObj.AddComponent<GameManager>();
                Undo.RegisterCreatedObjectUndo(gmObj, "Create GameManager");
                Debug.Log("[MagicTouchSceneSetup] Created GameManager GameObject.");
            }

            // 5. Generate UI Hierarchy (Canvas, HUD, GameOverPanel, Button)
            GenerateGameOverUIInternal(gm);

            // 6. Connect DrawingManager
            DrawingManager dm = Object.FindAnyObjectByType<DrawingManager>();
            if (dm != null)
            {
                SerializedObject dmSo = new SerializedObject(dm);
                SentisRecognizer sentis = dm.GetComponent<SentisRecognizer>() ?? Object.FindAnyObjectByType<SentisRecognizer>();
                if (sentis != null)
                {
                    dmSo.FindProperty("_sentisRecognizer").objectReferenceValue = sentis;
                }
                dmSo.ApplyModifiedProperties();
            }

            // 7. Mark scene dirty and save
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("<color=green>[MagicTouchSceneSetup] Scene and UI setup successfully completed and saved!</color>");
        }

        [MenuItem("Tools/Magic Touch/Generate GameOver UI")]
        public static void GenerateGameOverUI()
        {
            GameManager gm = Object.FindAnyObjectByType<GameManager>();
            if (gm == null)
            {
                GameObject gmObj = new GameObject("GameManager");
                gm = gmObj.AddComponent<GameManager>();
                Debug.Log("[MagicTouchSceneSetup] Created GameManager GameObject.");
            }

            GenerateGameOverUIInternal(gm);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green>[MagicTouchSceneSetup] GameOver UI generated and wired to GameManager!</color>");
        }

        private static void GenerateGameOverUIInternal(GameManager gm, bool keepExisting = false)
        {
            // keepExisting = true (Repair Scene Wiring): only add what is missing, never overwrite existing values.
            // 1. Find or create GameUI_Canvas (ScreenSpaceOverlay so it's always in front of sprites)
            GameObject canvasObj = GameObject.Find("GameUI_Canvas");
            Canvas canvas;
            if (canvasObj == null)
            {
                canvasObj = new GameObject("GameUI_Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;

                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;

                canvasObj.AddComponent<GraphicRaycaster>();
                Undo.RegisterCreatedObjectUndo(canvasObj, "Create GameUI_Canvas");
            }
            else
            {
                canvas = canvasObj.GetComponent<Canvas>();
            }

            // 2. HUD Elements
            // Score Text (Top-Left)
            TextMeshProUGUI scoreTmp = FindOrCreateUIText(canvasObj.transform, "ScoreText",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(50, -40), new Vector2(400, 100), "Score: 0", 48, TextAlignmentOptions.TopLeft, keepExisting);

            // Lives Text (Top-Right)
            TextMeshProUGUI livesTmp = FindOrCreateUIText(canvasObj.transform, "LivesText",
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-50, -40), new Vector2(400, 100), "Lives: 3 ❤ ❤ ❤", 44, TextAlignmentOptions.TopRight, keepExisting);

            // Combo Text (Top-Center)
            bool comboExisted = canvasObj.transform.Find("ComboText") != null;
            TextMeshProUGUI comboTmp = FindOrCreateUIText(canvasObj.transform, "ComboText",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -140), new Vector2(500, 100), "", 56, TextAlignmentOptions.Center, keepExisting);
            if (!keepExisting || !comboExisted)
            {
                comboTmp.color = new Color(1f, 0.85f, 0.2f);
                comboTmp.gameObject.SetActive(false);
            }

            SerializedObject gmSo = new SerializedObject(gm);
            SetReference(gmSo, "scoreText", scoreTmp, keepExisting);
            SetReference(gmSo, "livesText", livesTmp, keepExisting);
            SetReference(gmSo, "comboText", comboTmp, keepExisting);

            // 3. Reuse a GameOver panel GameManager already references elsewhere (e.g. the Phase 9
            //    reference UI) instead of creating a second, overlapping panel under GameUI_Canvas.
            if (gm.gameOverPanel != null && gm.gameOverPanel.transform.parent != canvasObj.transform)
            {
                gmSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(gm);
                return;
            }

            // Create or Find GameOverPanel
            Transform existingPanel = canvasObj.transform.Find("GameOverPanel");
            bool panelExisted = existingPanel != null;
            GameObject panelObj;
            if (existingPanel != null)
            {
                panelObj = existingPanel.gameObject;
            }
            else
            {
                panelObj = new GameObject("GameOverPanel");
                panelObj.transform.SetParent(canvasObj.transform, false);
                Undo.RegisterCreatedObjectUndo(panelObj, "Create GameOverPanel");
            }

            if (!keepExisting || !panelExisted)
            {
                RectTransform panelRt = panelObj.GetComponent<RectTransform>() ?? panelObj.AddComponent<RectTransform>();
                panelRt.anchorMin = new Vector2(0.5f, 0.5f);
                panelRt.anchorMax = new Vector2(0.5f, 0.5f);
                panelRt.pivot = new Vector2(0.5f, 0.5f);
                panelRt.anchoredPosition = Vector2.zero;
                panelRt.sizeDelta = new Vector2(700, 850);

                Image panelImg = panelObj.GetComponent<Image>() ?? panelObj.AddComponent<Image>();
                panelImg.color = new Color(0.06f, 0.06f, 0.1f, 0.92f); // Semi-transparent black backdrop
            }

            // 4. GameOverPanel Children:
            // 4a. Title: "GAME OVER"
            bool titleExisted = panelObj.transform.Find("GameOverTitle") != null;
            TextMeshProUGUI titleTmp = FindOrCreateUIText(panelObj.transform, "GameOverTitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -90), new Vector2(600, 120), "GAME OVER", 64, TextAlignmentOptions.Center, keepExisting);
            if (!keepExisting || !titleExisted)
            {
                titleTmp.fontStyle = FontStyles.Bold;
                titleTmp.color = new Color(1f, 0.25f, 0.3f); // Arcade Red
            }

            // 4b. Final Score Text
            bool finalExisted = panelObj.transform.Find("FinalScoreText") != null;
            TextMeshProUGUI finalScoreTmp = FindOrCreateUIText(panelObj.transform, "FinalScoreText",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -260), new Vector2(550, 90), "Score: 0", 48, TextAlignmentOptions.Center, keepExisting);
            if (!keepExisting || !finalExisted)
            {
                finalScoreTmp.color = Color.white;
            }

            // 4c. High Score Text
            bool highExisted = panelObj.transform.Find("HighScoreText") != null;
            TextMeshProUGUI highScoreTmp = FindOrCreateUIText(panelObj.transform, "HighScoreText",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0, -370), new Vector2(550, 90), "High Score: 0", 42, TextAlignmentOptions.Center, keepExisting);
            if (!keepExisting || !highExisted)
            {
                highScoreTmp.color = new Color(1f, 0.85f, 0.2f); // Golden yellow
            }

            // 4d. Restart Button
            Transform existingBtn = panelObj.transform.Find("RestartButton");
            bool buttonExisted = existingBtn != null;
            GameObject btnObj;
            if (existingBtn != null)
            {
                btnObj = existingBtn.gameObject;
            }
            else
            {
                btnObj = new GameObject("RestartButton");
                btnObj.transform.SetParent(panelObj.transform, false);
            }

            Button btn = btnObj.GetComponent<Button>() ?? btnObj.AddComponent<Button>();
            if (!keepExisting || !buttonExisted)
            {
                RectTransform btnRt = btnObj.GetComponent<RectTransform>() ?? btnObj.AddComponent<RectTransform>();
                btnRt.anchorMin = new Vector2(0.5f, 0f);
                btnRt.anchorMax = new Vector2(0.5f, 0f);
                btnRt.pivot = new Vector2(0.5f, 0.5f);
                btnRt.anchoredPosition = new Vector2(0, 140);
                btnRt.sizeDelta = new Vector2(340, 100);

                Image btnImg = btnObj.GetComponent<Image>() ?? btnObj.AddComponent<Image>();
                btnImg.color = new Color(0.18f, 0.75f, 0.35f); // Vibrant Emerald Green

                ColorBlock cb = btn.colors;
                cb.highlightedColor = new Color(0.25f, 0.85f, 0.45f);
                cb.pressedColor = new Color(0.12f, 0.55f, 0.25f);
                btn.colors = cb;
            }

            // Wire onClick to GameManager.RestartGame exactly once. RemoveAllListeners only clears
            // runtime listeners, so persistent ones must be checked before adding (no duplicates).
            btn.onClick.RemoveAllListeners();
            if (!HasPersistentListener(btn.onClick, gm, nameof(GameManager.RestartGame)))
            {
                UnityEventTools.AddPersistentListener(btn.onClick, gm.RestartGame);
            }

            // Button label text
            bool labelExisted = btnObj.transform.Find("Text (TMP)") != null;
            TextMeshProUGUI btnTmp = FindOrCreateUIText(btnObj.transform, "Text (TMP)",
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero, "RESTART", 38, TextAlignmentOptions.Center, keepExisting);
            if (!keepExisting || !labelExisted)
            {
                btnTmp.fontStyle = FontStyles.Bold;
                btnTmp.color = Color.white;
            }

            // Hide GameOverPanel by default
            if (!keepExisting || !panelExisted)
            {
                panelObj.SetActive(false);
            }

            // 5. Connect GameOver UI fields to GameManager
            SetReference(gmSo, "gameOverPanel", panelObj, keepExisting);
            SetReference(gmSo, "finalScoreText", finalScoreTmp, keepExisting);
            SetReference(gmSo, "highScoreText", highScoreTmp, keepExisting);
            gmSo.ApplyModifiedProperties();
            EditorUtility.SetDirty(gm);
        }

        private static void SetReference(SerializedObject so, string propertyName, Object value, bool keepExisting)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            if (!keepExisting || property.objectReferenceValue == null)
            {
                property.objectReferenceValue = value;
            }
        }

        private static bool HasPersistentListener(UnityEngine.Events.UnityEventBase unityEvent, Object target, string methodName)
        {
            for (int i = 0; i < unityEvent.GetPersistentEventCount(); i++)
            {
                if (unityEvent.GetPersistentTarget(i) == target && unityEvent.GetPersistentMethodName(i) == methodName)
                {
                    return true;
                }
            }
            return false;
        }

        private static TextMeshProUGUI FindOrCreateUIText(
            Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta,
            string defaultText, float fontSize, TextAlignmentOptions alignment, bool keepExisting = false)
        {
            Transform existing = parent.Find(name);
            if (existing != null && keepExisting)
            {
                return existing.GetComponent<TextMeshProUGUI>() ?? existing.gameObject.AddComponent<TextMeshProUGUI>();
            }
            GameObject textObj;
            TextMeshProUGUI tmp;

            if (existing != null)
            {
                textObj = existing.gameObject;
                tmp = textObj.GetComponent<TextMeshProUGUI>() ?? textObj.AddComponent<TextMeshProUGUI>();
            }
            else
            {
                textObj = new GameObject(name);
                textObj.transform.SetParent(parent, false);
                tmp = textObj.AddComponent<TextMeshProUGUI>();
            }

            RectTransform rt = textObj.GetComponent<RectTransform>() ?? textObj.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            tmp.text = defaultText;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;

            return tmp;
        }

        public static void EnsureMediaPipeConfiguredInternal()
        {
            if (Application.isPlaying) return;

            HandTrackingBridge bridge = Object.FindAnyObjectByType<HandTrackingBridge>();
            if (bridge == null) return;

            bool sceneModified = false;

            MediaPipeUDPReceiver receiver = bridge.GetComponent<MediaPipeUDPReceiver>();
            if (receiver == null)
            {
                receiver = Undo.AddComponent<MediaPipeUDPReceiver>(bridge.gameObject);
                sceneModified = true;
            }

            HandCursorVisualizer visualizer = bridge.GetComponent<HandCursorVisualizer>();
            if (visualizer == null)
            {
                visualizer = Undo.AddComponent<HandCursorVisualizer>(bridge.gameObject);
                sceneModified = true;
            }

            GameObject camBgObj = GameObject.Find("CameraBackground");
            RawImage rawImg = camBgObj != null ? camBgObj.GetComponent<RawImage>() : null;
            if (rawImg != null)
            {
                SerializedObject receiverSo = new SerializedObject(receiver);
                if (receiverSo.FindProperty("_cameraDisplay").objectReferenceValue == null)
                {
                    receiverSo.FindProperty("_cameraDisplay").objectReferenceValue = rawImg;
                    receiverSo.ApplyModifiedProperties();
                    sceneModified = true;
                }

                SerializedObject bridgeSo = new SerializedObject(bridge);
                if (bridgeSo.FindProperty("_cameraDisplayRect").objectReferenceValue == null)
                {
                    bridgeSo.FindProperty("_cameraDisplayRect").objectReferenceValue = rawImg.rectTransform;
                    bridgeSo.ApplyModifiedProperties();
                    sceneModified = true;
                }

                // The input mode (_useMediaPipeStream) is chosen via Switch Input; never overwritten here.
            }

            if (sceneModified)
            {
                EditorUtility.SetDirty(bridge);
                EditorUtility.SetDirty(receiver);
                EditorUtility.SetDirty(visualizer);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Debug.Log("<color=green>[MagicTouchSceneSetup] Automatically configured MediaPipe UDP Receiver and Hand Cursor Visualizer!</color>");
            }
        }

        [MenuItem("Tools/Magic Touch/Setup AR MediaPipe Tracking")]
        public static void SetupARMediaPipeTracking()
        {
            Debug.Log("<color=cyan>[MagicTouchSceneSetup] Configuring AR MediaPipe Hand Tracking...</color>");

            EnsureMediaPipeConfiguredInternal();

            // Set input simulation to false (AR mode)
            SwitchToARHandTracking();

            Debug.Log("<color=green>[MagicTouchSceneSetup] AR MediaPipe Tracking configured successfully! Use 'Tools/Magic Touch/Start MediaPipe Python Tracker' to launch tracker.</color>");
        }

        [MenuItem("Tools/Magic Touch/Start MediaPipe Python Tracker")]
        public static void StartMediaPipeTrackerMenu()
        {
            MediaPipeUDPReceiver receiver = Object.FindAnyObjectByType<MediaPipeUDPReceiver>();
            if (receiver != null)
            {
                receiver.StartPythonTracker();
            }
            else
            {
                string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
                string batPath = System.IO.Path.Combine(projectRoot, "Tools", "run_hand_tracker.bat");
                if (System.IO.File.Exists(batPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = batPath,
                        WorkingDirectory = projectRoot,
                        UseShellExecute = true
                    });
                }
            }
        }

        [MenuItem("Tools/Magic Touch/Switch Input/AR Hand Tracking (MediaPipe)")]
        public static void SwitchToARHandTracking()
        {
            SetSimulationMode(false);
            Debug.Log("<color=cyan>[MagicTouch] Input switched to AR Hand Tracking (MediaPipe). Make sure Python streamer is running!</color>");
        }

        [MenuItem("Tools/Magic Touch/Switch Input/Mouse Simulation (PC Debug)")]
        public static void SwitchToMouseSimulation()
        {
            SetSimulationMode(true);
            Debug.Log("<color=yellow>[MagicTouch] Input switched to Mouse Simulation (Left-Click to draw).</color>");
        }

        private static void SetSimulationMode(bool enableMouseSim)
        {
            DrawingManager dm = Object.FindAnyObjectByType<DrawingManager>();
            if (dm != null)
            {
                SerializedObject dmSo = new SerializedObject(dm);
                dmSo.FindProperty("_enableEditorSimulation").boolValue = enableMouseSim;
                dmSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(dm);
            }

            HandTrackingBridge bridge = Object.FindAnyObjectByType<HandTrackingBridge>();
            if (bridge != null)
            {
                SerializedObject bSo = new SerializedObject(bridge);
                bSo.FindProperty("_enableEditorSimulation").boolValue = enableMouseSim;
                bSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(bridge);
            }

            ARCameraBackground arCam = Object.FindAnyObjectByType<ARCameraBackground>();
            if (arCam != null)
            {
                SerializedObject arCamSo = new SerializedObject(arCam);
                arCamSo.FindProperty("_useMediaPipeStream").boolValue = !enableMouseSim;
                arCamSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(arCam);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        [MenuItem("Tools/Magic Touch/Configure & Wire All Sprites (Background, Enemies, Balloons, UI)")]
        public static void ConfigureAllSprites()
        {
            Debug.Log("<color=cyan>[MagicTouchSceneSetup] Configuring all sprites for Magic Touch...</color>");

            // 1. Setup Background in Scene & GameManager
            Sprite bgSprite = LoadFirstSprite("Assets/Sprites/Background/CastleBackground.png");
            GameObject bgObj = GameObject.Find("Background");
            if (bgObj == null)
            {
                bgObj = new GameObject("Background");
                Undo.RegisterCreatedObjectUndo(bgObj, "Create Background");
            }
            SpriteRenderer bgRenderer = bgObj.GetComponent<SpriteRenderer>();
            if (bgRenderer == null) bgRenderer = bgObj.AddComponent<SpriteRenderer>();
            if (bgSprite != null)
            {
                bgRenderer.sprite = bgSprite;
                bgRenderer.sortingOrder = -10;
                bgObj.transform.position = new Vector3(0, 0, 5);
            }

            // 2. Setup Player Wizard in Scene & GameManager
            Sprite wizSprite = LoadFirstSprite("Assets/Sprites/Main/Wizard.png");
            GameObject wizObj = GameObject.Find("PlayerWizard");
            if (wizObj == null)
            {
                wizObj = new GameObject("PlayerWizard");
                Undo.RegisterCreatedObjectUndo(wizObj, "Create PlayerWizard");
            }
            SpriteRenderer wizRenderer = wizObj.GetComponent<SpriteRenderer>();
            if (wizRenderer == null) wizRenderer = wizObj.AddComponent<SpriteRenderer>();
            if (wizSprite != null)
            {
                wizRenderer.sprite = wizSprite;
                wizRenderer.sortingOrder = 5;
                wizObj.transform.position = new Vector3(0, -3.8f, 0);
            }

            // 3. Connect to GameManager
            GameManager gm = Object.FindAnyObjectByType<GameManager>();
            if (gm != null)
            {
                SerializedObject gmSo = new SerializedObject(gm);
                gmSo.FindProperty("backgroundSprite").objectReferenceValue = bgRenderer;
                gmSo.FindProperty("backgroundSpriteAsset").objectReferenceValue = bgSprite;
                gmSo.FindProperty("playerWizardSprite").objectReferenceValue = wizRenderer;
                gmSo.FindProperty("playerWizardSpriteAsset").objectReferenceValue = wizSprite;
                gmSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(gm);
            }

            // 4. Populate EnemySpawner pools
            EnemySpawner spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                SerializedObject spSo = new SerializedObject(spawner);

                // Enemies
                string[] enemyPaths = new string[]
                {
                    "Assets/Sprites/Enemies/Enemy.png",
                    "Assets/Sprites/Enemies/Enemy2.png",
                    "Assets/Sprites/Enemies/enemy_frog.png",
                    "Assets/Sprites/Enemies/enemy_knight.png"
                };
                List<Sprite> enemyList = new List<Sprite>();
                foreach (var p in enemyPaths)
                {
                    Sprite s = LoadFirstSprite(p);
                    if (s != null) enemyList.Add(s);
                }
                SerializedProperty enemyProp = spSo.FindProperty("enemySpritePool");
                enemyProp.ClearArray();
                for (int i = 0; i < enemyList.Count; i++)
                {
                    enemyProp.InsertArrayElementAtIndex(i);
                    enemyProp.GetArrayElementAtIndex(i).objectReferenceValue = enemyList[i];
                }

                // Balloons
                string[] balloonNames = new string[]
                {
                    "balloon_blue", "balloon_cyan", "balloon_green", "balloon_orange", "balloon_pink",
                    "balloon_purple", "balloon_red", "balloon_silver", "balloon_white", "balloon_yellow"
                };
                List<Sprite> balloonList = new List<Sprite>();
                foreach (var b in balloonNames)
                {
                    Sprite s = LoadFirstSprite($"Assets/Sprites/Balloons/{b}.png");
                    if (s != null) balloonList.Add(s);
                }
                SerializedProperty balloonProp = spSo.FindProperty("balloonSpritePool");
                balloonProp.ClearArray();
                for (int i = 0; i < balloonList.Count; i++)
                {
                    balloonProp.InsertArrayElementAtIndex(i);
                    balloonProp.GetArrayElementAtIndex(i).objectReferenceValue = balloonList[i];
                }

                spSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(spawner);

                // Also update prefab
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnemyBalloon.prefab");
                if (prefab != null)
                {
                    EnemyBalloon eb = prefab.GetComponent<EnemyBalloon>();
                    if (eb != null)
                    {
                        SerializedObject ebSo = new SerializedObject(eb);
                        SerializedProperty ebEnemyProp = ebSo.FindProperty("enemySprites");
                        ebEnemyProp.ClearArray();
                        for (int i = 0; i < enemyList.Count; i++)
                        {
                            ebEnemyProp.InsertArrayElementAtIndex(i);
                            ebEnemyProp.GetArrayElementAtIndex(i).objectReferenceValue = enemyList[i];
                        }

                        SerializedProperty ebBalloonProp = ebSo.FindProperty("balloonSprites");
                        ebBalloonProp.ClearArray();
                        for (int i = 0; i < balloonList.Count; i++)
                        {
                            ebBalloonProp.InsertArrayElementAtIndex(i);
                            ebBalloonProp.GetArrayElementAtIndex(i).objectReferenceValue = balloonList[i];
                        }

                        // Pop VFX Sprites
                        Sprite pLeft = LoadFirstSprite("Assets/Sprites/VFX/balloon_pop_left.png");
                        Sprite pRight = LoadFirstSprite("Assets/Sprites/VFX/balloon_pop_right.png");
                        Sprite pSparkle = LoadFirstSprite("Assets/Sprites/VFX/pop_sparkle.png");
                        if (pLeft != null) ebSo.FindProperty("popLeftSprite").objectReferenceValue = pLeft;
                        if (pRight != null) ebSo.FindProperty("popRightSprite").objectReferenceValue = pRight;
                        if (pSparkle != null) ebSo.FindProperty("popSparkleSprite").objectReferenceValue = pSparkle;

                        ebSo.ApplyModifiedProperties();
                        EditorUtility.SetDirty(prefab);
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green>[MagicTouchSceneSetup] All Sprites (Background, Wizard, Enemies, Balloons, Prefab) successfully configured and saved!</color>");
        }

        private static Sprite LoadFirstSprite(string path)
        {
            Sprite sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sp != null) return sp;

            var all = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var a in all)
            {
                if (a is Sprite s) return s;
            }
            return null;
        }
    }
}
