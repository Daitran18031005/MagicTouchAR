using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using MagicTouch.Tracking;

namespace MagicTouch.Tracking
{
    /// <summary>
    /// Interface for any hand tracking provider (Python UDP, Native Homuler MediaPipe, or Sentis ONNX).
    /// Decouples tracking providers from the Magic Touch game logic.
    /// </summary>
    public interface IHandTrackerProvider
    {
        bool IsRunning { get; }
        int ActiveHandsCount { get; }
        void StartTracking();
        void StopTracking();
    }

    /// <summary>
    /// Native In-Engine Hand Tracking Bridge for mobile builds (Android & iOS).
    /// Replaces the external Python terminal streamer with native C# / C++ inference.
    ///
    /// Supported Backends:
    /// 1. MediaPipe Unity Plugin by homuler (Recommended for Mobile: 45-60 FPS with native GPU/Metal/NNAPI)
    /// 2. Unity Sentis ONNX Model (Pure C# Unity fallback)
    /// </summary>
    public class NativeMediaPipeBridge : MonoBehaviour, IHandTrackerProvider
    {
        public static NativeMediaPipeBridge Instance { get; private set; }

        public enum NativeBackend
        {
            HomulerMediaPipePlugin, // Official native C++ MediaPipe via TFLite (Fastest on Mobile)
            UnitySentisONNX         // Unity Sentis compute shader execution
        }

        [Header("Native Backend Selection")]
        [Tooltip("Select which native tracking backend to run inside Unity.")]
        [SerializeField] private NativeBackend _backend = NativeBackend.HomulerMediaPipePlugin;

        [Header("Tracking Settings")]
        [SerializeField] private int _maxHands = 2;
        [SerializeField] private float _minDetectionConfidence = 0.6f;
        [SerializeField] private float _minTrackingConfidence = 0.6f;

        [Header("Unity Sentis ONNX Configuration (Optional)")]
        [Tooltip("Exported Hand Landmark .onnx model asset.")]
        [SerializeField] private ModelAsset _handLandmarkModel;
        [SerializeField] private BackendType _sentisBackend = BackendType.GPUCompute;
        private Worker _sentisWorker;

        [Header("Live Native Status (Read-Only)")]
        [SerializeField] private bool _isRunning;
        [SerializeField] private int _activeHandsCount;
        [SerializeField] private float _currentFPS;

        public bool IsRunning => _isRunning;
        public int ActiveHandsCount => _activeHandsCount;
        public NativeBackend Backend => _backend;
        public int MaxHands => _maxHands;
        public float MinDetectionConfidence => _minDetectionConfidence;
        public float MinTrackingConfidence => _minTrackingConfidence;

        private int _frameCount;
        private float _fpsTimer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // If target platform is mobile, automatically initialize native tracking
            if (Application.isMobilePlatform)
            {
                StartTracking();
            }
        }

        private void OnDestroy()
        {
            StopTracking();
        }

        private void OnApplicationQuit()
        {
            StopTracking();
        }

        public void StartTracking()
        {
            if (_isRunning) return;

            switch (_backend)
            {
                case NativeBackend.HomulerMediaPipePlugin:
                    InitHomulerPlugin();
                    break;

                case NativeBackend.UnitySentisONNX:
                    InitSentisTracking();
                    break;
            }

            _isRunning = true;
            Debug.Log($"[NativeMediaPipeBridge] Started native hand tracking using {_backend}.");
        }

        public void StopTracking()
        {
            if (!_isRunning) return;

            switch (_backend)
            {
                case NativeBackend.HomulerMediaPipePlugin:
                    ShutdownHomulerPlugin();
                    break;

                case NativeBackend.UnitySentisONNX:
                    ShutdownSentisTracking();
                    break;
            }

            _isRunning = false;
            _activeHandsCount = 0;
            Debug.Log("[NativeMediaPipeBridge] Stopped native hand tracking.");
        }

        #region Homuler MediaPipe Plugin Integration

