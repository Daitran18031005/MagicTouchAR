using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;

namespace MagicTouch.Recognition
{
    /// <summary>
    /// Executes Deep Learning ONNX inference using Unity.Sentis (com.unity.ai.inference).
    /// Features:
    /// 1. Fast physical diagonal check (rejects dots / micro-taps without running CNN).
    /// 2. Heuristic aspect-ratio overrides for pure VerticalLine and HorizontalLine.
    /// 3. Uniform proportional rasterization into 28x28 grayscale tensor.
    /// 4. Strict confidence and margin gating to eliminate false positives.
    /// </summary>
    public class SentisRecognizer : MonoBehaviour
    {
        public static SentisRecognizer Instance { get; private set; }

        [Header("Model & Inference Setup")]
        [Tooltip("The exported magic_touch_model.onnx asset from Assets/Models/.")]
        [SerializeField] private ModelAsset _modelAsset;
        [SerializeField] private BackendType _backendType = BackendType.GPUCompute;

        [Header("Physical Heuristic Overrides")]
        [Tooltip("Minimum bounding box diagonal (in screen pixels) to reject dots and tiny scribbles.")]
        public float minStrokeDiagonal = 35f;

        [Tooltip("Aspect ratio ratio (width/height or height/width) above which the CNN is bypassed for lines.")]
        public float extremeAspectThreshold = 3.5f;

        [Header("Classification Tuning (Anti-False-Positive Filters)")]
        [Tooltip("Minimum Softmax probability (0.0 to 1.0) required to consider a gesture matched.")]
        [Range(0.0f, 1.0f)]
        public float minConfidence = 0.85f;

        [Tooltip("Minimum difference between Top 1 probability and Top 2 probability to reject ambiguous inputs.")]
        [Range(0.0f, 1.0f)]
        public float minMargin = 0.30f;

        [Tooltip("Stroke line thickness for 28x28 rasterization.")]
        [SerializeField] private float _brushRadius = 1.3f;
        [SerializeField] private bool _logDetailedProbabilities = true;

        /// <summary>
        /// Explicit 1:1 mapping from CNN class index to GestureSymbol enum.
        /// Matches the 11 classes in train_magic_touch.py:
        /// 0: V, 1: Caret, 2: HorizontalLine, 3: VerticalLine, 4: O, 5: Z, 6: W, 7: E, 8: X, 9: S, 10: Unknown
        /// </summary>
        public static readonly GestureSymbol[] ClassToSymbol = new GestureSymbol[]
        {
            GestureSymbol.V,              // 0
            GestureSymbol.Caret,          // 1
            GestureSymbol.HorizontalLine, // 2
            GestureSymbol.VerticalLine,   // 3
            GestureSymbol.O,              // 4
            GestureSymbol.Z,              // 5
            GestureSymbol.W,              // 6
            GestureSymbol.E,              // 7
            GestureSymbol.X,              // 8
            GestureSymbol.S,              // 9
            GestureSymbol.Unknown         // 10 (Garbage / Noise / Scribbles)
        };

        private Model _runtimeModel;
        private Worker _worker;

        public bool IsModelLoaded => _worker != null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeSentisWorker();
        }

        private void Start()
        {
            if (_worker == null)
            {
                InitializeSentisWorker();
            }
        }

        private void OnDestroy()
        {
            DisposeSentisWorker();
        }

        private void InitializeSentisWorker()
        {
            if (_modelAsset == null)
            {
                Debug.LogWarning("[SentisRecognizer] ModelAsset is not assigned in Inspector! Please assign magic_touch_model.onnx.");
                return;
            }

            try
            {
                _runtimeModel = ModelLoader.Load(_modelAsset);
                try
                {
                    _worker = new Worker(_runtimeModel, _backendType);
                    Debug.Log($"[SentisRecognizer] Worker initialized successfully using backend: {_backendType}.");
                }
                catch (Exception gpuEx)
                {
                    Debug.LogWarning($"[SentisRecognizer] GPUCompute initialization failed ({gpuEx.Message}). Falling back to CPU backend.");
                    _backendType = BackendType.CPU;
                    _worker = new Worker(_runtimeModel, BackendType.CPU);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SentisRecognizer] Failed to load ONNX model: {ex.Message}");
            }
        }

        private void DisposeSentisWorker()
        {
            if (_worker != null)
            {
                _worker.Dispose();
                _worker = null;
            }
        }

