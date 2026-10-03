using UnityEngine;
using UnityEngine.UI;

namespace MagicTouch.CameraControl
{
    /// <summary>
    /// Manages the front-facing camera feed and renders it as an undistorted fullscreen background.
    /// Handles mobile device rotation, selfie mirroring, and aspect fill scaling.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class ARCameraBackground : MonoBehaviour
    {
        [Header("UI Reference")]
        [SerializeField] private RawImage _cameraDisplay;
        [SerializeField] private AspectRatioFitter _aspectRatioFitter;

        [Header("Source Selection")]
        [Tooltip("If true, receives camera feed via MediaPipe UDP Streamer to avoid Windows webcam conflicts.")]
        [SerializeField] private bool _useMediaPipeStream = true;

        [Header("Camera Settings (For Unity WebCamTexture fallback)")]
        [SerializeField] private int _requestedWidth = 1280;
        [SerializeField] private int _requestedHeight = 720;
        [SerializeField] private int _requestedFPS = 60;
        [SerializeField] private bool _preferFrontFacing = true;

        private WebCamTexture _webCamTexture;
        private RectTransform _rectTransform;

        public WebCamTexture WebCamTexture => _webCamTexture;
        public bool IsInitialized => _useMediaPipeStream || (_webCamTexture != null && _webCamTexture.didUpdateThisFrame);
        public bool UseMediaPipeStream
        {
            get => _useMediaPipeStream;
            set => _useMediaPipeStream = value;
        }

        private void Awake()
        {
            if (_cameraDisplay == null)
            {
                _cameraDisplay = GetComponent<RawImage>();
            }

            _rectTransform = _cameraDisplay.rectTransform;

            if (_aspectRatioFitter == null)
            {
                _aspectRatioFitter = GetComponent<AspectRatioFitter>();
            }
        }

        private void Start()
        {
            if (!_useMediaPipeStream)
            {
                InitializeCamera();
            }
            else
            {
                // Setup transform for mirrored selfie display
                _rectTransform.localScale = new Vector3(_preferFrontFacing ? -1f : 1f, 1f, 1f);
                if (_aspectRatioFitter != null)
                {
                    _aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                    _aspectRatioFitter.aspectRatio = 640f / 480f;
                }
            }
        }

        private void Update()
        {
            if (_useMediaPipeStream)
            {
                if (_cameraDisplay.texture != null && _aspectRatioFitter != null)
                {
                    float aspect = (float)_cameraDisplay.texture.width / Mathf.Max(1, _cameraDisplay.texture.height);
                    _aspectRatioFitter.aspectRatio = aspect;
                }
                return;
            }

            if (_webCamTexture == null || !_webCamTexture.isPlaying)
            {
                return;
            }

            // Continuously adjust orientation and aspect ratio to adapt to device orientation changes
            UpdateDisplayTransform();
        }

        /// <summary>
        /// Finds the requested camera device (front-facing by default) and starts the video stream.
        /// </summary>
        public void InitializeCamera()
        {
            WebCamDevice[] devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                Debug.LogWarning("[ARCameraBackground] No camera devices found on this system.");
                return;
            }

            string targetDeviceName = devices[0].name;
            bool foundTarget = false;

            // Search for front-facing camera for AR selfie tracking
            for (int i = 0; i < devices.Length; i++)
            {
                if (_preferFrontFacing && devices[i].isFrontFacing)
                {
                    targetDeviceName = devices[i].name;
                    foundTarget = true;
                    break;
                }
            }

            if (!foundTarget && _preferFrontFacing)
            {
                Debug.LogWarning("[ARCameraBackground] Front camera not detected. Falling back to default: " + targetDeviceName);
            }

            _webCamTexture = new WebCamTexture(targetDeviceName, _requestedWidth, _requestedHeight, _requestedFPS);
            _cameraDisplay.texture = _webCamTexture;
            _cameraDisplay.material.mainTexture = _webCamTexture;

            _webCamTexture.Play();
            Debug.Log($"[ARCameraBackground] Started webcam: {targetDeviceName} ({_webCamTexture.width}x{_webCamTexture.height})");
        }

        /// <summary>
        /// Scales and rotates the RawImage to prevent stretch distortion in portrait/landscape modes.
        /// </summary>
        private void UpdateDisplayTransform()
        {
            if (_webCamTexture.width <= 16)
            {
                return; // Video stream not yet delivering valid frames
            }

            // 1. Calculate rotation angle provided by the camera driver
            int videoRotationAngle = _webCamTexture.videoRotationAngle;
            bool isRotated = (videoRotationAngle == 90 || videoRotationAngle == 270);

            // 2. Adjust RectTransform rotation
            _rectTransform.localEulerAngles = new Vector3(0f, 0f, -videoRotationAngle);

            // 3. Handle front camera mirroring and vertical flips
            // Most front cameras require horizontal flip (scale X = -1) to feel natural like a mirror
            float scaleX = _preferFrontFacing ? -1f : 1f;
            float scaleY = _webCamTexture.videoVerticallyMirrored ? -1f : 1f;
            _rectTransform.localScale = new Vector3(scaleX, scaleY, 1f);

            // 4. Calculate effective video aspect ratio
            float videoAspect = isRotated
                ? (float)_webCamTexture.height / _webCamTexture.width
                : (float)_webCamTexture.width / _webCamTexture.height;

            // 5. Fit or Envelope screen using AspectRatioFitter if present
            if (_aspectRatioFitter != null)
            {
                _aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                _aspectRatioFitter.aspectRatio = videoAspect;
            }
        }

        private void OnDisable()
        {
            if (_webCamTexture != null && _webCamTexture.isPlaying)
            {
                _webCamTexture.Stop();
            }
        }

        private void OnDestroy()
        {
            if (_webCamTexture != null)
            {
                _webCamTexture.Stop();
                Destroy(_webCamTexture);
                _webCamTexture = null;
            }
        }
    }
}
