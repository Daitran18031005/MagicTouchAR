using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using MagicTouch.Tracking;
using MagicTouch.Recognition;

namespace MagicTouch.Drawing
{
    /// <summary>
    /// Manages real-time drawing trails with multi-stroke support (e.g. 'X') and strict hysteresis logic:
    /// - multiStrokeDelay: Grace period in seconds while fingers are open before finalizing gesture.
    /// - Separate LineRenderers per stroke to prevent connecting lines between disjoint strokes.
    /// - currentTimer decreases each frame fingers are open; inference triggers ONLY when currentTimer <= 0.
    /// - Visual Feedback: Green lines for TRUE match, Red lines for FALSE mismatch, with on-screen HUD badge.
    /// </summary>
    public class DrawingManager : MonoBehaviour
    {
        public static DrawingManager Instance { get; private set; }

        [Header("Input Mode")]
        [Tooltip("If true, simulates drawing with mouse in Editor. If false, fetches Pinch status from HandTrackingBridge.")]
        [SerializeField] private bool _enableEditorSimulation = false;

        [Header("Multi-Stroke Hysteresis Timer")]
        [FormerlySerializedAs("_multiStrokeTimeout")]
        [Tooltip("Time in seconds to wait with fingers open before finalizing gesture (allows drawing multi-stroke shapes like 'X').")]
        public float multiStrokeDelay = 0.35f;

        [Tooltip("Live remaining time before the current gesture finalizes (Player 1).")]
        public float currentTimer = 0f;

        [Header("Tracking Loss Recovery")]
        [Tooltip("Recovery window only when tracking disappears. A visible pinch release ends the segment immediately.")]
        [SerializeField] private float trackingLossGrace = 0.14f;
        private readonly bool[] _trackingUnavailable = new bool[2];
        private readonly bool[] _pinchReleasePending = new bool[2];
        private readonly float[] _trackingLostAt = new float[2];

        [Header("Smart Key-Point Snapping (Tasks 1, 2 & 3)")]
        [Tooltip("Screen space pixel radius for smart key-point magnetic snapping (tight, intentional radius).")]
        public float magneticSnapRadius = 25f;

        [Tooltip("Color of the magnetic snap visual cue ring.")]
        [SerializeField] private Color _snapCueColor = new Color(1.0f, 0.92f, 0.2f, 1.0f); // Bright Gold

        [Header("Visual Configuration")]
        [SerializeField] private float _lineWidth = 0.22f;
        [SerializeField] private Color _colorHand1 = new Color(0.0f, 0.9f, 1.0f, 1.0f); // Neon Cyan (P1)
        [SerializeField] private Color _colorHand2 = new Color(1.0f, 0.1f, 0.6f, 1.0f); // Neon Pink (P2)
        [SerializeField] private Color _successColor = new Color(0.0f, 1.0f, 0.35f, 1.0f); // Neon Green (Match / True)
        [SerializeField] private Color _failureColor = new Color(1.0f, 0.2f, 0.2f, 1.0f);  // Neon Red (Mismatch / False)
        [SerializeField] private Material _trailMaterial;

        [Header("Anti-Jitter & Point Sampling")]
        [FormerlySerializedAs("minDistance")]
        [Tooltip("Minimum screen distance (pixels) between consecutive sampled points to register (set small to prevent skipped points).")]
        [Range(0.5f, 15f)]
        public float minDrawDistance = 2.0f;

        [Header("Adaptive Input Smoothing ($1 Euro / Speed EMA)")]
        [Tooltip("Enable Speed-Based Adaptive EMA for jitter-free slow strokes and zero-lag fast strokes.")]
        public bool enableAdaptiveSmoothing = true;

        [Tooltip("Smoothing alpha when moving slowly (tremor range). Lower = more smoothing.")]
        [Range(0.05f, 0.5f)]
        public float slowSmoothingAlpha = 0.20f;

        [Tooltip("Smoothing alpha when moving fast. Higher = less smoothing / zero lag.")]
        [Range(0.6f, 1.0f)]
        public float fastSmoothingAlpha = 0.95f;

        [Tooltip("Speed in screen pixels/second below which maximum smoothing is applied (tremor range).")]
        public float slowSpeedThreshold = 100f;

        [Tooltip("Speed in screen pixels/second above which minimum smoothing is applied (fast sweep range).")]
        public float fastSpeedThreshold = 700f;

        [Tooltip("Use camera sample timing for adaptive smoothing, with no fixed movement cap.")]
        public bool useSampleTimedSmoothing = true;

        [Tooltip("Keep interpolated rendering inside each sampled segment to prevent corner overshoot.")]
        public bool constrainSplineOvershoot = true;

        private readonly uint[] _lastTrackingSample = new uint[2];
        private readonly float[] _sampleInterval = new float[2] { 1f / 30f, 1f / 30f };
        private readonly Vector2[] _previousRawPosition = new Vector2[2];
        private readonly Vector2[] _filteredPosition = new Vector2[2];

        [Header("Legacy SMA Filter")]
        [Tooltip("Enable Simple Moving Average (SMA) filter to smooth trembling fingers (used if adaptive smoothing is disabled).")]
        public bool enableSMA = true;

        [Tooltip("Number of recent points to average for SMA smoothing (clamped to max 2 for zero latency - Task 4).")]
        [Range(1, 2)]
        public int smaWindowSize = 2;

        [Header("Smooth Spline Curve Rendering (Task 1)")]
        [Tooltip("Enable Catmull-Rom spline curve interpolation for organic, pen-like lines.")]
        [SerializeField] private bool _enableSplineInterpolation = true;

