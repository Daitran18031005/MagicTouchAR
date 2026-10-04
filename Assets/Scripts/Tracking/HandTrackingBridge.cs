using System;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using MagicTouch.Gameplay;
using Debug = UnityEngine.Debug;

namespace MagicTouch.Tracking
{
    /// <summary>
    /// VisionController Layer: Bridges MediaPipe hand tracking to Unity Screen & World space.
    /// Runs a dedicated background Thread for UDP reception (zero main-thread blocking).
    /// Tracks Thumb Tip (Landmark 4) and Index Tip (Landmark 8) for up to 2 hands.
    /// Enforces strict Pinch logic: IsPinching is true ONLY if distance < pinchThreshold.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class HandTrackingBridge : MonoBehaviour
    {
        public static HandTrackingBridge Instance { get; private set; }

        public const int LANDMARK_WRIST = 0;
        public const int LANDMARK_THUMB_TIP = 4;
        public const int LANDMARK_INDEX_TIP = 8;
        public const int LANDMARK_MIDDLE_TIP = 12;

        [Header("Camera & Coordinate Conversion")]
        [SerializeField] private UnityEngine.Camera _worldCamera;
        [SerializeField] private float _worldZDepth = 8f; // Puts line at z = -2f when camera is at z = -10f (in front of balloons at z = 0f)
        [SerializeField] private bool _mirrorHorizontal = true;
        [Tooltip("Optional reference to the CameraBackground RawImage RectTransform for aspect fit.")]
        [SerializeField] private RectTransform _cameraDisplayRect;

        [Header("Dual-Threshold Schmitt Trigger (Pinch Hysteresis)")]
        [Tooltip("Distance required to START a pinch (Enter Threshold).")]
        public float pinchEnterThreshold = 0.04f;

        [Tooltip("Distance required to BREAK a pinch (Exit Threshold).")]
        public float pinchExitThreshold = 0.08f;

        [Tooltip("EMA smoothing factor (0.75f = 75% new raw, 25% previous smoothed for instant zero-latency touch).")]
        [Range(0.05f, 1.0f)]
        public float distanceSmoothingFactor = 0.75f;

        [Header("Python Auto-Launcher (Stealth Mode - Task 1)")]
        [Tooltip("Automatically launches the Python tracking script in completely hidden stealth mode on startup.")]
        [SerializeField] private bool _autoLaunchPythonStreamer = false;
        [SerializeField] private string _pythonExecutable = "pythonw";
        [Tooltip("Use the optional Lite palm detector while retaining the original precise finger-landmark model.")]
        [SerializeField] private bool _useFastPalmDetector = false;
        private static Process _pythonProcess;
        public static bool IsPythonStreamerRunning => _pythonProcess != null && !_pythonProcess.HasExited;

        [Header("Shared Cursor / Stroke Filter")]
        public bool useOneEuroFilter = true;
        public bool usePalmRelativeFiltering = true;
        [Min(0.01f)] public float fingertipOffsetCutoff = 0.8f;
        [Min(0f)] public float fingertipOffsetBeta = 0.006f;
        [Min(0.01f)] public float positionMinCutoff = 1.5f;
        [Min(0f)] public float positionBeta = 0.025f;
        [Min(0.01f)] public float positionDerivativeCutoff = 1f;
        [Tooltip("Freeze one isolated fingertip jump (reference-height pixels) if the wrist did not move with it.")]
        public float fingertipSpikeThreshold = 45f;
        private readonly OneEuroPositionFilter[] _positionFilters = { new OneEuroPositionFilter(), new OneEuroPositionFilter() };
        private readonly HandPositionFilter[] _handPositionFilters = { new HandPositionFilter(), new HandPositionFilter() };
        private readonly Vector2[] _lastAcceptedTip = new Vector2[2], _lastWrist = new Vector2[2];
        private readonly bool[] _heldSpike = new bool[2];
        private readonly bool[] _pinchBeforeTrackingLoss = new bool[2];
        private readonly PinchTransitionFilter[] _pinchTransitions = { new PinchTransitionFilter(), new PinchTransitionFilter() };
        private float _lastPacketTime = -10f;
        public bool IsTrackingReady => hasReceivedFirstData && (_enableEditorSimulation || Time.realtimeSinceStartup - _lastPacketTime < 0.5f);

        // Backward-compatible properties
        public float pinchThreshold
        {
            get => pinchEnterThreshold;
            set => pinchEnterThreshold = value;
        }

        public float PinchThreshold
        {
            get => pinchEnterThreshold;
            set => pinchEnterThreshold = value;
        }

        [Header("Pinch Landmarks Configuration")]
        public int primaryPinchLandmark = 8;   // Index Tip
        public int secondaryPinchLandmark = 4; // Thumb Tip

        [Header("UDP Background Receiver (Task 2)")]
        [SerializeField] private bool _enableUdpReceiver = true;
        [SerializeField] private int _udpPort = 9050;

        [Header("Editor Simulation")]
        [SerializeField] private bool _enableEditorSimulation = true;

        public bool EnableEditorSimulation
        {
            get => _enableEditorSimulation;
            set => _enableEditorSimulation = value;
        }

        // Up to 2 tracked hands (0: Hand 1 / Left player, 1: Hand 2 / Right player)
        private readonly TrackedHand[] _hands = new TrackedHand[2]
        {
            new TrackedHand { HandIndex = 0 },
            new TrackedHand { HandIndex = 1 }
        };

        // Events for listeners
        public event Action<int, bool, Vector2> OnPinchStatusChanged;
        public event Action<int, Vector2, Vector3> OnFingerMoved;
        public event Action<int, Vector2, Vector3> OnDrawingStarted;
        public event Action<int, Vector2, Vector3> OnDrawingEnded;

        #region UDP Background Thread Data Structures

        #pragma warning disable CS0649
        [Serializable]
        private struct UdpHandData
        {
            public int index;
            public int trackId;
            public bool pinch;
            public float pinchDistance;
            public float[] landmarks; // 42 floats [x0, y0, ..., x20, y20]
        }

        [Serializable]
        private struct UdpPacket
        {
            public string sessionId;
            public long timestamp;
            public float cameraFps;
            public float trackingFps;
            public float inferenceMs;
            public UdpHandData[] hands;
        }
        #pragma warning restore CS0649

        private struct HandSnapshot
        {
            public bool isDetected;
            public int trackId;
            public long timestamp;
            public Vector2[] landmarks; // 21 Vector2 elements
        }

        private Thread _udpThread;
        private UdpClient _udpClient;
        private volatile bool _isUdpRunning;
        private readonly object _dataLock = new object();
        private bool _hasNewUdpData;

        // Hardware handshake trigger (Task 3)
        private volatile bool hasReceivedFirstData = false;
        public bool HasReceivedFirstData => hasReceivedFirstData;

        [Header("Measured Camera Performance")]
        [SerializeField] private float _cameraFPS;
        [SerializeField] private float _trackingFPS;
        [SerializeField] private float _inferenceMilliseconds;
        private Vector3 _workerPerformance;
        public float CameraFPS => _cameraFPS;
        public float TrackingFPS => _trackingFPS;
        public float InferenceMilliseconds => _inferenceMilliseconds;

        private readonly HandSnapshot[] _workerSnapshots = new HandSnapshot[2]
        {
            new HandSnapshot { landmarks = new Vector2[21] },
            new HandSnapshot { landmarks = new Vector2[21] }
        };

        private readonly HandSnapshot[] _mainSnapshots = new HandSnapshot[2]
        {
            new HandSnapshot { landmarks = new Vector2[21] },
            new HandSnapshot { landmarks = new Vector2[21] }
        };

        public bool IsUdpReceiverRunning => _isUdpRunning;

        #endregion

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Task 2: Maintain 60 FPS and prevent Unity pausing when clicking Python debug window
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            if (_worldCamera == null)
            {
                _worldCamera = UnityEngine.Camera.main;
            }

            if (_cameraDisplayRect == null)
            {
                GameObject camBg = GameObject.Find("CameraBackground");
                if (camBg != null)
                {
                    _cameraDisplayRect = camBg.GetComponent<RectTransform>();
                }
            }

            if (_autoLaunchPythonStreamer)
            {
                StartPythonStreamerStealth();
            }

            StartUdpReceiver();
        }

