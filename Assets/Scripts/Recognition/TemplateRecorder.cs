using System.Text;
using UnityEngine;
using MagicTouch.Drawing;

namespace MagicTouch.Recognition
{
    /// <summary>
    /// Utility tool to capture hand/mouse drawn gestures in the Unity Editor,
    /// uniformly normalize them (N = 64 points), and output valid C# code blocks
    /// directly to the Unity Console and Windows Clipboard.
    /// Supports multi-template variants (e.g. var1, var2, var3) with auto-increment.
    /// </summary>
    public class TemplateRecorder : MonoBehaviour
    {
        [Header("Target Gesture Configuration")]
        [Tooltip("Select which symbol you are recording from the reference sheet.")]
        [SerializeField] private GestureSymbol _targetSymbol = GestureSymbol.V;

        [Tooltip("Prefix for the variant name.")]
        [SerializeField] private string _variantPrefix = "var";
        [Tooltip("Current variant index (auto-increments after each successful recording).")]
        [SerializeField] private int _variantIndex = 1;
        [SerializeField] private bool _autoIncrementVariant = true;

        [Header("Hotkeys & Triggers")]
        [SerializeField] private KeyCode _captureKey = KeyCode.Space;
        [Tooltip("Hand index to record from (0 = Hand 1 / Left, 1 = Hand 2 / Right).")]
        [SerializeField] private int _targetHandIndex = 0;

        [Header("Sampling Settings")]
        [Tooltip("High-fidelity resample count (matches PRecognizer.NUM_POINTS = 64).")]
        [SerializeField] private int _targetPoints = 64;
        [SerializeField] private bool _autoRegisterInLiveRecognizer = true;
        [SerializeField] private bool _copyToClipboard = true;

        public GestureSymbol TargetSymbol
        {
            get => _targetSymbol;
            set => _targetSymbol = value;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_captureKey))
            {
                CaptureAndExportTemplate();
            }
        }

        /// <summary>
        /// Captures the most recent gesture stroke, uniformly normalizes it, and outputs multi-template C# code.
        /// </summary>
        [ContextMenu("Capture Last Gesture")]
        public void CaptureAndExportTemplate()
        {
            if (DrawingManager.Instance == null)
            {
                Debug.LogWarning("[TemplateRecorder] DrawingManager instance not found in scene!");
                return;
            }

            Point[] rawPoints = DrawingManager.Instance.GetLastGesturePoints(_targetHandIndex);
            if (rawPoints == null || rawPoints.Length < 4)
            {
                Debug.LogWarning($"[TemplateRecorder] No valid stroke points found for Hand {_targetHandIndex + 1}. Please draw the gesture in the Game View first, then press {_captureKey}!");
                return;
            }

            // Uniform proportional normalization (Resample N=64 -> Uniform Scale max(w,h) -> Translate origin)
            Point[] normalized = PRecognizer.NormalizePointCloud(rawPoints, _targetPoints);

            string currentVariantTag = $"{_variantPrefix}{_variantIndex}";

            // Generate clean, pasteable C# code block
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"// ==========================================================================");
            sb.AppendLine($"// Template: GestureSymbol.{_targetSymbol} | Variant: \"{currentVariantTag}\" ({normalized.Length} Points)");
            sb.AppendLine($"// ==========================================================================");
            sb.AppendLine($"AddPreNormalizedTemplate(GestureSymbol.{_targetSymbol}, new Point[]");
            sb.AppendLine("{");

            for (int i = 0; i < normalized.Length; i++)
            {
                Point p = normalized[i];
                string comma = (i < normalized.Length - 1) ? "," : "";
                sb.AppendLine($"    new Point({p.X:F4}f, {p.Y:F4}f, {p.StrokeID}){comma}");
            }
            sb.AppendLine($"}}, \"{currentVariantTag}\");");

            string formattedCode = sb.ToString();

            // 1. Copy to clipboard
            if (_copyToClipboard)
            {
                GUIUtility.systemCopyBuffer = formattedCode;
            }

            // 2. Register live in memory for instant multi-template nearest neighbor testing
            if (_autoRegisterInLiveRecognizer && DrawingManager.Instance.Recognizer != null)
            {
                DrawingManager.Instance.Recognizer.AddPreNormalizedTemplate(_targetSymbol, normalized, currentVariantTag);
                int totalForSymbol = DrawingManager.Instance.Recognizer.GetTemplateCount(_targetSymbol);
                Debug.Log($"<color=#76FF03><b>[TemplateRecorder]</b></color> Live registered <b>{_targetSymbol}</b> (Variant: <i>{currentVariantTag}</i>). Total variations loaded: {totalForSymbol}.");
            }

            // 3. Print to Console
            Debug.Log($"<color=#00E5FF><b>[TemplateRecorder]</b></color> Successfully recorded <b>{_targetSymbol}</b> (<i>{currentVariantTag}</i>)!\n" +
                      (_copyToClipboard ? "<i>(Code copied to your Clipboard! Ready to Ctrl+V into PRecognizer.cs)</i>\n" : "") +
                      formattedCode);

            // 4. Auto increment variant counter for rapid recording
            if (_autoIncrementVariant)
            {
                _variantIndex++;
            }
        }
    }
}