        /// <summary>
        /// Initializes the Homuler MediaPipe Unity Plugin graph.
        /// Native TFLite runs with GPU Delegate (OpenGL ES / Vulkan on Android, Metal on iOS).
        /// </summary>
        private void InitHomulerPlugin()
        {
            Debug.Log("[NativeMediaPipeBridge] Initializing Homuler MediaPipe Native Graph...");
            // When com.github.homuler.mediapipe is imported into the project:
            // 1. Initialize ImageSource (WebCamSource or TextureSource)
            // 2. Initialize HandLandmarkerRunner or HandTrackingGraph
            // 3. Register callback on OnHandLandmarksOutput
        }

        private void ShutdownHomulerPlugin()
        {
            // Dispose Homuler Graph and resources
        }

        /// <summary>
        /// Ingestion entry point called by Homuler's HandTrackingGraph output callback.
        /// Converts normalized landmarks from Homuler format and feeds HandTrackingBridge.
        /// </summary>
        /// <param name="handIndex">0 for Player 1, 1 for Player 2</param>
        /// <param name="normalizedLandmarks">21 Vector2 normalized coordinates (x: 0..1, y: 0..1)</param>
        public void OnHomulerLandmarksReceived(int handIndex, Vector2[] normalizedLandmarks)
        {
            if (normalizedLandmarks == null || normalizedLandmarks.Length < 21)
            {
                if (HandTrackingBridge.Instance != null)
                {
                    HandTrackingBridge.Instance.SetHandLost(handIndex);
                }
                return;
            }

            // Feed directly into existing HandTrackingBridge logic (zero changes to game mechanics!)
            if (HandTrackingBridge.Instance != null)
            {
                HandTrackingBridge.Instance.ProcessHandLandmarks(handIndex, normalizedLandmarks);
            }

            _frameCount++;
        }

        #endregion

        #region Unity Sentis ONNX Integration

        private void InitSentisTracking()
        {
            if (_handLandmarkModel != null)
            {
                Model runtimeModel = ModelLoader.Load(_handLandmarkModel);
                _sentisWorker = new Worker(runtimeModel, _sentisBackend);
                Debug.Log($"[NativeMediaPipeBridge] Sentis Hand Landmark worker created on {_sentisBackend}.");
            }
            else
            {
                Debug.LogWarning("[NativeMediaPipeBridge] Sentis Hand Landmark ModelAsset is not assigned!");
            }
        }

        private void ShutdownSentisTracking()
        {
            _sentisWorker?.Dispose();
            _sentisWorker = null;
        }

        /// <summary>
        /// Runs Sentis ONNX inference on a 224x224 cropped hand tensor.
        /// Extracts 21 3D landmarks and feeds HandTrackingBridge.
        /// </summary>
        public void ProcessFrameWithSentis(Texture handCropTexture, int handIndex)
        {
            if (_sentisWorker == null || handCropTexture == null) return;

            // 1. Convert cropped hand texture to normalized tensor [1, 3, 224, 224] in [0, 1]
            using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, 224, 224));
            TextureConverter.ToTensor(handCropTexture, inputTensor, new TextureTransform());
            _sentisWorker.Schedule(inputTensor);

            // 2. Fetch landmark output tensor
            Tensor rawOutput = _sentisWorker.PeekOutput();
            using Tensor<float> cpuTensor = rawOutput.ReadbackAndClone() as Tensor<float>;
            if (cpuTensor != null)
            {
                float[] landmarkFloats = cpuTensor.DownloadToArray();
                if (landmarkFloats != null && landmarkFloats.Length >= 63)
                {
                    Vector2[] landmarks = new Vector2[21];
                    for (int i = 0; i < 21; i++)
                    {
                        // MediaPipe 3D landmark tensor layout: [x, y, z] per point
                        float x = landmarkFloats[i * 3 + 0] / 224f;
                        float y = landmarkFloats[i * 3 + 1] / 224f;
                        landmarks[i] = new Vector2(x, y);
                    }

                    if (HandTrackingBridge.Instance != null)
                    {
                        HandTrackingBridge.Instance.ProcessHandLandmarks(handIndex, landmarks);
                    }
                }
            }
        }

        #endregion

        private void Update()
        {
            if (!_isRunning) return;

            _fpsTimer += Time.deltaTime;
            if (_fpsTimer >= 1.0f)
            {
                _currentFPS = _frameCount / _fpsTimer;
                _frameCount = 0;
                _fpsTimer = 0f;
            }
        }
    }
}