        private void Start()
        {
            if (_worldCamera == null)
            {
                _worldCamera = UnityEngine.Camera.main;
            }
        }

        private void Update()
        {
#if UNITY_EDITOR
            if (_enableEditorSimulation)
            {
                if (Input.GetMouseButton(0) || Input.anyKeyDown)
                {
                    hasReceivedFirstData = true;
                }
                SimulateHandWithMouse();
                CheckCameraHandshake();
                return;
            }
#endif
            ProcessUdpSnapshots();
            if (Time.realtimeSinceStartup - _lastPacketTime > 0.25f)
                for (int i = 0; i < _hands.Length; i++) SetHandLost(i);
            CheckCameraHandshake();
        }

        private void CheckCameraHandshake()
        {
            if (IsTrackingReady && GameManager.Instance != null && GameManager.Instance.currentState == GameState.WaitingForCamera)
            {
                GameManager.Instance.StartGame();
            }
        }

        private void OnDestroy()
        {
            StopUdpReceiver();
            TerminatePythonProcess();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            StopUdpReceiver();
            TerminatePythonProcess();
        }

        #region Python Stealth Process Launcher (Task 1)

        public void StartPythonStreamerStealth()
        {
            if (_pythonProcess != null && !_pythonProcess.HasExited) return;
            if (MediaPipeUDPReceiver.IsPythonTrackerRunning) return;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptPath = Path.Combine(projectRoot, "Tools", "mediapipe_streamer.py");

            if (!File.Exists(scriptPath)) return;

            var videoReceiver = FindAnyObjectByType<MediaPipeUDPReceiver>();
            string videoArguments = videoReceiver != null && videoReceiver.EnableVideoStream ? " --stream-video" : "";
            string fastModel = Path.Combine(projectRoot, "Tools", "hand_landmarker_fast.task");
            string modelArguments = _useFastPalmDetector && File.Exists(fastModel) ? $" --model \"{fastModel}\"" : "";
            string sessionArguments = " --session-id " + TrackingStreamSession.Begin();

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = string.IsNullOrEmpty(_pythonExecutable) ? "pythonw" : _pythonExecutable,
                    Arguments = $"-u \"{scriptPath}\"{videoArguments}{modelArguments}{sessionArguments}",
                    WorkingDirectory = projectRoot,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                _pythonProcess = Process.Start(psi);
                UnityEngine.Debug.Log($"[HandTrackingBridge] Launched Python Streamer in Stealth Mode (PID: {_pythonProcess?.Id})");
            }
            catch (Exception ex)
            {
                try
                {
                    ProcessStartInfo fallbackPsi = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"-u \"{scriptPath}\"{videoArguments}{modelArguments}{sessionArguments}",
                        WorkingDirectory = projectRoot,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    _pythonProcess = Process.Start(fallbackPsi);
                }
                catch (Exception fallbackEx)
                {
                    UnityEngine.Debug.LogWarning($"[HandTrackingBridge] Python auto-launch notice: {ex.Message} / {fallbackEx.Message}");
                }
            }
        }

        public static void TerminatePythonProcess()
        {
            if (_pythonProcess != null)
            {
                try
                {
                    if (!_pythonProcess.HasExited)
                    {
                        _pythonProcess.Kill();
                        _pythonProcess.WaitForExit(1000);
                    }
                    _pythonProcess.Dispose();
                }
                catch { }
                finally
                {
                    _pythonProcess = null;
                }
            }
        }

        #endregion

        #region UDP Background Receiver Implementation (Task 2)

        private void StartUdpReceiver()
        {
            if (!_enableUdpReceiver || _isUdpRunning) return;

            _isUdpRunning = true;
            _udpThread = new Thread(UdpReceiverWorker)
            {
                IsBackground = true,
                Name = "HandTrackingBridge_UDP_Receiver"
            };
            _udpThread.Start();
            Debug.Log($"[HandTrackingBridge] Background UDP receiver thread started on port {_udpPort}");
        }

        private void StopUdpReceiver()
        {
            _isUdpRunning = false;
            try
            {
                _udpClient?.Close();
                _udpClient = null;
            }
            catch (Exception) { }

            if (_udpThread != null && _udpThread.IsAlive)
            {
                _udpThread.Join(150);
                _udpThread = null;
            }
        }

        private void UdpReceiverWorker()
        {
            try
            {
                _udpClient = new UdpClient();
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, _udpPort));
                _udpClient.Client.ReceiveTimeout = 1000;
                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                while (_isUdpRunning)
                {
                    try
                    {
                        byte[] bytes = _udpClient.Receive(ref remoteEP);

                        // Task 4: Drain the ENTIRE queue to get the absolute latest packet, dropping older stale packets
                        while (_udpClient.Available > 0)
                        {
                            bytes = _udpClient.Receive(ref remoteEP);
                        }

                        if (bytes != null && bytes.Length > 0)
                        {
                            string json = Encoding.UTF8.GetString(bytes);
                            UdpPacket packet = JsonUtility.FromJson<UdpPacket>(json);
                            if (!TrackingStreamSession.AcceptLandmarks(packet.sessionId)) continue;

                            lock (_dataLock)
                            {
                                // Task 3: Flip boolean to true safely upon receiving first valid tracking packet
                                hasReceivedFirstData = true;
                                _workerPerformance = new Vector3(packet.cameraFps, packet.trackingFps, packet.inferenceMs);

                                _workerSnapshots[0].isDetected = false;
                                _workerSnapshots[1].isDetected = false;

                                if (packet.hands != null)
                                {
                                    for (int h = 0; h < packet.hands.Length; h++)
                                    {
                                        int idx = packet.hands[h].index;
                                        if (idx < 0 || idx >= 2) continue;
                                        float[] rawLms = packet.hands[h].landmarks;
                                        if (rawLms == null || rawLms.Length < 42) continue;

                                        _workerSnapshots[idx].isDetected = true;
                                        _workerSnapshots[idx].trackId = packet.hands[h].trackId;
                                        _workerSnapshots[idx].timestamp = packet.timestamp;
                                        for (int i = 0; i < 21; i++)
                                        {
                                            _workerSnapshots[idx].landmarks[i].x = rawLms[i * 2];
                                            _workerSnapshots[idx].landmarks[i].y = rawLms[i * 2 + 1];
                                        }
                                    }
                                }
                                _hasNewUdpData = true;
                            }
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.TimedOut && _isUdpRunning)
                        {
                            Thread.Sleep(2);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isUdpRunning)
                {
                    Debug.LogWarning($"[HandTrackingBridge] UDP receiver thread exception: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Instantly swaps thread-safe landmark data and updates hand states with 0ms blocking.
        /// </summary>
        private void ProcessUdpSnapshots()
        {
            bool hasUpdate = false;
            lock (_dataLock)
            {
                if (_hasNewUdpData)
                {
                    hasUpdate = true;
                    _hasNewUdpData = false;
                    _cameraFPS = _workerPerformance.x;
                    _trackingFPS = _workerPerformance.y;
                    _inferenceMilliseconds = _workerPerformance.z;
                    for (int h = 0; h < 2; h++)
                    {
                        _mainSnapshots[h].isDetected = _workerSnapshots[h].isDetected;
                        _mainSnapshots[h].trackId = _workerSnapshots[h].trackId;
                        _mainSnapshots[h].timestamp = _workerSnapshots[h].timestamp;
                        if (_workerSnapshots[h].isDetected)
                        {
                            Array.Copy(_workerSnapshots[h].landmarks, _mainSnapshots[h].landmarks, 21);
                        }
                    }
                }
            }

            if (!hasUpdate) return;
            _lastPacketTime = Time.realtimeSinceStartup;

            for (int h = 0; h < 2; h++)
            {
                if (_mainSnapshots[h].isDetected)
                {
                    ProcessHandLandmarks(h, _mainSnapshots[h].landmarks, _mainSnapshots[h].timestamp, _mainSnapshots[h].trackId);
                }
                else
                {
                    SetHandLost(h);
                }
            }
        }

        #endregion

        #region Strict Pinch Logic & Landmark Ingestion (Task 2)

        /// <summary>
        /// Feeds 21 normalized landmarks into the tracking system and computes strict pinch logic.
        /// </summary>
        public void ProcessHandLandmarks(int handIndex, Vector2[] landmarks, long timestamp = 0, int trackId = 0)
        {
            if (handIndex < 0 || handIndex >= _hands.Length || landmarks == null || landmarks.Length < 21)
            {
                return;
            }

            TrackedHand hand = _hands[handIndex];
            bool sameTrack = trackId == 0 || hand.TrackId == trackId;
            if (sameTrack && timestamp > 0 && hand.SourceTimestamp > 0 && timestamp <= hand.SourceTimestamp) return;
            bool wasDetected = hand.IsDetected && sameTrack;
            bool shortRecovery = !wasDetected && sameTrack && hand.SampleVersion > 0
                && Time.realtimeSinceStartup - hand.SampleTime <= 0.18f;
            if (!sameTrack) { SetHandLost(handIndex); hand.TrackId = trackId; }
            if (!wasDetected && !shortRecovery) { _positionFilters[handIndex].Reset(); _handPositionFilters[handIndex].Reset(); _heldSpike[handIndex] = false; }
            hand.SampleInterval = wasDetected && timestamp > 0 && hand.SourceTimestamp > 0
                ? Mathf.Clamp((timestamp - hand.SourceTimestamp) * 0.001f, 0.001f, 0.25f)
                : hand.SampleVersion == 0 ? 1f / 30f : Mathf.Clamp(Time.realtimeSinceStartup - hand.SampleTime, 0.001f, 0.25f);
            hand.SourceTimestamp = timestamp;
            _lastPacketTime = Time.realtimeSinceStartup;
            hasReceivedFirstData = true;
            hand.SampleTime = Time.realtimeSinceStartup;
            hand.SampleVersion++;
            hand.IsDetected = true;

            // Extract Thumb (4) and Index (8)
            Vector2 thumbNorm = landmarks[LANDMARK_THUMB_TIP];
            Vector2 indexNorm = landmarks[LANDMARK_INDEX_TIP];

            // Map coordinates to Screen Space
            hand.RawIndexTipScreen = NormalizedToScreenSpace(indexNorm);
            float referenceScale = 1080f / Mathf.Max(1, Screen.height);
            Vector2 tip = hand.RawIndexTipScreen * referenceScale;
            hand.RawWristScreen = NormalizedToScreenSpace(landmarks[LANDMARK_WRIST]);
            Vector2 wrist = hand.RawWristScreen * referenceScale;
            // Reject only a single implausible fingertip jump, never cap sustained motion.
            bool spike = wasDetected && Vector2.Distance(tip, _lastAcceptedTip[handIndex]) >
                Mathf.Max(fingertipSpikeThreshold, Vector2.Distance(wrist, _lastWrist[handIndex]) * 3f);
            bool hold = spike && !_heldSpike[handIndex];
            // Require confirmation only at the beginning of a discontinuity. Sustained
            // fast motion must not be held on every other camera frame.
            _heldSpike[handIndex] = spike;
            // Reject only the suspect finger offset. Still advance with wrist
            // translation, instead of freezing the whole cursor for this sample.
            if (hold) tip = _lastAcceptedTip[handIndex] + (wrist - _lastWrist[handIndex]);
            _lastWrist[handIndex] = wrist;
            _lastAcceptedTip[handIndex] = tip;
            Vector2 position = tip;
            if (useOneEuroFilter)
                position = usePalmRelativeFiltering
                    ? _handPositionFilters[handIndex].Filter(tip, wrist, hand.SampleInterval,
                        positionMinCutoff, positionBeta, positionDerivativeCutoff, fingertipOffsetCutoff, fingertipOffsetBeta)
                    : _positionFilters[handIndex].Filter(tip, hand.SampleInterval,
                        positionMinCutoff, positionBeta, positionDerivativeCutoff);
            hand.IndexTipScreen = position / referenceScale;
            hand.IndexTipWorld = ScreenToWorldSpace(hand.IndexTipScreen);
            hand.ThumbTipScreen = NormalizedToScreenSpace(thumbNorm);
            hand.MiddleTipScreen = NormalizedToScreenSpace(landmarks[LANDMARK_MIDDLE_TIP]);

            // Task 2: Calculate Euclidean distance and apply Exponential Moving Average (EMA) smoothing
            float rawDistance = Vector2.Distance(thumbNorm, indexNorm);
            hand.RawPinchDistance = rawDistance;

            // Task 3: Signal Smoothing with responsive factor (0.75f)
            if (!wasDetected || hand.SmoothedPinchDistance <= 0.0001f)
            {
                hand.SmoothedPinchDistance = rawDistance;
            }
            else
            {
                hand.SmoothedPinchDistance = Mathf.Lerp(hand.SmoothedPinchDistance, rawDistance, distanceSmoothingFactor);
            }
            hand.PinchDistance = hand.SmoothedPinchDistance;

            bool wasPinching = hand.IsPinching;
            var transition = _pinchTransitions[handIndex];
            if (!wasDetected) transition.Reset(shortRecovery && _pinchBeforeTrackingLoss[handIndex]);
            transition.Update(rawDistance, pinchEnterThreshold, pinchExitThreshold);
            bool isPinching = transition.IsPinching;
            hand.IsPinchReleasePending = transition.IsReleasePending;
            if (isPinching != wasPinching) hand.SmoothedPinchDistance = rawDistance;
            hand.IsPinching = isPinching;

            if (isPinching != wasPinching)
            {
                OnPinchStatusChanged?.Invoke(handIndex, isPinching, hand.IndexTipScreen);
            }

            UpdateDrawingState(hand, isPinching && !hand.IsPinchReleasePending);
            OnFingerMoved?.Invoke(handIndex, hand.IndexTipScreen, hand.IndexTipWorld);
        }

        public void SetHandLost(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hands.Length) return;

            TrackedHand hand = _hands[handIndex];
            if (hand.IsDetected)
            {
                _pinchBeforeTrackingLoss[handIndex] = hand.IsPinching && !hand.IsPinchReleasePending;
                if (hand.IsDrawing)
                {
                    UpdateDrawingState(hand, false);
                }
                hand.IsPinching = false;
                hand.IsPinchReleasePending = false;
                hand.IsDetected = false;
                hand.SmoothedPinchDistance = 0f;
                hand.RawPinchDistance = 0f;
                OnPinchStatusChanged?.Invoke(handIndex, false, hand.IndexTipScreen);
            }
        }

        private void UpdateDrawingState(TrackedHand hand, bool shouldDraw)
        {
            if (!hand.IsDrawing && shouldDraw)
            {
                hand.IsDrawing = true;
                OnDrawingStarted?.Invoke(hand.HandIndex, hand.IndexTipScreen, hand.IndexTipWorld);
            }
            else if (hand.IsDrawing && !shouldDraw)
            {
                hand.IsDrawing = false;
                OnDrawingEnded?.Invoke(hand.HandIndex, hand.IndexTipScreen, hand.IndexTipWorld);
            }
        }

        #endregion

        #region Public Queries

        public bool IsPinching(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hands.Length) return false;
            return _hands[handIndex].IsDetected && _hands[handIndex].IsPinching;
        }

        public Vector2 GetIndexTipScreenPosition(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hands.Length) return Vector2.zero;
            return _hands[handIndex].IndexTipScreen;
        }

        public Vector3 GetIndexTipWorldPosition(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hands.Length) return Vector3.zero;
            return _hands[handIndex].IndexTipWorld;
        }

        public bool IsHandDetected(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hands.Length) return false;
            return _hands[handIndex].IsDetected;
        }

        public float GetPinchDistance(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hands.Length) return 1.0f;
            return _hands[handIndex].PinchDistance;
        }

        public float GetSmoothedPinchDistance(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hands.Length) return 1.0f;
            return _hands[handIndex].SmoothedPinchDistance;
        }

        public float GetRawPinchDistance(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hands.Length) return 1.0f;
            return _hands[handIndex].RawPinchDistance;
        }

        public TrackedHand GetHand(int index) => (index >= 0 && index < _hands.Length) ? _hands[index] : null;

        #endregion

        #region Coordinate Mapping & Math

        public Vector2 NormalizedToScreenSpace(Vector2 normalizedPos)
        {
            if (_worldCamera == null) _worldCamera = UnityEngine.Camera.main;

            if (_cameraDisplayRect != null && _cameraDisplayRect.rect.width > 10f && _worldCamera != null)
            {
                Rect rect = _cameraDisplayRect.rect;
                bool alreadyMirroredInTransformX = _cameraDisplayRect.lossyScale.x < 0;
                float u = (_mirrorHorizontal && !alreadyMirroredInTransformX)
                    ? (1.0f - normalizedPos.x)
                    : normalizedPos.x;

                bool alreadyMirroredInTransformY = _cameraDisplayRect.lossyScale.y < 0;
                float v = alreadyMirroredInTransformY
                    ? normalizedPos.y
                    : (1.0f - normalizedPos.y);

                Vector3 localPoint = new Vector3(
                    Mathf.Lerp(rect.xMin, rect.xMax, u),
                    Mathf.Lerp(rect.yMin, rect.yMax, v),
                    0f
                );
                Vector3 worldPoint = _cameraDisplayRect.TransformPoint(localPoint);
                Canvas canvas = _cameraDisplayRect.GetComponentInParent<Canvas>();
                UnityEngine.Camera uiCamera = canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : (canvas != null && canvas.worldCamera != null ? canvas.worldCamera : _worldCamera);
                return UnityEngine.RectTransformUtility.WorldToScreenPoint(uiCamera, worldPoint);
            }

            float screenU = _mirrorHorizontal ? (1.0f - normalizedPos.x) : normalizedPos.x;
            float screenV = 1.0f - normalizedPos.y;
            return new Vector2(screenU * Screen.width, screenV * Screen.height);
        }

        public Vector3 ScreenToWorldSpace(Vector2 screenPixelPos)
        {
            if (_worldCamera == null) _worldCamera = UnityEngine.Camera.main;
            if (_worldCamera == null) return Vector3.zero;
            Vector3 screenWithDepth = new Vector3(screenPixelPos.x, screenPixelPos.y, _worldZDepth);
            return _worldCamera.ScreenToWorldPoint(screenWithDepth);
        }

        #endregion

        #region Editor Simulation

        private int _activeSimulatedHandIndex = -1;

        private void SimulateHandWithMouse()
        {
            Vector2 mouseScreen = Input.mousePosition;
            bool isClicking = Input.GetMouseButton(0);

            if (Input.GetMouseButtonDown(0))
            {
                _activeSimulatedHandIndex = (mouseScreen.x < Screen.width * 0.5f) ? 0 : 1;
            }

            int simulatedHandIndex = (_activeSimulatedHandIndex >= 0)
                ? _activeSimulatedHandIndex
                : ((mouseScreen.x < Screen.width * 0.5f) ? 0 : 1);

            int otherHandIndex = 1 - simulatedHandIndex;
            _hands[otherHandIndex].IsDetected = false;
            _hands[otherHandIndex].IsPinching = false;

            TrackedHand hand = _hands[simulatedHandIndex];
            hand.IsDetected = true;
            hand.IsPinching = isClicking;
            hand.IsPinchReleasePending = false;
            float simulatedDist = isClicking ? 0.02f : 0.25f;
            hand.RawPinchDistance = simulatedDist;
            hand.SmoothedPinchDistance = simulatedDist;
            hand.PinchDistance = simulatedDist;
            hand.IndexTipScreen = mouseScreen;
            hand.IndexTipWorld = ScreenToWorldSpace(mouseScreen);

            UpdateDrawingState(hand, isClicking);

            if (hand.IsDrawing || Input.GetMouseButtonDown(0) || Input.GetMouseButtonUp(0))
            {
                OnFingerMoved?.Invoke(simulatedHandIndex, hand.IndexTipScreen, hand.IndexTipWorld);
            }

            if (Input.GetMouseButtonUp(0))
            {
                _activeSimulatedHandIndex = -1;
            }
        }

        #endregion
    }
}
