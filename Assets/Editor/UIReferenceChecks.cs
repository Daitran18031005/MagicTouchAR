using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MagicTouch.UI;

public static class UIReferenceChecks
{
    [MenuItem("Tools/MagicTouch/UI/Validate UI Bindings")]
    public static void Validate()
    {
        var root = GameObject.Find(UIAutoBuilder.RootName);
        Require(root != null, "UI root is missing");
        var menu = root.GetComponent<MenuManager>();
        Require(menu != null && menu.mainMenuPanel && menu.pausePanel && menu.gameOverPanel && menu.loadingPanel && menu.settingsPanel && menu.creditsPanel && menu.gameplayHUDPanel, "Missing panel binding");
        Require(menu.pauseScoreText && menu.pauseBestText && menu.finalScoreText && menu.highScoreText, "Missing score binding");
        var buttons = root.GetComponentsInChildren<Button>(true);
        foreach (var button in buttons)
        {
            Require(button.onClick.GetPersistentEventCount() > 0, "Unbound button: " + button.name);
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                Require(button.onClick.GetPersistentTarget(i) != null, "Missing OnClick target: " + button.name);
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            Require(text.font != null && text.font.name.Contains("PressStart"), "Non-pixel font: " + text.name);
        foreach (var image in root.GetComponentsInChildren<Image>(true))
            if (image.sprite != null && image.sprite.border.sqrMagnitude > 0)
                Require(image.type == Image.Type.Sliced, "Missing 9-slice: " + image.name);
        Require(menu.bgmSlider.onValueChanged.GetPersistentEventCount() == 1 && menu.sfxSlider.onValueChanged.GetPersistentEventCount() == 1, "Slider callbacks duplicated");
        Debug.Log("[UIReferenceChecks] PASS: " + buttons.Length + " buttons, pixel fonts, panel references and 9-slice sprites.");
    }

    public static void CheckRepeatBuild()
    {
        UIAutoBuilder.Build();
        var root = GameObject.Find(UIAutoBuilder.RootName);
        int count = root.GetComponentsInChildren<Transform>(true).Length;
        int callbacks = root.GetComponentsInChildren<Button>(true).Sum(b => b.onClick.GetPersistentEventCount());
        UIAutoBuilder.Build();
        Require(count == root.GetComponentsInChildren<Transform>(true).Length, "Rebuild duplicated hierarchy");
        Require(callbacks == root.GetComponentsInChildren<Button>(true).Sum(b => b.onClick.GetPersistentEventCount()), "Rebuild duplicated listeners");
        Validate();
        Debug.Log("[UIReferenceChecks] PASS: repeat build preserves object and callback counts.");
    }

    // Render the actual Unity UI at a requested resolution, restoring all scene settings afterward.
    public static void RenderPreview(string panel, int width, int height, string output)
    {
        UIAutoBuilder.Preview(panel);
        var root = GameObject.Find(UIAutoBuilder.RootName);
        var canvas = root.GetComponent<Canvas>();
        var scaler = root.GetComponent<CanvasScaler>();
        var transforms = root.GetComponentsInChildren<Transform>(true);
        var layers = transforms.Select(t => t.gameObject.layer).ToArray();
        RenderMode mode = canvas.renderMode;
        Camera originalCamera = canvas.worldCamera;
        float originalPlane = canvas.planeDistance;
        float originalScale = canvas.scaleFactor;
        bool scalerEnabled = scaler.enabled;
        var cameraObject = new GameObject("UI Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        var texture = new RenderTexture(width, height, 24);
        Texture2D image = null;
        var previous = RenderTexture.active;
        try
        {
            camera.transform.position = new Vector3(0, 0, -100);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .08f, .17f);
            camera.cullingMask = 1 << 30; camera.orthographic = true; camera.orthographicSize = 5;
            camera.targetTexture = texture;
            foreach (var t in transforms) t.gameObject.layer = 30;
            scaler.enabled = false;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
            canvas.scaleFactor = Mathf.Min(width / 1080f, height / 1920f);
            Canvas.ForceUpdateCanvases();
            foreach (var safe in root.GetComponentsInChildren<ReferenceSafeArea>(false))
            { var r = (RectTransform)safe.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }
            foreach (var fit in root.GetComponentsInChildren<ReferencePanelFit>(false))
                fit.Refit();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = texture;
            image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllBytes(output, image.EncodeToPNG());
        }
        finally
        {
            canvas.renderMode = mode; canvas.worldCamera = originalCamera; canvas.planeDistance = originalPlane;
            canvas.scaleFactor = originalScale; scaler.enabled = scalerEnabled;
            for (int i = 0; i < transforms.Length; i++) transforms[i].gameObject.layer = layers[i];
            RenderTexture.active = previous; camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(cameraObject);
            texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            foreach (var safe in root.GetComponentsInChildren<ReferenceSafeArea>(false)) safe.ApplyNow();
            Canvas.ForceUpdateCanvases();
        }
        Debug.Log("[UIReferenceChecks] Preview saved: " + output);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
