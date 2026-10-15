using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace MagicTouch.Tracking
{
    /// <summary>
    /// Receives live MediaPipe Hand Tracking data and compressed camera frames over UDP.
    /// Ingests 21 normalized landmarks into HandTrackingBridge for Pinch-to-Draw gameplay.
    /// Updates the AR camera background texture with zero-latency loopback streaming.
    /// </summary>
    public class MediaPipeUDPReceiver : MonoBehaviour
    {
        public static MediaPipeUDPReceiver Instance { get; private set; }

        [Serializable]
        public struct HandData
        {
            public int index;
            public int trackId;
            public bool pinch;
            public float pinchDistance;
            public float[] landmarks; // 42 floats: [x0, y0, x1, y1, ..., x20, y20]
        }

        [Serializable]
        public struct MediaPipePacket
        {
            public string sessionId;
            public long timestamp;
            public HandData[] hands;
        }

        [Header("Network Settings")]
        [Tooltip("UDP port for JSON hand landmark packets from Python.")]
        [SerializeField] private int _landmarksPort = 9050;

        [Tooltip("Enable receiving live webcam video frames over UDP from Python.")]
        [SerializeField] private bool _enableVideoStream = true;

        [Tooltip("UDP port for JPEG video stream from Python.")]
        [SerializeField] private int _videoPort = 9051;

        [Header("Display Target")]
        [Tooltip("Optional RawImage to render incoming camera video feed on.")]
        [SerializeField] private RawImage _cameraDisplay;

        [Header("Auto-Launch Tracker")]
        [Tooltip("If true, automatically launches Python MediaPipe tracker when entering Play Mode in Editor.")]
        [SerializeField] private bool _autoLaunchPythonOnPlay = false;

        [Header("Live Connection Status (Read-Only)")]
        [SerializeField] private bool _isConnected;
        [SerializeField] private float _receivedLandmarksFPS;
        [SerializeField] private int _activeHandsCount;
        [SerializeField] private float _lastPacketReceivedTime;

        public bool IsConnected => _isConnected;
        public float ReceivedFPS => _receivedLandmarksFPS;
        public int ActiveHandsCount => _activeHandsCount;
        public bool EnableVideoStream => _enableVideoStream;
        public int VideoPort => _videoPort;

        // Background UDP threading
        private Thread _landmarksThread;
        private Thread _videoThread;
        private UdpClient _landmarksClient;
        private UdpClient _videoClient;
        private volatile bool _isRunning;

        public struct ParsedHandData
        {
            public bool isDetected;
            public int trackId;
            public long timestamp;
            public bool isPinching;
            public float pinchDistance;
            public Vector2[] landmarks;
        }

        // Thread-safe data exchange (Task 2: parsed on background thread, zero main thread allocation)
        private readonly object _landmarkLock = new object();
        private bool _hasNewData;
        private readonly ParsedHandData[] _workerHands = new ParsedHandData[2]
        {
            new ParsedHandData { landmarks = new Vector2[21] },
            new ParsedHandData { landmarks = new Vector2[21] }
        };
        private readonly ParsedHandData[] _mainHands = new ParsedHandData[2]
        {
            new ParsedHandData { landmarks = new Vector2[21] },
            new ParsedHandData { landmarks = new Vector2[21] }
        };

        private readonly object _videoLock = new object();
        private byte[] _pendingVideoBytes;
        private bool _hasNewVideo;
        private float _lastVideoFrameTime;

        // FPS tracking
        private int _packetCounter;
        private float _fpsTimer;

        // Video texture
        private Texture2D _videoTexture;
        private bool[] _handActiveState = new bool[2] { false, false };

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Optional machine-local overrides from .env (Awake runs before OnEnable starts the sockets).
            _landmarksPort = ProjectEnv.GetInt("LANDMARK_UDP_PORT", _landmarksPort);
            _videoPort = ProjectEnv.GetInt("VIDEO_UDP_PORT", _videoPort);

            if (_cameraDisplay == null)
            {
                GameObject camObj = GameObject.Find("CameraBackground");
                if (camObj != null)
                {
                    _cameraDisplay = camObj.GetComponent<RawImage>();
                }
            }
        }

        private void Start()
        {
            if (_autoLaunchPythonOnPlay && Application.isEditor)
            {
                StartPythonTracker();
            }

            StartReceiving();
        }

        private void OnEnable()
        {
            if (!_isRunning)
            {
                StartReceiving();
            }
        }

        private void OnDisable()
        {
            StopReceiving();
        }

        private void OnDestroy()
        {
            StopReceiving();
            TerminateExternalTrackerProcess();
            if (_videoTexture != null)
            {
                Destroy(_videoTexture);
                _videoTexture = null;
            }
        }

        private void OnApplicationQuit()
        {
            StopReceiving();
            TerminateExternalTrackerProcess();
        }

        private void StartReceiving()
        {
            if (_isRunning) return;
            _isRunning = true;

            // Start Landmarks receiver thread (only if HandTrackingBridge is not already running UDP receiver)
            if (HandTrackingBridge.Instance != null && HandTrackingBridge.Instance.IsUdpReceiverRunning)
            {
                Debug.Log($"[MediaPipeUDPReceiver] HandTrackingBridge is already receiving UDP landmarks on port {_landmarksPort}. Skipping duplicate receiver thread.");
            }
            else
            {
                _landmarksThread = new Thread(ReceiveLandmarksWorker)
                {
                    IsBackground = true,
                    Name = "MediaPipe_Landmarks_Receiver"
                };
                _landmarksThread.Start();
            }

            // Start Video receiver thread if enabled
            if (_enableVideoStream)
            {
                _videoThread = new Thread(ReceiveVideoWorker)
                {
                    IsBackground = true,
                    Name = "MediaPipe_Video_Receiver"
                };
                _videoThread.Start();
            }

            Debug.Log($"[MediaPipeUDPReceiver] Listening on UDP Landmarks:{_landmarksPort}, Video:{_videoPort}");
        }

        public void StopReceiving()
        {
            _isRunning = false;

            try
            {
                _landmarksClient?.Close();
                _landmarksClient = null;
            }
            catch (Exception) { }

            try
            {
                _videoClient?.Close();
                _videoClient = null;
            }
            catch (Exception) { }

            if (_landmarksThread != null && _landmarksThread.IsAlive)
            {
                _landmarksThread.Join(200);
                _landmarksThread = null;
            }

            if (_videoThread != null && _videoThread.IsAlive)
            {
                _videoThread.Join(200);
                _videoThread = null;
            }

            _isConnected = false;
            _activeHandsCount = 0;
        }

        #region Background Worker Threads

        private void ReceiveLandmarksWorker()
        {
            try
            {
                _landmarksClient = new UdpClient();
                _landmarksClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _landmarksClient.Client.Bind(new IPEndPoint(IPAddress.Any, _landmarksPort));
                _landmarksClient.Client.ReceiveTimeout = 1000;

                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                while (_isRunning)
                {
                    try
                    {
                        byte[] data = _landmarksClient.Receive(ref remoteEP);
                        if (data != null && data.Length > 0)
                        {
                            string json = Encoding.UTF8.GetString(data);
                            MediaPipePacket packet = JsonUtility.FromJson<MediaPipePacket>(json);
                            if (!TrackingStreamSession.AcceptLandmarks(packet.sessionId)) continue;

                            lock (_landmarkLock)
                            {
                                _workerHands[0].isDetected = false;
                                _workerHands[1].isDetected = false;

                                if (packet.hands != null)
                                {
                                    for (int h = 0; h < packet.hands.Length; h++)
                                    {
                                        HandData hand = packet.hands[h];
                                        int hIdx = hand.index;
                                        if (hIdx < 0 || hIdx >= 2) continue;
                                        if (hand.landmarks == null || hand.landmarks.Length < 42) continue;

                                        _workerHands[hIdx].isDetected = true;
                                        _workerHands[hIdx].trackId = hand.trackId;
                                        _workerHands[hIdx].timestamp = packet.timestamp;
                                        _workerHands[hIdx].isPinching = hand.pinch;
                                        _workerHands[hIdx].pinchDistance = hand.pinchDistance;

                                        for (int i = 0; i < 21; i++)
                                        {
                                            _workerHands[hIdx].landmarks[i].x = hand.landmarks[i * 2];
                                            _workerHands[hIdx].landmarks[i].y = hand.landmarks[i * 2 + 1];
                                        }
                                    }
                                }
                                _hasNewData = true;
                            }
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.TimedOut && _isRunning)
                        {
                            // Socket closed or error
                            Thread.Sleep(5);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Debug.LogWarning($"[MediaPipeUDPReceiver] Landmarks thread error: {ex.Message}");
                }
            }
        }

        private void ReceiveVideoWorker()
        {
            try
            {
                _videoClient = new UdpClient();
                _videoClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _videoClient.Client.ReceiveBufferSize = 65536 * 4;
                _videoClient.Client.Bind(new IPEndPoint(IPAddress.Any, _videoPort));
                _videoClient.Client.ReceiveTimeout = 1000;

                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

                while (_isRunning)
                {
                    try
                    {
                        byte[] data = _videoClient.Receive(ref remoteEP);
                        if (!TrackingStreamSession.AcceptVideo(data, out data)) continue;
                        if (data != null && data.Length > 0)
                        {
                            lock (_videoLock)
                            {
                                _pendingVideoBytes = data;
                                _hasNewVideo = true;
                            }
                        }
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode != SocketError.TimedOut && _isRunning)
                        {
                            Thread.Sleep(5);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isRunning)
                {
                    Debug.LogWarning($"[MediaPipeUDPReceiver] Video thread error: {ex.Message}");
                }
            }
        }

        #endregion

        #region Main Thread Processing

        private void Update()
        {
            ProcessLandmarks();
            ProcessVideo();
            UpdateConnectionStatus();
        }

        private void ProcessLandmarks()
        {
            bool hasUpdate = false;
            lock (_landmarkLock)
            {
                if (_hasNewData)
                {
                    hasUpdate = true;
                    _hasNewData = false;
                    for (int h = 0; h < 2; h++)
                    {
                        _mainHands[h].isDetected = _workerHands[h].isDetected;
                        _mainHands[h].trackId = _workerHands[h].trackId;
                        _mainHands[h].timestamp = _workerHands[h].timestamp;
                        _mainHands[h].isPinching = _workerHands[h].isPinching;
                        _mainHands[h].pinchDistance = _workerHands[h].pinchDistance;
                        if (_workerHands[h].isDetected)
                        {
                            Array.Copy(_workerHands[h].landmarks, _mainHands[h].landmarks, 21);
                        }
                    }
                }
            }

            if (!hasUpdate) return;

            _lastPacketReceivedTime = Time.time;
            _isConnected = true;
            _packetCounter++;

            for (int h = 0; h < 2; h++)
            {
                if (_mainHands[h].isDetected)
                {
                    HandTrackingBridge.Instance?.ProcessHandLandmarks(h, _mainHands[h].landmarks, _mainHands[h].timestamp, _mainHands[h].trackId);
                }
                else if (_handActiveState[h])
                {
                    HandTrackingBridge.Instance?.SetHandLost(h);
                }
                _handActiveState[h] = _mainHands[h].isDetected;
            }

            _activeHandsCount = (_mainHands[0].isDetected ? 1 : 0) + (_mainHands[1].isDetected ? 1 : 0);
        }

        private void ProcessVideo()
        {
            if (!_enableVideoStream) return;

            // Throttle JPEG decompression to max 30 FPS to prevent blocking Unity main thread
            if (Time.unscaledTime - _lastVideoFrameTime < 0.033f) return;

            byte[] bytesToProcess = null;
            lock (_videoLock)
            {
                if (_hasNewVideo)
                {
                    bytesToProcess = _pendingVideoBytes;
                    _hasNewVideo = false;
                }
            }

            if (bytesToProcess == null || bytesToProcess.Length == 0) return;

            _lastVideoFrameTime = Time.unscaledTime;

            if (_videoTexture == null)
            {
                _videoTexture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            }

            _videoTexture.LoadImage(bytesToProcess);

            if (_cameraDisplay != null && _cameraDisplay.texture != _videoTexture)
            {
                _cameraDisplay.texture = _videoTexture;
                if (_cameraDisplay.material != null)
                {
                    _cameraDisplay.material.mainTexture = _videoTexture;
                }
            }
        }

        private void UpdateConnectionStatus()
        {
            // FPS calculation
            _fpsTimer += Time.deltaTime;
            if (_fpsTimer >= 1.0f)
            {
                _receivedLandmarksFPS = _packetCounter / _fpsTimer;
                _packetCounter = 0;
                _fpsTimer = 0f;
            }

            // Connection timeout (no packet in 2.0s)
            if (_isConnected && Time.time - _lastPacketReceivedTime > 2.0f)
            {
                _isConnected = false;
                _activeHandsCount = 0;
                _receivedLandmarksFPS = 0f;

                if (HandTrackingBridge.Instance != null)
                {
                    HandTrackingBridge.Instance.SetHandLost(0);
                    HandTrackingBridge.Instance.SetHandLost(1);
                }
                _handActiveState[0] = false;
                _handActiveState[1] = false;
            }
        }

        #endregion

        #region Python Streamer Process Management

        private static Process _pythonTrackerProcess;
        public static bool IsPythonTrackerRunning => _pythonTrackerProcess != null && !_pythonTrackerProcess.HasExited;

        [ContextMenu("Start MediaPipe Python Streamer")]
        public void StartPythonTracker()
        {
            // Single launch path: HandTrackingBridge owns the streamer process (one attempt per
            // Play session), so the two components never start Python twice or compete for the camera.
            if (HandTrackingBridge.Instance != null)
            {
                HandTrackingBridge.Instance.StartPythonStreamerStealth();
                return;
            }
            Debug.LogError("[MediaPipeUDPReceiver] HandTrackingBridge is required to launch the Python streamer (enter Play Mode with a HandTrackingBridge in the scene).");
        }

        /// <summary>
        /// Safely terminates the external Python tracker process and releases camera and UDP resources.
        /// </summary>
        public static void TerminateExternalTrackerProcess()
        {
            if (_pythonTrackerProcess != null)
            {
                try
                {
                    if (!_pythonTrackerProcess.HasExited)
                    {
                        _pythonTrackerProcess.Kill();
                        _pythonTrackerProcess.WaitForExit(1000);
                        Debug.Log("[MediaPipeUDPReceiver] Terminated external Python tracker process.");
                    }
                    _pythonTrackerProcess.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MediaPipeUDPReceiver] Note while terminating process: {ex.Message}");
                }
                finally
                {
                    _pythonTrackerProcess = null;
                }
            }

            // Only terminate the Process object this launcher owns. Session tags
            // isolate other senders without killing unrelated windows/processes.
        }

        #endregion
    }
}