        /// <summary>
        /// Classifies stroke points using physical heuristics first, then CNN inference if needed.
        /// Returns PRecognitionResult with strict confidence and margin gating.
        /// </summary>
        public PRecognitionResult Recognize(IReadOnlyList<Point> points)
        {
            if (points == null || points.Count < 3)
            {
                return PRecognitionResult.NoMatch;
            }

            // =========================================================================
            // Stage 1: Physical Bounds & Fast Rejection
            // =========================================================================
            GesturePreprocessor.StrokeBounds bounds = GesturePreprocessor.CalculateBounds(points);

            // Reject dots, stationary taps, or micro-scribbles
            if (bounds.Diagonal < minStrokeDiagonal)
            {
                if (_logDetailedProbabilities)
                {
                    Debug.Log($"[SentisRecognizer] Rejected: stroke diagonal ({bounds.Diagonal:F1}px) < minStrokeDiagonal ({minStrokeDiagonal}px).");
                }
                return PRecognitionResult.NoMatch;
            }

            // =========================================================================
            // Stage 2: Heuristic Aspect Ratio Overrides
            // =========================================================================
            // If stroke is extremely tall and narrow -> definitely a VerticalLine
            if (GestureCompletionPolicy.IsStraight(points) && bounds.Height > bounds.Width * extremeAspectThreshold)
            {
                if (_logDetailedProbabilities)
                {
                    Debug.Log($"[SentisRecognizer] Heuristic Override: Height ({bounds.Height:F1}px) > Width ({bounds.Width:F1}px) * {extremeAspectThreshold} -> Forcing VerticalLine.");
                }
                return new PRecognitionResult
                {
                    Symbol = GestureSymbol.VerticalLine,
                    MatchedVariant = "Heuristic (Vertical Aspect)",
                    Score = 1.0f,
                    Distance = 0.0f,
                    IsMatch = true
                };
            }

            // If stroke is extremely wide and short -> definitely a HorizontalLine
            if (GestureCompletionPolicy.IsStraight(points) && bounds.Width > bounds.Height * extremeAspectThreshold)
            {
                if (_logDetailedProbabilities)
                {
                    Debug.Log($"[SentisRecognizer] Heuristic Override: Width ({bounds.Width:F1}px) > Height ({bounds.Height:F1}px) * {extremeAspectThreshold} -> Forcing HorizontalLine.");
                }
                return new PRecognitionResult
                {
                    Symbol = GestureSymbol.HorizontalLine,
                    MatchedVariant = "Heuristic (Horizontal Aspect)",
                    Score = 1.0f,
                    Distance = 0.0f,
                    IsMatch = true
                };
            }

            // =========================================================================
            // Stage 3: CNN Model Inference (Sentis)
            // =========================================================================
            if (_worker == null)
            {
                return PRecognitionResult.NoMatch;
            }

            // Step 3.1: Uniform proportional rasterization into 28x28 float array
            float[] inputPixels = GesturePreprocessor.RasterizeTo28x28(points, _brushRadius);
            if (inputPixels == null)
            {
                return PRecognitionResult.NoMatch;
            }

            // Step 3.2: Construct Sentis Tensor<float> [1, 1, 28, 28]
            TensorShape tensorShape = new TensorShape(1, 1, GesturePreprocessor.GRID_SIZE, GesturePreprocessor.GRID_SIZE);
            using var inputTensor = new Tensor<float>(tensorShape, inputPixels);

            // Step 3.3: Schedule worker execution
            _worker.Schedule(inputTensor);

            // Step 3.4: Download output logits to CPU
            Tensor rawOutput = _worker.PeekOutput();
            using var cpuOutput = rawOutput.ReadbackAndClone() as Tensor<float>;

            float[] logits = cpuOutput.DownloadToArray();
            if (logits == null || logits.Length == 0)
            {
                return PRecognitionResult.NoMatch;
            }

            // Step 3.5: Softmax probability calculation
            float maxLogit = float.MinValue;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] > maxLogit) maxLogit = logits[i];
            }

            float sumExp = 0f;
            float[] probs = new float[logits.Length];
            for (int i = 0; i < logits.Length; i++)
            {
                probs[i] = Mathf.Exp(logits[i] - maxLogit);
                sumExp += probs[i];
            }

            // Step 3.6: Find Top 1 and Top 2 for Confidence Margin check
            int top1Index = -1;
            float top1Prob = -1f;
            int top2Index = -1;
            float top2Prob = -1f;

            for (int i = 0; i < probs.Length; i++)
            {
                probs[i] /= sumExp;

                if (probs[i] > top1Prob)
                {
                    top2Prob = top1Prob;
                    top2Index = top1Index;

                    top1Prob = probs[i];
                    top1Index = i;
                }
                else if (probs[i] > top2Prob)
                {
                    top2Prob = probs[i];
                    top2Index = i;
                }
            }

            float margin = top1Prob - Mathf.Max(0f, top2Prob);

            // Step 3.7: Map index to GestureSymbol enum
            GestureSymbol recognizedSymbol = (top1Index >= 0 && top1Index < ClassToSymbol.Length)
                ? ClassToSymbol[top1Index]
                : GestureSymbol.Unknown;

            if (_logDetailedProbabilities)
            {
                string top2Name = (top2Index >= 0 && top2Index < ClassToSymbol.Length) ? ClassToSymbol[top2Index].ToString() : "None";
                Debug.Log($"[SentisRecognizer] Top1: {recognizedSymbol} ({top1Prob:P1}) | Top2: {top2Name} ({top2Prob:P1}) | Margin: {margin:P1}");
            }

            // Step 3.8: Strict Anti-False-Positive Filter Gating
            bool isValidMatch = (top1Prob >= minConfidence) &&
                                (margin >= minMargin) &&
                                (recognizedSymbol != GestureSymbol.Unknown) &&
                                (recognizedSymbol != GestureSymbol.None);

            if (!isValidMatch)
            {
                return PRecognitionResult.NoMatch;
            }

            return new PRecognitionResult
            {
                Symbol = recognizedSymbol,
                MatchedVariant = $"CNN (class {top1Index})",
                Score = top1Prob,
                Distance = 1.0f - top1Prob,
                IsMatch = true
            };
        }
    }
}