        [Tooltip("Number of interpolated subdivisions between each pair of tracked points (4-8 recommended).")]
        [Range(2, 10)]
        [SerializeField] private int _splineSubdivisions = 5;

        [Tooltip("LineRenderer round cap vertices for smooth pen-like ends.")]
        [SerializeField] private int _lineCapVertices = 8;

        [Tooltip("LineRenderer round corner vertices for smooth joints.")]
        [SerializeField] private int _lineCornerVertices = 8;

        [Header("Recognition Configuration")]
        [Tooltip("Minimum total points across all strokes in a gesture to qualify as valid.")]
        [SerializeField] private int _minGesturePoints = 6;
        [Tooltip("Minimum confidence threshold (0.0 to 1.0) for a valid gesture match.")]
        [SerializeField] private float _recognitionThreshold = 0.60f;
        [Tooltip("Calibrated distance factor for human drawing tolerance (higher = more forgiving).")]
        [SerializeField] private float _distanceNormalizationFactor = 0.85f;

        public enum RecognizerEngine
        {
            SentisNeuralNetwork,
            PointcloudP
        }

        [Header("Recognition Backend")]
        [SerializeField] private RecognizerEngine _engine = RecognizerEngine.SentisNeuralNetwork;
        [SerializeField] private SentisRecognizer _sentisRecognizer;

        private PRecognizer _pRecognizer;

        // Backwards compatibility property
        public float minDistance
        {
            get => minDrawDistance;
            set => minDrawDistance = value;
        }

        public bool EnableEditorSimulation
        {
            get => _enableEditorSimulation;
            set
            {
                _enableEditorSimulation = value;
                if (HandTrackingBridge.Instance != null)
                {
                    HandTrackingBridge.Instance.EnableEditorSimulation = value;
                }
            }
        }

        // Point buffers per hand
        private readonly List<Point>[] _accumulatedPoints = new List<Point>[2] { new List<Point>(), new List<Point>() };
        private readonly List<Point>[] _lastFinalizedPoints = new List<Point>[2] { new List<Point>(), new List<Point>() };

        // Multiple LineRenderers per hand to separate distinct strokes (e.g. line 1 and line 2 of 'X')
        private readonly List<LineRenderer>[] _activeLineRenderers = new List<LineRenderer>[2]
        {
            new List<LineRenderer>(),
            new List<LineRenderer>()
        };

        // State Machine variables per hand (0: Hand 1 / Left, 1: Hand 2 / Right)
        private readonly bool[] _isGestureActive = new bool[2] { false, false };
        private readonly bool[] _wasPinching = new bool[2] { false, false };
        private readonly float[] _currentTimer = new float[2] { 0f, 0f };
        private readonly int[] _currentStrokeId = new int[2] { 0, 0 };
        private readonly Coroutine[] _feedbackCoroutines = new Coroutine[2];
        private int _activeSimulatedMouseHand = -1;
        private readonly bool[] _waitForRelease = new bool[2];
        private readonly int[] _lastTrackId = new int[2];
        private readonly PRecognitionResult[] _pendingResult = new PRecognitionResult[2];
        private readonly bool[] _hasPendingResult = new bool[2];

        // Anti-Flicker Coyote Time Buffer State (Task 1)
        private readonly float[] _pinchDropTimer = new float[2] { 0f, 0f };
        private readonly bool[] _inDropBufferState = new bool[2] { false, false };

        // Allocation-Free Catmull-Rom Spline Buffers
        private readonly List<Vector3> _tempStrokePoints = new List<Vector3>(128);
        private readonly List<Vector3> _tempSplinePoints = new List<Vector3>(512);

        // Smart Key-Point Snapping & Noise Rejection (Tasks 1, 2, 3)
        private readonly List<Vector2> _keyPointsBuffer = new List<Vector2>(32);
        private readonly bool[] _strokeSnapEvaluated = new bool[2] { false, false };
        private readonly bool[] _strokeAwaitingSecondFrame = new bool[2] { false, false };

        // On-screen HUD Result Banner
        private TextMeshProUGUI _feedbackText;
        private CanvasGroup _feedbackCanvasGroup;
        private Coroutine _bannerCoroutine;

        // Events
        public event Action<int, PRecognitionResult> OnGestureRecognized;
        public PRecognizer Recognizer => _pRecognizer;

        public Point[] GetLastGesturePoints(int handIndex = 0)
        {
            if (handIndex >= 0 && handIndex < 2 && _lastFinalizedPoints[handIndex].Count > 0)
                return _lastFinalizedPoints[handIndex].ToArray();
            if (handIndex >= 0 && handIndex < 2 && _accumulatedPoints[handIndex].Count > 0)
                return _accumulatedPoints[handIndex].ToArray();
            return Array.Empty<Point>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            // Machine-local input mode (EditorPrefs / .env) overrides the scene value at runtime only.
            InputMode? mode = InputModeSettings.Override;
            if (mode.HasValue) _enableEditorSimulation = mode.Value == InputMode.MouseSimulation;

            _pRecognizer = new PRecognizer(_recognitionThreshold, _distanceNormalizationFactor);
            if (_sentisRecognizer == null)
            {
                _sentisRecognizer = GetComponent<SentisRecognizer>() ?? FindAnyObjectByType<SentisRecognizer>();
            }

            EnsureFeedbackUI();
        }

