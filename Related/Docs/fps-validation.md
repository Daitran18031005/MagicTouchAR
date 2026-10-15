Camera and tracking validation
==============================

Measured on the project's webcam (no camera images saved):

- MSMF 640x480 requesting 60 FPS: driver selected 30 FPS, measured 29.91–29.93 FPS.
- MSMF 320x240 requesting 60 FPS: driver selected 30 FPS, measured 29.93 FPS.
- DirectShow 640x480 and 320x240 requesting 60 FPS: reported 60 but measured 14.96 FPS.
- Original palm detector with optimized image/video processing: approximately 20–21 tracking FPS in the no-hand diagnostic run.
- Lite palm detector with the unchanged original landmark model: approximately 22–23 tracking FPS in the no-hand diagnostic run.
- Unity Play Mode observed with one hand: camera 30.24 FPS, tracking 28.75 FPS, inference 29.4 ms (rolling two-second window).
- Reacquisition/load can reduce tracking throughput; these measurements do not imply sustained 60 FPS or identical hand motions across runs.

Validation passed in Unity Editor: key-point geometry, no spline overshoot, fast motion uncapped, first/recovery sample rejection, player isolation, 80 ms visible-release grace and separate 140 ms tracking-loss grace.
C# gameplay and Editor build passed with zero errors/warnings before the final model-selection option; Unity subsequently compiled the final option and ran Play Mode.
The finger-landmark model was verified byte-for-byte identical between the original and optional fast bundle.

Live counters: HandTrackingBridge > Measured Camera Performance.
Current rolling measurements: Logs/tracking-performance.json.
Original hand_landmarker.task retained. Disable Use Fast Palm Detector on HandTrackingBridge before entering Play Mode to return to the original palm detector.
Official palm model source: https://github.com/google-ai-edge/mediapipe/blob/master/docs/solutions/models.md
