using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using MagicTouch.Gameplay;
using MagicTouch.UI;

/// <summary>Non-destructive, repeatable reference UI builder. Owns only ReferenceUI_Canvas.</summary>
public static partial class UIAutoBuilder
{
    public const string RootName = "ReferenceUI_Canvas";
    private const string Art = "Assets/UI/Phase9/";
    private static TMP_FontAsset _font;
    private static MenuManager _menu;
    private static Material _textMaterial;
    private static readonly Color Cream = new Color32(255, 240, 187, 255);
    private static readonly Color Ink = new Color32(34, 22, 40, 255);

    [MenuItem("Tools/MagicTouch/UI/Build Reference UI")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Build UI in Edit Mode.");
        var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gm == null) throw new InvalidOperationException("Open the gameplay scene first.");
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Art + "PressStart2P SDF.asset");
        if (_font == null)
            _font = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.IndexOf("pixel", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("arcade", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>).FirstOrDefault();
        if (_font == null) throw new InvalidOperationException("Import a pixel TMP font before building.");
        foreach (string name in new[] { "Banner", "Circle", "Gate", "GreenButton", "Home", "Medal", "Modal", "Play", "Restart", "Ribbon", "Settings", "Star", "Tablet", "Trophy", "Wall", "Defeat" })
            if (Sprite(name) == null) throw new InvalidOperationException("Missing UI sprite: " + name);
        _textMaterial = TextMaterial("ArcadeText", new Color32(29, 18, 41, 255));
        Undo.IncrementCurrentGroup();
        int undo = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Build Magic Touch reference UI");
        var root = GameObject.Find(RootName);
        if (root == null) root = Node(null, RootName).gameObject;
        Undo.RegisterFullObjectHierarchyUndo(root, "Update reference UI");
        Canvas canvas = Ensure<Canvas>(root);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = Ensure<CanvasScaler>(root);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        Ensure<GraphicRaycaster>(root);
        _menu = Ensure<MenuManager>(root);
        _menu.combatMusic = gm.combatCastleMusic;
        _menu.bgmSource = gm.bgmSource != null ? gm.bgmSource : GameObject.Find("BGM_Source")?.GetComponent<AudioSource>();
        _menu.sfxSource = gm.sfxSource != null ? gm.sfxSource : GameObject.Find("SFX_Source")?.GetComponent<AudioSource>();
        BuildMain(root.transform);
        BuildPause(root.transform);
        BuildGameOver(root.transform);
        BuildSettings(root.transform);
        BuildCredits(root.transform);
        BuildLoading(root.transform, gm);
        // Old canvas stays intact for rollback; its obsolete loading view must not cover gameplay.
        var legacyCanvas = gm.gameObject.scene.GetRootGameObjects().FirstOrDefault(o => o.name == "GameUI_Canvas");
        var legacyLoading = legacyCanvas != null ? legacyCanvas.transform.Find("LoadingPanel") : null;
        if (legacyLoading != null && legacyLoading.gameObject != _menu.loadingPanel)
        {
            Undo.RecordObject(legacyLoading.gameObject, "Retire migrated loading view");
            legacyLoading.gameObject.SetActive(false);
            _menu.legacyLoadingPanels = new[] { legacyLoading.gameObject };
        }
        var hud = Panel(root.transform, "GameplayHUD", false);
        _menu.gameplayHUDPanel = hud.gameObject;
        var safe = Safe(hud);
        var pause = Button(safe, "PauseButton", "II", "Circle", Vector2.zero, new Vector2(94, 94), _menu.PauseGame, 34);
        var pr = (RectTransform)pause.transform;
        pr.anchorMin = pr.anchorMax = pr.pivot = Vector2.one;
        pr.anchoredPosition = new Vector2(-28, -100);
        Undo.RecordObject(gm, "Connect reference UI");
        gm.gameOverPanel = _menu.gameOverPanel;
        gm.finalScoreText = _menu.finalScoreText;
        gm.highScoreText = _menu.highScoreText;
        // Preserve an existing UIManager and every legacy object; only rebind the new view.
        if (gm.uiManager != null)
        {
            Undo.RecordObject(gm.uiManager, "Connect reference UI");
            gm.uiManager.gameOverPanel = _menu.gameOverPanel;
            gm.uiManager.finalScoreText = _menu.finalScoreText;
            gm.uiManager.highScoreText = _menu.highScoreText;
            EditorUtility.SetDirty(gm.uiManager);
        }
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("ReferenceUI_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            Undo.RegisterCreatedObjectUndo(es, "Create UI event system");
        }
        BuildVersus(root.transform, gm);
        Preview("MainMenu");
        EditorUtility.SetDirty(gm);
        EditorUtility.SetDirty(_menu);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Undo.CollapseUndoOperations(undo);
        Debug.Log("[UIAutoBuilder] Reference UI built. Existing gameplay objects and assets preserved.");
    }

    private static void BuildMain(Transform root)
    {
        var panel = Panel(root, "MainMenu", true);
        _menu.mainMenuPanel = panel.gameObject;
        var bg = Picture(panel, "CastleBackground", AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Background/CastleBackground.png"), Vector2.zero, Vector2.one);
        Stretch(bg.rectTransform);
        bg.preserveAspect = false;
        var shade = Picture(panel, "Atmosphere", null, Vector2.zero, Vector2.one);
        Stretch(shade.rectTransform); shade.color = new Color(.08f, .035f, .15f, .40f);
        var design = Design(Safe(panel), new Vector2(940, 1500), 18);
        var wall = Picture(design, "Battlements", Sprite("Wall"), new Vector2(0, -16), new Vector2(940, 294));
        var wallBody = Picture(design, "WallBody", WallBody(), new Vector2(0, -448), new Vector2(940, 576));
        wallBody.type = Image.Type.Tiled;
        wallBody.rectTransform.SetAsFirstSibling();
        Picture(design, "Portcullis", Sprite("Gate"), new Vector2(0, -245), new Vector2(560, 700));
        var wizard = Picture(design, "Wizard", Wizard(), new Vector2(0, 246), new Vector2(255, 280));
        wizard.preserveAspect = true;
        var staff = Picture(design, "StarStaff", null, new Vector2(95, 247), new Vector2(12, 190));
        staff.color = new Color32(174, 107, 63, 255);
        Picture(design, "MagicStar", Sprite("Star"), new Vector2(95, 347), new Vector2(74, 74));
        var title = Label(design, "Title", "MAGIC\nTOUCH", new Vector2(0, 560), new Vector2(880, 260), 98, new Color32(133, 250, 255, 255));
        title.characterSpacing = 6;
        title.fontSharedMaterial = TextMaterial("ArcadeTitle", new Color32(25, 73, 137, 255));
        var outline = Ensure<Outline>(title.gameObject);
        outline.effectColor = new Color32(25, 73, 137, 255); outline.effectDistance = new Vector2(5, -5);
        Label(design, "Tagline", "DRAW YOUR MAGIC", new Vector2(0, 405), new Vector2(760, 46), 23, Cream);
        var buttons = Node(design, "MenuButtons");
        Place(buttons, new Vector2(0, -270), new Vector2(430, 400));
        var layout = Ensure<VerticalLayoutGroup>(buttons.gameObject);
        layout.spacing = 25; layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        MenuButton(buttons, "PlayButton", "PLAY SOLO", _menu.StartGame, new Color32(163, 250, 255, 255));
        MenuButton(buttons, "SettingsButton", "SETTINGS", _menu.OpenSettings, new Color32(255, 229, 152, 255));
        MenuButton(buttons, "CreditsButton", "CREDITS", _menu.OpenCredits, new Color32(229, 193, 255, 255));
        Label(design, "Footer", "A LITTLE MAGIC. A BRAVE DEFENDER.", new Vector2(0, -668), new Vector2(880, 48), 17, Cream);
    }

    private static void BuildPause(Transform root)
    {
        var panel = Panel(root, "Pause", true, true);
        _menu.pausePanel = panel.gameObject;
        var d = Design(Safe(panel), new Vector2(820, 1100));
        Picture(d, "Tablet", Sprite("Tablet"), Vector2.zero, new Vector2(770, 980));
        Header(d, "PAUSED", 452);
        IconButton(d, "SettingsButton", "Settings", new Vector2(286, 320), 86, _menu.OpenSettings);
        Picture(d, "Trophy", Sprite("Trophy"), new Vector2(-200, 238), new Vector2(105, 105));
        Label(d, "BestLabel", "BEST", new Vector2(40, 288), new Vector2(350, 50), 30, Cream);
        _menu.pauseBestText = Label(d, "BestScore", "0", new Vector2(40, 208), new Vector2(360, 74), 51, Cream);
        Picture(d, "Medal", Sprite("Medal"), new Vector2(-223, 28), new Vector2(104, 120));
        Label(d, "ScoreLabel", "TOTAL SCORE", new Vector2(25, 82), new Vector2(400, 52), 25, Ink);
        _menu.pauseScoreText = Label(d, "Score", "0", new Vector2(28, -12), new Vector2(410, 106), 68, Cream);
        var resume = Button(d, "ResumeButton", "RESUME", "GreenButton", new Vector2(0, -205), new Vector2(540, 135), _menu.ResumeGame, 39);
        Picture(resume.transform, "PlayIcon", Sprite("Play"), new Vector2(-194, 0), new Vector2(48, 58));
        Place((RectTransform)resume.transform.Find("Label"), new Vector2(36, 0), new Vector2(410, 110));
        IconButton(d, "HomeButton", "Home", new Vector2(0, -378), 100, _menu.ReturnToMainMenu);
    }

    private static void BuildGameOver(Transform root)
    {
        var panel = Panel(root, "GameOver", true, true);
        _menu.gameOverPanel = panel.gameObject;
        var d = Design(Safe(panel), new Vector2(820, 1120));
        var modal = Picture(d, "MetalFrame", Sprite("Modal"), new Vector2(0, -5), new Vector2(770, 1010));
        var inner = Picture(d, "DarkInterior", Sprite("Modal"), new Vector2(0, -5), new Vector2(719, 954));
        inner.color = new Color32(68, 60, 97, 255);
        Header(d, "GAME OVER", 450);
        Label(d, "Subtitle", "YOU LOSE!", new Vector2(0, 324), new Vector2(620, 72), 40, new Color32(255, 165, 157, 255));
        Picture(d, "AvatarFrame", Sprite("Circle"), new Vector2(0, 145), new Vector2(250, 250));
        var mascot = Picture(d, "DefeatedWizard", Wizard(), new Vector2(0, 149), new Vector2(190, 190));
        mascot.color = new Color32(161, 181, 214, 255);
        mascot.preserveAspect = true;
        mascot.rectTransform.localRotation = Quaternion.Euler(0, 0, -12);
        Picture(d, "DefeatBadge", Sprite("Defeat"), new Vector2(102, 61), new Vector2(62, 62));
        Label(d, "ScoreLabel", "TOTAL SCORE", new Vector2(0, -20), new Vector2(650, 56), 25, Cream);
        _menu.finalScoreText = Label(d, "FinalScore", "0", new Vector2(0, -98), new Vector2(650, 104), 61, Color.white);
        _menu.highScoreText = Label(d, "HighScore", "BEST 0", new Vector2(0, -185), new Vector2(650, 62), 25, Cream);
        IconButton(d, "RestartButton", "Restart", new Vector2(-160, -287), 100, _menu.RestartGame);
        IconButton(d, "HomeButton", "Home", new Vector2(160, -287), 100, _menu.ReturnToMainMenu);
        Button(d, "PlayAgainButton", "TRY AGAIN", "GreenButton", new Vector2(0, -425), new Vector2(560, 122), _menu.RestartGame, 38);
    }

    private static void BuildSettings(Transform root)
    {
        var panel = Panel(root, "Settings", true, true);
        _menu.settingsPanel = panel.gameObject;
        var d = Design(Safe(panel), new Vector2(820, 1200));
        Picture(d, "Tablet", Sprite("Tablet"), Vector2.zero, new Vector2(770, 1080));
        Header(d, "SETTINGS", 500);
        Label(d, "MusicLabel", "MUSIC", new Vector2(0, 315), new Vector2(590, 60), 32, Cream);
        _menu.bgmSlider = Slider(d, "MusicVolume", 220, _menu.SetBGMVolume, .35f);
        Label(d, "EffectsLabel", "SOUND FX", new Vector2(0, 100), new Vector2(590, 60), 32, Cream);
        _menu.sfxSlider = Slider(d, "EffectsVolume", 5, _menu.SetSFXVolume, 1f);
        int level = Mathf.Clamp(PlayerPrefs.GetInt("Difficulty", 1), 0, 2);
        string[] names = { "EASY", "NORMAL", "HARD" };
        _menu.difficultyDisplay = Label(d, "DifficultyLabel", "DIFFICULTY: " + names[level], new Vector2(0, -125), new Vector2(690, 62), 26, Cream);
        _menu.difficultyButtons = new[] {
            Button(d, "EasyButton", "EASY", "Banner", new Vector2(-232, -235), new Vector2(215, 100), _menu.SetDifficultyEasy, 27),
            Button(d, "NormalButton", "NORMAL", "Banner", new Vector2(0, -235), new Vector2(215, 100), _menu.SetDifficultyNormal, 27),
            Button(d, "HardButton", "HARD", "Banner", new Vector2(232, -235), new Vector2(215, 100), _menu.SetDifficultyHard, 27)
        };
        for (int i = 0; i < 3; i++) _menu.difficultyButtons[i].image.color = i == level ? new Color32(148, 255, 130, 255) : Color.white;
        string[] hints = { "SLOWER FALL / FEWER ENEMIES", "CLASSIC SPEED / CLASSIC SPAWNS", "FASTER FALL / MORE ENEMIES" };
        _menu.difficultyHint = Label(d, "DifficultyHint", hints[level], new Vector2(0, -340), new Vector2(690, 55), 17, Cream);
        Button(d, "BackButton", "BACK", "Banner", new Vector2(0, -455), new Vector2(450, 110), _menu.CloseSettings, 35);
    }

    private static void BuildCredits(Transform root)
    {
        var panel = Panel(root, "Credits", true, true);
        _menu.creditsPanel = panel.gameObject;
        var d = Design(Safe(panel), new Vector2(820, 1000));
        Picture(d, "Tablet", Sprite("Tablet"), Vector2.zero, new Vector2(770, 880));
        Header(d, "CREDITS", 400);
        Label(d, "CreditText", "MAGIC TOUCH\n\nAR HAND TRACKING\n& GESTURE MAGIC\n\nFONT: PRESS START 2P\nBY CODEMAN38\nSIL OPEN FONT LICENSE", new Vector2(0, 25), new Vector2(640, 570), 25, Cream);
        Button(d, "BackButton", "BACK", "Banner", new Vector2(0, -320), new Vector2(450, 110), _menu.CloseCredits, 35);
    }

    private static void BuildLoading(Transform root, GameManager gm)
    {
        var panel = Panel(root, "Loading", true, true);
        _menu.loadingPanel = panel.gameObject;
        var d = Design(Safe(panel), new Vector2(900, 800));
        Picture(d, "Tablet", Sprite("Tablet"), Vector2.zero, new Vector2(860, 710));
        Header(d, "GET READY", 305);
        var text = Label(d, "LoadingText", "WAITING FOR CAMERA\n\nSHOW YOUR HANDS", new Vector2(0, 0), new Vector2(770, 300), 29, Cream);
        Button(d, "CancelButton", "MENU", "Banner", new Vector2(0, -230), new Vector2(420, 100), _menu.ReturnToMainMenu, 32);
        Undo.RecordObject(gm, "Connect loading view");
        gm.loadingPanel = panel.gameObject; gm.loadingText = text;
    }

    private static RectTransform Node(Transform parent, string name)
    {
        var old = parent != null ? parent.Find(name) : null;
        if (old != null) return (RectTransform)old;
        var obj = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(obj, "Create UI element");
        obj.transform.SetParent(parent, false);
        return (RectTransform)obj.transform;
    }
    private static T Ensure<T>(GameObject obj) where T : Component { var component = obj.GetComponent<T>(); return component != null ? component : Undo.AddComponent<T>(obj); }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f); rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position; rect.localScale = Vector3.one;
    }
    private static RectTransform Panel(Transform parent, string name, bool blocks, bool dim = false)
    {
        var rect = Node(parent, name); Stretch(rect);
        if (blocks)
        {
            var img = Ensure<Image>(rect.gameObject);
            img.color = dim ? new Color(.025f, .018f, .065f, .86f) : Ink;
            img.raycastTarget = true;
        }
        return rect;
    }
    private static RectTransform Safe(Transform parent)
    {
        var safe = Node(parent, "SafeArea"); Stretch(safe); Ensure<ReferenceSafeArea>(safe.gameObject); return safe;
    }
    private static RectTransform Design(Transform parent, Vector2 size, float margin = 28)
    {
        var rect = Node(parent, "Design"); Place(rect, Vector2.zero, size);
        var fit = Ensure<ReferencePanelFit>(rect.gameObject); fit.designSize = size; fit.margin = margin;
        return rect;
    }
    private static Material TextMaterial(string name, Color outline)
    {
        string path = Art + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(_font.material); AssetDatabase.CreateAsset(material, path); }
        material.EnableKeyword("OUTLINE_ON"); material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_OutlineColor", outline); material.SetFloat("_OutlineWidth", .22f);
        material.SetColor("_UnderlayColor", new Color(0, 0, 0, .85f));
        material.SetFloat("_UnderlayOffsetX", .5f); material.SetFloat("_UnderlayOffsetY", -.7f);
        material.SetFloat("_UnderlayDilate", .15f);
        EditorUtility.SetDirty(material);
        return material;
    }
    private static Sprite WallBody()
    {
        string path = Art + "WallBody.asset";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            var wall = Sprite("Wall");
            sprite = UnityEngine.Sprite.Create(wall.texture, new Rect(0, 0, wall.texture.width, 120), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "WallBody"; AssetDatabase.CreateAsset(sprite, path);
        }
        return sprite;
    }
    private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
    private static Sprite Wizard() => AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Main/Wizard.png").OfType<Sprite>().FirstOrDefault();
    private static Image Picture(Transform parent, string name, Sprite sprite, Vector2 pos, Vector2 size)
    {
        var rect = Node(parent, name); Place(rect, pos, size);
        var img = Ensure<Image>(rect.gameObject); img.sprite = sprite; img.color = Color.white;
        img.type = sprite != null && sprite.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
        if (img.type == Image.Type.Sliced) img.pixelsPerUnitMultiplier = .5f;
        img.raycastTarget = false;
        return img;
    }
    private static TextMeshProUGUI Label(Transform parent, string name, string value, Vector2 pos, Vector2 size, float fontSize, Color color)
    {
        var rect = Node(parent, name); Place(rect, pos, size);
        var text = Ensure<TextMeshProUGUI>(rect.gameObject);
        text.font = _font; text.fontSharedMaterial = _textMaterial; text.text = value; text.fontSize = fontSize;
        text.enableAutoSizing = true; text.fontSizeMin = fontSize * .6f; text.fontSizeMax = fontSize;
        text.alignment = TextAlignmentOptions.Center; text.color = color; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        var shadow = Ensure<Shadow>(rect.gameObject); shadow.effectColor = new Color(0, 0, 0, .75f); shadow.effectDistance = new Vector2(3, -4);
        return text;
    }
    private static Button Button(Transform parent, string name, string label, string art, Vector2 pos, Vector2 size, UnityAction action, float fontSize = 35)
    {
        var img = Picture(parent, name, Sprite(art), pos, size); img.raycastTarget = true;
        var button = Ensure<Button>(img.gameObject); button.targetGraphic = img;
        // Only deduplicate this builder's action; preserve every custom persistent callback.
        bool found = false;
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            if (button.onClick.GetPersistentTarget(i) == action.Target as UnityEngine.Object && button.onClick.GetPersistentMethodName(i) == action.Method.Name) found = true;
        if (!found) UnityEventTools.AddPersistentListener(button.onClick, action);
        var colors = button.colors; colors.highlightedColor = new Color(1f, .92f, .72f); colors.pressedColor = new Color(.65f, .78f, .9f); button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        if (!string.IsNullOrEmpty(label)) Label(img.transform, "Label", label, Vector2.zero, size - new Vector2(44, 16), fontSize, Cream);
        return button;
    }
    private static void MenuButton(Transform parent, string name, string text, UnityAction action, Color tint)
    {
        var b = Button(parent, name, text, "Banner", Vector2.zero, new Vector2(430, 112), action, 38);
        b.image.color = tint;
        var le = Ensure<LayoutElement>(b.gameObject); le.preferredHeight = le.minHeight = 112;
    }
    private static void IconButton(Transform parent, string name, string icon, Vector2 pos, float size, UnityAction action)
    {
        var b = Button(parent, name, null, "Circle", pos, new Vector2(size, size), action);
        Picture(b.transform, "Icon", Sprite(icon), Vector2.zero, Vector2.one * size * .5f).preserveAspect = true;
    }
    private static void Header(Transform parent, string title, float y)
    {
        var ribbon = Picture(parent, "HeaderRibbon", Sprite("Ribbon"), new Vector2(0, y), new Vector2(800, 155));
        Label(ribbon.transform, "Header", title, Vector2.zero, new Vector2(650, 100), 47, Cream);
    }
    private static Slider Slider(Transform parent, string name, float y, UnityAction<float> action, float initial)
    {
        var rect = Node(parent, name); Place(rect, new Vector2(0, y), new Vector2(580, 64));
        var slider = Ensure<Slider>(rect.gameObject);
        Picture(rect, "Track", Sprite("Banner"), Vector2.zero, new Vector2(580, 28));
        var area = Node(rect, "HandleArea"); Stretch(area); area.offsetMin = new Vector2(25, 0); area.offsetMax = new Vector2(-25, 0);
        var handle = Picture(area, "Handle", Sprite("Circle"), Vector2.zero, new Vector2(62, 62)); handle.raycastTarget = true;
        slider.handleRect = handle.rectTransform; slider.targetGraphic = handle;
        slider.minValue = 0; slider.maxValue = 1; slider.SetValueWithoutNotify(initial);
        bool found = false;
        for (int i = 0; i < slider.onValueChanged.GetPersistentEventCount(); i++)
            if (slider.onValueChanged.GetPersistentTarget(i) == action.Target as UnityEngine.Object && slider.onValueChanged.GetPersistentMethodName(i) == action.Method.Name) found = true;
        if (!found) UnityEventTools.AddPersistentListener(slider.onValueChanged, action);
        return slider;
    }

    [MenuItem("Tools/MagicTouch/UI/Preview Main Menu")] public static void PreviewMain() => Preview("MainMenu");
    [MenuItem("Tools/MagicTouch/UI/Preview Pause")] public static void PreviewPause() => Preview("Pause");
    [MenuItem("Tools/MagicTouch/UI/Preview Game Over")] public static void PreviewGameOver() => Preview("GameOver");
    public static void Preview(string panel)
    {
        if (EditorApplication.isPlaying) return;
        var root = GameObject.Find(RootName);
        if (root == null) return;
        foreach (Transform child in root.transform) child.gameObject.SetActive(child.name == panel);
        Canvas.ForceUpdateCanvases();
        foreach (var fit in root.GetComponentsInChildren<ReferencePanelFit>(false)) fit.Refit();
    }
}