        private void Update()
        {
            if (MagicTouch.Gameplay.GameManager.Instance != null &&
                MagicTouch.Gameplay.GameManager.Instance.currentState != MagicTouch.Gameplay.GameState.Playing) return;
            if (_enableEditorSimulation)
            {
                HandleMouseInput();
            }
            else
            {
                HandleARHandInput();
            }

            // Expose Player 1 timer for inspection
            currentTimer = _currentTimer[0];
        }

        #region Input Handling & Multi-Stroke State Machine

        private void HandleMouseInput()
        {
            Vector2 mouseScreen = Input.mousePosition;
            bool isClicking = Input.GetMouseButton(0);

            if (Input.GetMouseButtonDown(0))
                _activeSimulatedMouseHand = (mouseScreen.x < Screen.width * 0.5f) ? 0 : 1;

            int handIndex = (_activeSimulatedMouseHand >= 0)
                ? _activeSimulatedMouseHand
                : ((mouseScreen.x < Screen.width * 0.5f) ? 0 : 1);

            Vector3 worldPos = ScreenToWorld(mouseScreen);

            UpdateHandGestureState(handIndex, isClicking, mouseScreen, worldPos);
            int other = 1 - handIndex;
            UpdateHandGestureState(other, false, Vector2.zero, Vector3.zero);

            if (Input.GetMouseButtonUp(0))
            {
                _activeSimulatedMouseHand = -1;
            }
        }

        private readonly int[] _observedReleases = new int[2];

        private void HandleARHandInput()
        {
            if (HandTrackingBridge.Instance == null) return;

            for (int handIndex = 0; handIndex < 2; handIndex++)
            {
                bool isDetected = HandTrackingBridge.Instance.IsHandDetected(handIndex);
                _trackingUnavailable[handIndex] = !isDetected;
                bool isPinching = isDetected && HandTrackingBridge.Instance.IsPinching(handIndex);
                Vector2 screenPos = HandTrackingBridge.Instance.GetIndexTipScreenPosition(handIndex);
                Vector3 worldPos = HandTrackingBridge.Instance.GetIndexTipWorldPosition(handIndex);
                TrackedHand hand = HandTrackingBridge.Instance.GetHand(handIndex);
                if (_lastTrackId[handIndex] != hand.TrackId)
                {
                    ResetHandGesture(handIndex);
                    _lastTrackId[handIndex] = hand.TrackId;
                    _observedReleases[handIndex] = hand.ReleaseCount;
                }
                if (hand.HasStreamPinch && hand.ReleaseCount != _observedReleases[handIndex])
                {
                    _observedReleases[handIndex] = hand.ReleaseCount;
                    // Preserve the pen-up boundary even when UDP coalesced OPEN then PINCH.
                    _pinchReleasePending[handIndex] = false;
                    UpdateHandGestureState(handIndex, false, screenPos, worldPos, true);
                }
                _pinchReleasePending[handIndex] = hand.IsPinchReleasePending;
                bool freshSample = hand.SampleVersion != _lastTrackingSample[handIndex];
                if (freshSample)
                {
                    _lastTrackingSample[handIndex] = hand.SampleVersion;
                    _sampleInterval[handIndex] = hand.SampleInterval;
                }
                UpdateHandGestureState(handIndex, isPinching, screenPos, worldPos, freshSample);
            }
        }

        /// <summary>
        /// Release ends a segment immediately. A brief missing detection freezes
        /// input and may resume the same segment if position and pinch remain consistent.
        /// </summary>
        private void UpdateHandGestureState(int handIndex, bool isPinching, Vector2 screenPos, Vector3 worldPos, bool freshSample = true)
        {
            // Missing tracking is not a release. Freeze geometry; cancel incomplete input
            // after a bounded outage instead of scoring a gesture the player never finished.
            if (_trackingUnavailable[handIndex])
            {
                if (!_inDropBufferState[handIndex])
                {
                    _inDropBufferState[handIndex] = true;
                    _pinchDropTimer[handIndex] = trackingLossGrace;
                    _trackingLostAt[handIndex] = Time.realtimeSinceStartup;
                    // The preceding frame elapsed before loss was observed. Start the
                    // recovery window now, so a slow frame cannot cancel it immediately.
                    return;
                }
                _pinchDropTimer[handIndex] -= Time.unscaledDeltaTime;
                if (_pinchDropTimer[handIndex] <= 0f && _isGestureActive[handIndex]) ResetHandGesture(handIndex);
                return;
            }
            // No geometry or recognition timer advances on an ambiguous release.
            // A confirmed release still closes the segment before adding any point.
            if (_pinchReleasePending[handIndex]) return;
            if (_inDropBufferState[handIndex])
            {
                if (!freshSample) return;
                _inDropBufferState[handIndex] = false;
                bool timely = Time.realtimeSinceStartup - _trackingLostAt[handIndex] <= trackingLossGrace;
                if (!timely && _isGestureActive[handIndex]) ResetHandGesture(handIndex);
                var points = _accumulatedPoints[handIndex];
                bool resume = timely && isPinching && _wasPinching[handIndex]
                    && !_strokeAwaitingSecondFrame[handIndex] && points.Count > 0;
                if (resume)
                {
                    Point last = points[points.Count - 1];
                    float gap = Vector2.Distance(new Vector2(last.X, last.Y), screenPos);
                    resume = gap <= 90f * Mathf.Max(1, Screen.height) / 1080f;
                }
                if (resume)
                {
                    // Only bridge a short, local dropout of an ongoing pinch. Explicit
                    // release and a far-away reacquisition still start separate segments.
                    ContinueStroke(handIndex, screenPos);
                    return;
                }
                _wasPinching[handIndex] = false;
                _strokeAwaitingSecondFrame[handIndex] = false;
                // Long or spatially discontinuous reacquisition must not bridge the gap.
                if (!isPinching && _isGestureActive[handIndex])
                {
                    PrepareGestureCompletion(handIndex);
                    return;
                }
            }
            if (_waitForRelease[handIndex])
            {
                if (freshSample && !isPinching) _waitForRelease[handIndex] = false;
                return;
            }
            if (isPinching)
            {
                if (!freshSample) return;
                if (_feedbackCoroutines[handIndex] != null)
                {
                    StopCoroutine(_feedbackCoroutines[handIndex]);
                    _feedbackCoroutines[handIndex] = null;
                    ClearAllLines(handIndex);
                }
                if (!_wasPinching[handIndex])
                {
                    _wasPinching[handIndex] = true;
                    _strokeAwaitingSecondFrame[handIndex] = true;
                    return;
                }
                if (_strokeAwaitingSecondFrame[handIndex])
                {
                    _strokeAwaitingSecondFrame[handIndex] = false;
                    if (!_isGestureActive[handIndex])
                    {
                        ClearAllLines(handIndex);
                        _accumulatedPoints[handIndex].Clear();
                        _currentStrokeId[handIndex] = 0;
                        _isGestureActive[handIndex] = true;
                    }
                    else _currentStrokeId[handIndex]++;
                    _hasPendingResult[handIndex] = false;
                    // Shared filtered cursor is authoritative. No start snap or second filter.
                    StartNewStrokeSegment(handIndex, _currentStrokeId[handIndex], screenPos, ScreenToWorld(screenPos));
                }
                else ContinueStroke(handIndex, screenPos);
                return;
            }
            if (_wasPinching[handIndex])
            {
                if (!freshSample) return;
                _wasPinching[handIndex] = false;
                _strokeAwaitingSecondFrame[handIndex] = false;
                if (_isGestureActive[handIndex]) PrepareGestureCompletion(handIndex);
                return; // Never admit the release-frame coordinate into the stroke.
            }
            if (_isGestureActive[handIndex])
            {
                _currentTimer[handIndex] -= Time.unscaledDeltaTime;
                if (_currentTimer[handIndex] <= 0f) CommitGesture(handIndex);
            }
        }

