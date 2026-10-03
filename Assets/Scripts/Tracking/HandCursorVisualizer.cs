using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MagicTouch.Tracking
{
    /// <summary>
    /// Visual Debug Cursor (Task 3):
    /// ALWAYS tracks the Index Finger tip coordinates on screen.
    /// Provides crystal-clear visual feedback for pinch detection:
    /// - Red: Open Hand (distance >= pinchThreshold)
    /// - Green: Actively Pinching (distance < pinchThreshold)
    /// - Displays live distance and threshold debug metrics below each fingertip.
    /// </summary>
    public class HandCursorVisualizer : MonoBehaviour
    {
        [Header("Cursor Colors")]
        [SerializeField] private Color _openColor = new Color(1.0f, 0.2f, 0.2f, 0.9f);   // Bright Red
        [SerializeField] private Color _pinchColor = new Color(0.0f, 1.0f, 0.35f, 1.0f); // Bright Green

        [Header("Cursor Sizes")]
        [SerializeField] private float _sizeOpen = 30f;
        [SerializeField] private float _sizePinch = 46f;

        [Header("Debug Metrics")]
        [SerializeField] private bool _showDebugLabel = true;

        private Canvas _cursorCanvas;
        private RectTransform _canvasRect;
        private readonly RectTransform[] _cursorRects = new RectTransform[2];
        private readonly Image[] _cursorImages = new Image[2];
        private readonly TextMeshProUGUI[] _debugLabels = new TextMeshProUGUI[2];

        private void Awake()
        {
            SetupCanvasAndCursors();
        }

        private void SetupCanvasAndCursors()
        {
            // Create an overlay Canvas for cursors (highest sorting order)
            GameObject canvasObj = new GameObject("HandCursorCanvas");
            canvasObj.transform.SetParent(transform);

            _cursorCanvas = canvasObj.AddComponent<Canvas>();
            _cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _cursorCanvas.sortingOrder = 9999; // Topmost, always rendered over everything

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObj.AddComponent<GraphicRaycaster>();
            _canvasRect = canvasObj.GetComponent<RectTransform>();

            // Generate circular texture
            Texture2D circleTex = CreateCircleTexture(64);
            Sprite circleSprite = Sprite.Create(circleTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));

            for (int i = 0; i < 2; i++)
            {
                // Root cursor object
                GameObject cursorObj = new GameObject($"Cursor_Hand_{i + 1}");
                cursorObj.transform.SetParent(canvasObj.transform, false);

                RectTransform rt = cursorObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(_sizeOpen, _sizeOpen);

                // Circle Image
                Image img = cursorObj.AddComponent<Image>();
                img.sprite = circleSprite;
                img.color = _openColor;
                img.raycastTarget = false;

                // High-contrast outline
                Outline outline = cursorObj.AddComponent<Outline>();
                outline.effectColor = Color.black;
                outline.effectDistance = new Vector2(2f, -2f);

                // Debug Metric Label
                GameObject labelObj = new GameObject("DebugMetricLabel");
                labelObj.transform.SetParent(cursorObj.transform, false);

                RectTransform labelRt = labelObj.AddComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0.5f, 0f);
                labelRt.anchorMax = new Vector2(0.5f, 0f);
                labelRt.pivot = new Vector2(0.5f, 1f);
                labelRt.anchoredPosition = new Vector2(0f, -8f);
                labelRt.sizeDelta = new Vector2(240f, 60f);

                TextMeshProUGUI label = labelObj.AddComponent<TextMeshProUGUI>();
                label.fontSize = 18f;
                label.fontStyle = FontStyles.Bold;
                label.alignment = TextAlignmentOptions.Top;
                label.raycastTarget = false;

                // Add dark background shadow to label for readability
                Outline textOutline = labelObj.AddComponent<Outline>();
                textOutline.effectColor = Color.black;
                textOutline.effectDistance = new Vector2(1.5f, -1.5f);

                cursorObj.SetActive(false);

                _cursorRects[i] = rt;
                _cursorImages[i] = img;
                _debugLabels[i] = label;
            }
        }

        private void Update()
        {
            if (HandTrackingBridge.Instance == null) return;

            float threshold = HandTrackingBridge.Instance.pinchThreshold;

            for (int i = 0; i < 2; i++)
            {
                bool isDetected = HandTrackingBridge.Instance.IsHandDetected(i);

                if (!isDetected)
                {
                    if (_cursorRects[i] != null && _cursorRects[i].gameObject.activeSelf)
                    {
                        _cursorRects[i].gameObject.SetActive(false);
                    }
                    continue;
                }

                if (_cursorRects[i] != null && !_cursorRects[i].gameObject.activeSelf)
                {
                    _cursorRects[i].gameObject.SetActive(true);
                }

                Vector2 screenPixelPos = HandTrackingBridge.Instance.GetIndexTipScreenPosition(i);
                bool isPinching = HandTrackingBridge.Instance.IsPinching(i);
                float distance = HandTrackingBridge.Instance.GetPinchDistance(i);

                // Map screen pixel position accurately onto the overlay Canvas
                if (_canvasRect != null && _cursorRects[i] != null)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRect,
                        screenPixelPos,
                        null,
                        out Vector2 localPoint
                    );
                    _cursorRects[i].anchoredPosition = localPoint;

                    // Update size & color based on pinch state (Task 3)
                    float targetSize = isPinching ? _sizePinch : _sizeOpen;
                    _cursorRects[i].sizeDelta = Vector2.Lerp(
                        _cursorRects[i].sizeDelta,
                        new Vector2(targetSize, targetSize),
                        Time.unscaledDeltaTime * 25f
                    );

                    Color targetColor = isPinching ? _pinchColor : _openColor;
                    if (_cursorImages[i] != null)
                    {
                        _cursorImages[i].color = Color.Lerp(
                            _cursorImages[i].color,
                            targetColor,
                            Time.unscaledDeltaTime * 25f
                        );
                    }

                    float enterThresh = HandTrackingBridge.Instance.pinchEnterThreshold;
                    float exitThresh = HandTrackingBridge.Instance.pinchExitThreshold;
                    float rawDist = HandTrackingBridge.Instance.GetRawPinchDistance(i);

                    // Update live debug label
                    if (_showDebugLabel && _debugLabels[i] != null)
                    {
                        if (isPinching)
                        {
                            _debugLabels[i].text = $"<color=#00FF55><b>P{i + 1}: PINCH</b>\nSmooth: {distance:F3} (&gt; {exitThresh:F2} to exit)\nRaw: {rawDist:F3}</color>";
                        }
                        else
                        {
                            _debugLabels[i].text = $"<color=#FF4444><b>P{i + 1}: OPEN</b>\nSmooth: {distance:F3} (&lt; {enterThresh:F2} to enter)\nRaw: {rawDist:F3}</color>";
                        }
                    }
                }
            }
        }

        private Texture2D CreateCircleTexture(int resolution)
        {
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            float radius = resolution * 0.5f;
            Vector2 center = new Vector2(radius, radius);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist <= radius - 2.0f)
                    {
                        tex.SetPixel(x, y, Color.white);
                    }
                    else if (dist <= radius)
                    {
                        float alpha = Mathf.Clamp01(1.0f - (dist - (radius - 2.0f)) / 2.0f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