        private PRecognitionResult RecognizePoints(List<Point> points)
        {
            if (points.Count < _minGesturePoints) return PRecognitionResult.NoMatch;
            if (_engine == RecognizerEngine.SentisNeuralNetwork && _sentisRecognizer != null && _sentisRecognizer.IsModelLoaded)
                return _sentisRecognizer.Recognize(points);
            if (_pRecognizer == null) _pRecognizer = new PRecognizer(_recognitionThreshold, _distanceNormalizationFactor);
            return _pRecognizer.Recognize(points.ToArray());
        }

        private void PrepareGestureCompletion(int handIndex)
        {
            List<Point> points = _accumulatedPoints[handIndex];
            var candidate = RecognizePoints(points);
            _pendingResult[handIndex] = candidate;
            _hasPendingResult[handIndex] = true;
            if (_pRecognizer == null) _pRecognizer = new PRecognizer(_recognitionThreshold, _distanceNormalizationFactor);
            var geometry = points.Count >= _minGesturePoints ? _pRecognizer.Recognize(points.ToArray()) : PRecognitionResult.NoMatch;
            if (GestureCompletionPolicy.CanCommit(points, candidate, geometry)) CommitGesture(handIndex);
            else _currentTimer[handIndex] = Mathf.Clamp(multiStrokeDelay, 0.20f, 0.45f);
        }

        private void CommitGesture(int handIndex)
        {
            _isGestureActive[handIndex] = false;
            _currentTimer[handIndex] = 0f;
            FinalizeAndRecognize(handIndex);
        }

        private void ResetHandGesture(int handIndex)
        {
            _pinchReleasePending[handIndex] = false;
            if (_feedbackCoroutines[handIndex] != null) StopCoroutine(_feedbackCoroutines[handIndex]);
            _feedbackCoroutines[handIndex] = null;
            ClearAllLines(handIndex);
            _accumulatedPoints[handIndex].Clear();
            _lastFinalizedPoints[handIndex].Clear();
            _isGestureActive[handIndex] = _wasPinching[handIndex] = _hasPendingResult[handIndex] = false;
            _currentStrokeId[handIndex] = 0;
            _currentTimer[handIndex] = 0f;
        }

        public void ResetForNewRound()
        {
            for (int i=0;i<2;i++)
            {
                ResetHandGesture(i);
                _trackingUnavailable[i] = false;
                // A held pinch over the restart button must not start a stroke in the next round.
                _waitForRelease[i] = true;
            }
            _activeSimulatedMouseHand = -1;
            if (_bannerCoroutine != null) StopCoroutine(_bannerCoroutine);
            _bannerCoroutine = null;
            if (_feedbackText != null) _feedbackText.gameObject.SetActive(false);
            currentTimer = 0f;
        }

        private float GetPinchDropBuffer(int handIndex)
        {
            return _trackingUnavailable[handIndex]
                ? trackingLossGrace : 0f;
        }

        private Vector3 ScreenToWorld(Vector2 screenPixelPos)
        {
            if (HandTrackingBridge.Instance != null)
                return HandTrackingBridge.Instance.ScreenToWorldSpace(screenPixelPos);
            Camera cam = Camera.main;
            if (cam == null) return Vector3.zero;
            return cam.ScreenToWorldPoint(new Vector3(screenPixelPos.x, screenPixelPos.y, 8f));
        }

        /// <summary>
        /// Task 1: Smart Key-Point Snapping.
        /// Replaces greedy path scanning with key-point extraction:
        /// For each previous stroke, only extracts and checks:
        /// - Start Point (0%)
        /// - 3 evenly spaced Midpoints (25%, 50%, 75%)
        /// - End Point (100%)
        /// Snaps ONLY to these intentional landmark points within the tighter magneticSnapRadius.
        /// </summary>
        private bool TryGetMagneticSnapPoint(int handIndex, Vector2 screenPos, out Vector2 snappedScreenPos)
        {
            snappedScreenPos = screenPos;
            if (handIndex < 0 || handIndex >= 2) return false;

            List<Point> points = _accumulatedPoints[handIndex];
            if (points == null || points.Count == 0) return false;

            // Extract intentional Key-Points from all previous strokes
            _keyPointsBuffer.Clear();
            ExtractStrokeKeyPoints(points, _keyPointsBuffer);

            if (_keyPointsBuffer.Count == 0) return false;

            float minDistanceSq = magneticSnapRadius * magneticSnapRadius;
            bool found = false;
            Vector2 closest = screenPos;

            for (int i = 0; i < _keyPointsBuffer.Count; i++)
            {
                Vector2 keyPt = _keyPointsBuffer[i];
                float distSq = (keyPt - screenPos).sqrMagnitude;
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closest = keyPt;
                    found = true;
                }
            }

            if (found)
            {
                snappedScreenPos = closest;
                return true;
            }

            return false;
        }

        private void ExtractStrokeKeyPoints(List<Point> points, List<Vector2> output)
        {
            if (points == null || points.Count == 0) return;

            int startIndex = 0;
            while (startIndex < points.Count)
            {
                int currentStroke = points[startIndex].StrokeID;
                int endIndex = startIndex;

                while (endIndex + 1 < points.Count && points[endIndex + 1].StrokeID == currentStroke)
                {
                    endIndex++;
                }

                // Measure arc length once; sample density must not move the landmarks.
                float totalLength = 0f;
                for (int i = startIndex; i < endIndex; i++)
                {
                    totalLength += Vector2.Distance(
                        new Vector2(points[i].X, points[i].Y),
                        new Vector2(points[i + 1].X, points[i + 1].Y));
                }

                Vector2 start = new Vector2(points[startIndex].X, points[startIndex].Y);
                output.Add(start);
                int segmentIndex = startIndex;
                float traversedLength = 0f;
                for (int quarter = 1; quarter <= 3; quarter++)
                {
                    float targetLength = totalLength * (quarter * 0.25f);
                    Vector2 keyPoint = start;
                    while (segmentIndex < endIndex)
                    {
                        Vector2 a = new Vector2(points[segmentIndex].X, points[segmentIndex].Y);
                        Vector2 b = new Vector2(points[segmentIndex + 1].X, points[segmentIndex + 1].Y);
                        float segmentLength = Vector2.Distance(a, b);
                        keyPoint = b;
                        if (segmentLength > 0f && traversedLength + segmentLength >= targetLength)
                        {
                            keyPoint = Vector2.Lerp(a, b, (targetLength - traversedLength) / segmentLength);
                            break;
                        }
                        traversedLength += segmentLength;
                        segmentIndex++;
                    }
                    // Degenerate strokes safely contribute coincident landmarks.
                    output.Add(keyPoint);
                }
                output.Add(new Vector2(points[endIndex].X, points[endIndex].Y));

                startIndex = endIndex + 1;
            }
        }

        /// <summary>
        /// Task 3: Instant Snap Visual Feedback.
        /// Spawns an expanding luminous ring at the exact snapped world coordinate.
        /// Procedural 24-segment LineRenderer ring, zero external asset dependencies.
        /// </summary>
        private void TriggerSnapFeedback(Vector3 worldPos, int handIndex)
        {
            StartCoroutine(SnapFeedbackRoutine(worldPos, handIndex));
        }

        private IEnumerator SnapFeedbackRoutine(Vector3 worldPos, int handIndex)
        {
            GameObject ringObj = new GameObject($"SnapFeedback_Hand_{handIndex + 1}");
            ringObj.transform.position = worldPos;

            LineRenderer lr = ringObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            int segments = 24;
            lr.positionCount = segments;
            float baseRadius = 0.28f;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * baseRadius, Mathf.Sin(angle) * baseRadius, 0f));
            }

            lr.startWidth = 0.08f;
            lr.endWidth = 0.08f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.alignment = LineAlignment.View;
            lr.sortingLayerName = "Default";
            lr.sortingOrder = 150; // In front of drawing trails (100)

            if (_trailMaterial != null)
                lr.material = new Material(_trailMaterial);
            else
                lr.material = new Material(Shader.Find("Sprites/Default"));

            Color startCol = _snapCueColor;
            float duration = 0.22f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.6f, 1.6f, t);
                ringObj.transform.localScale = new Vector3(scale, scale, 1f);

                Color c = startCol;
                c.a = Mathf.Lerp(1.0f, 0.0f, t);
                lr.startColor = c;
                lr.endColor = c;

                yield return null;
            }

            if (lr != null && lr.material != null)
            {
                Destroy(lr.material);
            }
            Destroy(ringObj);
        }

        #endregion

        #region Stroke Management & LineRenderer Handling

        /// <summary>
        /// Creates a separate LineRenderer for each individual stroke segment.
        /// Ensures no connecting line connects the end of stroke 1 to the start of stroke 2.
        /// </summary>
        private void StartNewStrokeSegment(int handIndex, int strokeId, Vector2 screenPos, Vector3 worldPos)
        {
            _previousRawPosition[handIndex] = screenPos;
            _filteredPosition[handIndex] = screenPos;
            _accumulatedPoints[handIndex].Add(new Point(screenPos.x, screenPos.y, strokeId));

            LineRenderer lr = CreateStrokeLineRenderer(handIndex, strokeId);
            lr.positionCount = 1;
            lr.SetPosition(0, worldPos);
        }

        private void ContinueStroke(int handIndex, Vector2 screenPos)
        {
            if (handIndex < 0 || handIndex >= 2) return;

            List<Point> points = _accumulatedPoints[handIndex];
            if (points == null || points.Count == 0) return;

            Point lastPoint = points[points.Count - 1];
            Vector2 lastPos = new Vector2(lastPoint.X, lastPoint.Y);

            // Filter by minDrawDistance (2.0px)
            if (!useSampleTimedSmoothing && Vector2.Distance(lastPos, screenPos) <= minDrawDistance)
            {
                return;
            }

            // Task 2: Noise Rejection & Spike Clamping for erratic single-frame jumps (> 100 pixels)
            if (!useSampleTimedSmoothing && Vector2.Distance(lastPos, screenPos) > 100f)
            {
                screenPos = Vector2.MoveTowards(lastPos, screenPos, 50f);
            }

            int strokeId = _currentStrokeId[handIndex];

            // Task 2: Adaptive Input Smoothing ($1 Euro / Speed EMA)
            Vector2 smoothedScreenPos;
            if (!_enableEditorSimulation && HandTrackingBridge.Instance != null)
            {
                smoothedScreenPos = screenPos; // Already filtered once in the bridge, same as cursor.
            }
            else if (enableAdaptiveSmoothing)
            {
                if (useSampleTimedSmoothing)
                {
                    float dt = _enableEditorSimulation ? Time.unscaledDeltaTime : _sampleInterval[handIndex];
                    smoothedScreenPos = CalculateSampleTimedSmoothing(
                        _filteredPosition[handIndex], _previousRawPosition[handIndex], screenPos, dt);
                    _previousRawPosition[handIndex] = screenPos;
                    _filteredPosition[handIndex] = smoothedScreenPos;
                }
                else
                {
                    smoothedScreenPos = CalculateAdaptiveSmoothing(lastPos, screenPos);
                }
            }
            else
            {
                smoothedScreenPos = CalculateSMA(points, screenPos, strokeId);
            }

            if (Vector2.Distance(lastPos, smoothedScreenPos) <= minDrawDistance) return;

            Vector3 smoothedWorldPos = ScreenToWorld(smoothedScreenPos);

            // Keep raw/adaptive tracked points in existing list for AI recognition model
            points.Add(new Point(smoothedScreenPos.x, smoothedScreenPos.y, strokeId));

            // Task 1: Update the active stroke's LineRenderer with smooth Catmull-Rom spline
            List<LineRenderer> lineList = _activeLineRenderers[handIndex];
            if (lineList.Count > 0)
            {
                LineRenderer activeLr = lineList[lineList.Count - 1];
                if (_enableSplineInterpolation)
                {
                    UpdateSmoothedLineRenderer(activeLr, points, strokeId);
                }
                else
                {
                    int count = activeLr.positionCount;
                    activeLr.positionCount = count + 1;
                    activeLr.SetPosition(count, smoothedWorldPos);
                }
            }
        }

        /// <summary>
        /// Task 2: Adaptive Input Smoothing ($1 Euro / Speed-Based EMA).
        /// Dynamically adjusts smoothing factor based on hand movement speed:
        /// - Slow movements (tremor range): High smoothing dampens jitter and camera noise.
        /// - Fast movements: Low smoothing ensures instantaneous tracking with zero lag.
        /// </summary>
        private Vector2 CalculateAdaptiveSmoothing(Vector2 lastPos, Vector2 currentPos)
        {
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            float distance = Vector2.Distance(lastPos, currentPos);
            float speed = distance / dt; // screen pixels per second

            // Normalize speed to 0..1 range between tremor threshold and fast sweep threshold
            float speedFactor = Mathf.Clamp01((speed - slowSpeedThreshold) / Mathf.Max(fastSpeedThreshold - slowSpeedThreshold, 1f));

            // Interpolate alpha: slowSpeed -> slowSmoothingAlpha (high smoothing), fastSpeed -> fastSmoothingAlpha (low smoothing)
            float dynamicAlpha = Mathf.Lerp(slowSmoothingAlpha, fastSmoothingAlpha, speedFactor);

            return Vector2.Lerp(lastPos, currentPos, dynamicAlpha);
        }

        private Vector2 CalculateSampleTimedSmoothing(Vector2 filtered, Vector2 previousRaw, Vector2 raw, float dt)
        {
            dt = Mathf.Clamp(dt, 0.001f, 0.25f);
            // Express speed at a reference height so portrait resolution doesn't
            // amplify tiny tracking tremors into the fast, almost unfiltered range.
            float pixelScale = 1080f / Mathf.Max(Screen.height, 1);
            float speed = Vector2.Distance(previousRaw, raw) * pixelScale / dt;
            float factor = Mathf.InverseLerp(slowSpeedThreshold, fastSpeedThreshold, speed);
            float alpha = Mathf.Lerp(slowSmoothingAlpha, fastSmoothingAlpha, factor);
            alpha = 1f - Mathf.Pow(1f - Mathf.Clamp01(alpha), dt * 60f);
            return Vector2.Lerp(filtered, raw, alpha);
        }

        /// <summary>
        /// Task 1: Catmull-Rom Spline Curve Interpolation for LineRenderer.
        /// Generates an organic, pen-like smooth curve passing through all control points.
        /// </summary>
        private void UpdateSmoothedLineRenderer(LineRenderer lr, List<Point> points, int strokeId)
        {
            if (lr == null || points == null || points.Count == 0) return;

            // Collect all points belonging to this stroke ID
            _tempStrokePoints.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i].StrokeID == strokeId)
                {
                    _tempStrokePoints.Add(ScreenToWorld(new Vector2(points[i].X, points[i].Y)));
                }
            }

            int count = _tempStrokePoints.Count;
            if (count == 0) return;

            if (count < 3 || _splineSubdivisions <= 1)
            {
                // With 1 or 2 points, render linear segment
                lr.positionCount = count;
                for (int i = 0; i < count; i++)
                {
                    lr.SetPosition(i, _tempStrokePoints[i]);
                }
                return;
            }

            // Generate smooth spline curve
            _tempSplinePoints.Clear();
            GenerateCatmullRomSpline(_tempStrokePoints, _splineSubdivisions, _tempSplinePoints);

            int splineCount = _tempSplinePoints.Count;
            lr.positionCount = splineCount;
            for (int i = 0; i < splineCount; i++)
            {
                lr.SetPosition(i, _tempSplinePoints[i]);
            }
        }

        private void GenerateCatmullRomSpline(List<Vector3> controlPoints, int subdivisions, List<Vector3> output)
        {
            int n = controlPoints.Count;
            if (n < 2)
            {
                if (n == 1) output.Add(controlPoints[0]);
                return;
            }

            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = (i == 0) ? (2f * controlPoints[0] - controlPoints[1]) : controlPoints[i - 1];
                Vector3 p1 = controlPoints[i];
                Vector3 p2 = controlPoints[i + 1];
                Vector3 p3 = (i + 2 < n) ? controlPoints[i + 2] : (2f * controlPoints[n - 1] - controlPoints[n - 2]);

                for (int j = 0; j < subdivisions; j++)
                {
                    float t = (float)j / subdivisions;
                    Vector3 interpolated = EvaluateCatmullRom(p0, p1, p2, p3, t);
                    if (constrainSplineOvershoot)
                    {
                        // Keep the spline's easing but project onto the measured
                        // segment: no invented hooks or loops at sharp corners.
                        Vector3 segment = p2 - p1;
                        float lengthSq = segment.sqrMagnitude;
                        float along = lengthSq > 0f
                            ? Mathf.Clamp01(Vector3.Dot(interpolated - p1, segment) / lengthSq) : 0f;
                        interpolated = p1 + segment * along;
                    }
                    output.Add(interpolated);
                }
            }

            // Add final control point to terminate curve cleanly
            output.Add(controlPoints[n - 1]);
        }

        private static Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        private Vector2 CalculateSMA(List<Point> points, Vector2 currentPoint, int strokeId)
        {
            if (!enableSMA || points == null || points.Count == 0) return currentPoint;

            float sumX = currentPoint.x;
            float sumY = currentPoint.y;
            int count = 1;
            int effectiveWindow = Mathf.Min(smaWindowSize, 2); // Task 4: Strict maximum of 2 frames to eliminate trailing latency

            // Only average with points belonging to the CURRENT stroke ID
            for (int i = points.Count - 1; i >= 0 && count < effectiveWindow; i--)
            {
                if (points[i].StrokeID == strokeId)
                {
                    sumX += points[i].X;
                    sumY += points[i].Y;
                    count++;
                }
                else
                {
                    break; // Do not blend across stroke boundaries
                }
            }

            return new Vector2(sumX / count, sumY / count);
        }

        private LineRenderer CreateStrokeLineRenderer(int handIndex, int strokeId)
        {
            GameObject lineObj = new GameObject($"DrawingTrail_Hand_{handIndex + 1}_Stroke_{strokeId}");
            lineObj.transform.SetParent(transform);

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            lr.startWidth = _lineWidth;
            lr.endWidth = _lineWidth * 0.85f;
            lr.numCapVertices = _lineCapVertices;
            lr.numCornerVertices = _lineCornerVertices;
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View; // Always face the camera
            lr.sortingLayerName = "Default";
            lr.sortingOrder = 100; // In front of balloons (0) and background (-100)
            lr.positionCount = 0;

            if (_trailMaterial != null)
            {
                lr.material = _trailMaterial;
            }
            else
            {
                lr.material = new Material(Shader.Find("Sprites/Default"));
            }

            Color baseColor = (handIndex == 0) ? _colorHand1 : _colorHand2;
            lr.startColor = baseColor;
            lr.endColor = baseColor;

            _activeLineRenderers[handIndex].Add(lr);
            return lr;
        }

        private void ClearAllLines(int handIndex)
        {
            if (handIndex < 0 || handIndex >= 2) return;
            _inDropBufferState[handIndex] = false;
            _pinchDropTimer[handIndex] = 0f;
            _strokeSnapEvaluated[handIndex] = false;
            _strokeAwaitingSecondFrame[handIndex] = false;
            List<LineRenderer> lines = _activeLineRenderers[handIndex];
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] != null)
                {
                    // Editor validators drive this outside Play Mode, where Destroy is not allowed.
                    if (Application.isPlaying) Destroy(lines[i].gameObject);
                    else DestroyImmediate(lines[i].gameObject);
                }
            }
            lines.Clear();
        }

        #endregion

        #region Recognition & Visual Feedback

        private void FinalizeAndRecognize(int handIndex)
        {
            if (handIndex < 0 || handIndex >= 2) return;

            List<Point> points = _accumulatedPoints[handIndex];

            // If user just tapped or stroke is too short, clear lines immediately
            if (points == null || points.Count < _minGesturePoints)
            {
                ClearAllLines(handIndex);
                points?.Clear();
                return;
            }

            _lastFinalizedPoints[handIndex] = new List<Point>(points);

            PRecognitionResult result = _hasPendingResult[handIndex]
                ? _pendingResult[handIndex] : RecognizePoints(points);
            _hasPendingResult[handIndex] = false;

            // Revalidate cached and fallback results too: timeout must not bypass structure.
            if (result.IsMatch && !GestureCompletionPolicy.HasRequiredStructure(points, result.Symbol))
                result = PRecognitionResult.NoMatch;

            Debug.Log($"[DrawingManager] Hand {handIndex + 1} recognized: '{result.Symbol}' (Score: {result.Score:F2}, Match: {result.IsMatch})");

            // Notify GameManager to pop matching balloons
            OnGestureRecognized?.Invoke(handIndex, result);

            // Show on-screen result badge (e.g. "✓ X (TRUE - 94%)" or "✗ MISMATCH (FALSE)")
            ShowResultBanner(result);

            // Keep lines visible with feedback color (Green for match, Red for mismatch) and fade out
            Color feedbackColor = result.IsMatch ? _successColor : _failureColor;
            if (_feedbackCoroutines[handIndex] != null) StopCoroutine(_feedbackCoroutines[handIndex]);
            _feedbackCoroutines[handIndex] = StartCoroutine(FeedbackRoutine(handIndex, feedbackColor, result.IsMatch));
        }

        private IEnumerator FeedbackRoutine(int handIndex, Color feedbackColor, bool isMatch)
        {
            List<LineRenderer> lines = _activeLineRenderers[handIndex];
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i] != null)
                {
                    lines[i].startColor = feedbackColor;
                    lines[i].endColor = feedbackColor;
                }
            }

            float holdDuration = isMatch ? 0.40f : 0.50f;
            yield return new WaitForSeconds(holdDuration);

            float fadeDuration = 0.25f;
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(1.0f - (elapsed / fadeDuration));
                Color faded = new Color(feedbackColor.r, feedbackColor.g, feedbackColor.b, alpha);

                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i] != null)
                    {
                        lines[i].startColor = faded;
                        lines[i].endColor = faded;
                    }
                }
                yield return null;
            }

            ClearAllLines(handIndex);
            _accumulatedPoints[handIndex].Clear();
            _feedbackCoroutines[handIndex] = null;
        }

        #endregion

        #region HUD Feedback UI Indicator

        private void ShowResultBanner(PRecognitionResult result)
        {
            EnsureFeedbackUI();
            if (_feedbackText == null) return;

            if (_bannerCoroutine != null) StopCoroutine(_bannerCoroutine);

            string symbolStr = result.Symbol.ToString().ToUpper();
            if (result.IsMatch)
                _feedbackText.text = $"<color=#00FF66>{symbolStr} (TRUE - {result.Score * 100:F0}%)</color>";
            else
                _feedbackText.text = "<color=#FF3344>MISMATCH (FALSE)</color>";

            _feedbackText.gameObject.SetActive(true);
            _bannerCoroutine = StartCoroutine(BannerFadeRoutine());
        }

        private IEnumerator BannerFadeRoutine()
        {
            if (_feedbackCanvasGroup != null) _feedbackCanvasGroup.alpha = 1.0f;
            yield return new WaitForSeconds(0.65f);

            float fadeDuration = 0.35f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                if (_feedbackCanvasGroup != null)
                    _feedbackCanvasGroup.alpha = Mathf.Clamp01(1.0f - (elapsed / fadeDuration));
                yield return null;
            }

            if (_feedbackText != null) _feedbackText.gameObject.SetActive(false);
            _bannerCoroutine = null;
        }

        private void EnsureFeedbackUI()
        {
            if (_feedbackText != null) return;

            GameObject existing = GameObject.Find("GestureFeedbackText");
            if (existing != null)
            {
                _feedbackText = existing.GetComponent<TextMeshProUGUI>();
                _feedbackCanvasGroup = existing.GetComponent<CanvasGroup>() ?? existing.AddComponent<CanvasGroup>();
                return;
            }

            GameObject canvasObj = GameObject.Find("GameUI_Canvas");
            if (canvasObj == null)
            {
                Canvas anyCanvas = FindAnyObjectByType<Canvas>();
                if (anyCanvas != null) canvasObj = anyCanvas.gameObject;
            }

            if (canvasObj != null)
            {
                GameObject bannerObj = new GameObject("GestureFeedbackText");
                bannerObj.transform.SetParent(canvasObj.transform, false);

                RectTransform rt = bannerObj.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.72f);
                rt.anchorMax = new Vector2(0.5f, 0.72f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(700, 70);

                _feedbackText = bannerObj.AddComponent<TextMeshProUGUI>();
                _feedbackText.fontSize = 36;
                _feedbackText.fontStyle = FontStyles.Bold;
                _feedbackText.alignment = TextAlignmentOptions.Center;
                _feedbackText.raycastTarget = false;

                _feedbackCanvasGroup = bannerObj.AddComponent<CanvasGroup>();
                bannerObj.SetActive(false);
            }
        }

        #endregion

        private void OnDestroy()
        {
            for (int i = 0; i < 2; i++)
            {
                ClearAllLines(i);
            }
            if (Instance == this) Instance = null;
        }
    }
}
