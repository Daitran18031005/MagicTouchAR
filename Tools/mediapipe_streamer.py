#!/usr/bin/env python3
"""
mediapipe_streamer.py - High-Performance Multi-Threaded Hand Tracking Streamer for Magic Touch AR.
Phase 5 Master Polish:
- Toggleable headless mode avoids preview rendering; FPS depends on camera and inference load.
- Dedicated VideoStreamWidget thread: cap.read() runs in isolated daemon thread (no hardware I/O blocking).
- Legacy Hands defaults to model_complexity=1; Tasks uses the selected model bundle. Both track up to two hands.
- Dual UDP Streaming: Port 9050 (JSON landmarks) and Port 9051 (optional video frames).
"""

import sys
import os
import time
import json
import socket
import argparse
import threading
from hand_identity import HandIdentityTracker
from stream_protocol import video_prefix
from pinch_state import PinchState
import cv2
import mediapipe as mp

# ==============================================================================
# GLOBAL CONFIGURATION & MASTER TOGGLES
# ==============================================================================
ENABLE_PREVIEW = False      # Set to False for headless mode (max FPS, 0 rendering overhead)
DEFAULT_HOST = "127.0.0.1"
LANDMARKS_PORT = 9050
VIDEO_PORT = 9051
CAMERA_INDEX = 0
FRAME_WIDTH = 640
FRAME_HEIGHT = 480
TARGET_FPS = 60
MODEL_PATH_OVERRIDE = None

# Key MediaPipe Landmark Indices
WRIST = 0
THUMB_TIP = 4
INDEX_TIP = 8

# Standard 21-hand landmark bone connections
HAND_CONNECTIONS = [
    (0, 1), (1, 2), (2, 3), (3, 4),
    (0, 5), (5, 6), (6, 7), (7, 8),
    (5, 9), (9, 10), (10, 11), (11, 12),
    (9, 13), (13, 14), (14, 15), (15, 16),
    (13, 17), (17, 18), (18, 19), (19, 20),
    (0, 17)
]


# ==============================================================================
# Multi-threaded VideoCapture (VideoStreamWidget)
# ==============================================================================
class VideoStreamWidget:
    """Asynchronous camera reader that continuously captures frames on a dedicated daemon thread."""
    def __init__(self, src=0, width=640, height=480, fps=60):
        self.src = src
        if sys.platform.startswith("win"):
            # DSHOW initializes in <1s on Windows; MSMF often hangs 30-40s during device discovery
            backend = cv2.CAP_DSHOW
            self.cap = cv2.VideoCapture(src, backend)
            if not self.cap.isOpened():
                backend = cv2.CAP_MSMF
                self.cap = cv2.VideoCapture(src, backend)
        else:
            backend = cv2.CAP_ANY
            self.cap = cv2.VideoCapture(src, backend)

        if not self.cap.isOpened():
            raise RuntimeError(f"Could not open camera {src}!")

        # Set resolution, frame rate, and buffer size
        self.cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc('M', 'J', 'P', 'G'))
        self.cap.set(cv2.CAP_PROP_FRAME_WIDTH, width)
        self.cap.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
        self.cap.set(cv2.CAP_PROP_FPS, fps)
        self.cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc('M', 'J', 'P', 'G'))
        self.cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)

        self.width = int(self.cap.get(cv2.CAP_PROP_FRAME_WIDTH))
        self.height = int(self.cap.get(cv2.CAP_PROP_FRAME_HEIGHT))
        self.reported_fps = self.cap.get(cv2.CAP_PROP_FPS)
        self.backend = self.cap.getBackendName()

        self.ret, self.frame = self.cap.read()
        self.frame_sequence = 1
        self.lock = threading.Lock()
        self.running = True
        self.thread = threading.Thread(target=self._capture_worker, daemon=True, name="CameraWorker")
        self.thread.start()

    def _capture_worker(self):
        while self.running:
            ret, frame = self.cap.read()
            if ret and frame is not None:
                with self.lock:
                    self.ret = ret
                    self.frame = frame
                    self.frame_sequence += 1
            else:
                time.sleep(0.001)

    def read(self):
        """Returns the latest available frame instantly without hardware wait."""
        with self.lock:
            if not self.ret or self.frame is None:
                return False, None
            return True, self.frame.copy()

    def isOpened(self):
        return self.running and self.cap.isOpened()

    def read_latest(self, previous_sequence):
        """Do not run inference repeatedly on the same camera exposure."""
        with self.lock:
            if not self.ret or self.frame is None or self.frame_sequence == previous_sequence:
                return False, None, previous_sequence
            return True, self.frame.copy(), self.frame_sequence

    def release(self):
        self.running = False
        if self.thread.is_alive():
            self.thread.join(timeout=0.3)
        self.cap.release()


class VideoSender:
    """JPEG encoding stays off the inference path and never queues old frames."""
    def __init__(self, camera, udp_socket, target, prefix=b''):
        self.camera, self.socket, self.target = camera, udp_socket, target
        self.prefix = prefix
        self.stop = threading.Event()
        self.thread = threading.Thread(target=self._run, daemon=True, name="VideoSender")
        self.thread.start()

    def _run(self):
        sequence = -1
        while not self.stop.is_set():
            started = time.perf_counter()
            ok, frame, sequence = self.camera.read_latest(sequence)
            if ok:
                success, encoded = cv2.imencode('.jpg', frame, [int(cv2.IMWRITE_JPEG_QUALITY), 50])
                if success and encoded.nbytes + len(self.prefix) <= 65000:
                    try:
                        self.socket.sendto(self.prefix + encoded.tobytes(), self.target)
                    except OSError:
                        pass
            self.stop.wait(max(0.001, 1.0 / 30.0 - (time.perf_counter() - started)))

    def close(self):
        self.stop.set()
        self.thread.join(timeout=1.0)


# ==============================================================================
# MediaPipe Hands Setup & Compatibility Wrapper
# ==============================================================================
if hasattr(mp, "solutions") and hasattr(mp.solutions, "hands"):
    mp_hands = mp.solutions.hands
    mp_drawing = mp.solutions.drawing_utils if hasattr(mp.solutions, "drawing_utils") else None
else:
    from mediapipe.tasks import python as mp_python
    from mediapipe.tasks.python import vision as mp_vision

    mp_drawing = None

    class _NormalizedLandmark:
        __slots__ = ("x", "y", "z")
        def __init__(self, x=0.0, y=0.0, z=0.0):
            self.x = x
            self.y = y
            self.z = z

    class _HandsAdapter:
        def __init__(self, static_image_mode=False, max_num_hands=2, model_complexity=0,
                     min_detection_confidence=0.7, min_tracking_confidence=0.7):
            self.max_num_hands = max_num_hands
            self.model_complexity = model_complexity

            script_dir = os.path.dirname(os.path.abspath(__file__))
            candidate_paths = [
                os.path.join(script_dir, "..", "hand_landmarker.task"),
                os.path.join(script_dir, "hand_landmarker.task"),
                "hand_landmarker.task"
            ]
            model_path = next((p for p in candidate_paths if os.path.exists(p)), "hand_landmarker.task")
            fallback_model_path = model_path
            if MODEL_PATH_OVERRIDE:
                model_path = MODEL_PATH_OVERRIDE
            self.model_path = model_path

            def _create_base_options(path):
                with open(path, "rb") as f:
                    return mp_python.BaseOptions(model_asset_buffer=f.read())

            base_options = _create_base_options(model_path)
            options = mp_vision.HandLandmarkerOptions(
                base_options=base_options,
                running_mode=mp_vision.RunningMode.VIDEO,
                num_hands=max_num_hands,
                min_hand_detection_confidence=min_detection_confidence,
                min_hand_presence_confidence=0.7,
                min_tracking_confidence=min_tracking_confidence
            )
            try:
                self._detector = mp_vision.HandLandmarker.create_from_options(options)
            except (RuntimeError, ValueError, FileNotFoundError) as error:
                if not MODEL_PATH_OVERRIDE:
                    raise
                print(f"Fast model unavailable ({error}); using original hand model.", flush=True)
                options.base_options = _create_base_options(fallback_model_path)
                self._detector = mp_vision.HandLandmarker.create_from_options(options)
                self.model_path = fallback_model_path
            self._start_time = time.monotonic()
            self._last_timestamp_ms = 0

        def __enter__(self):
            return self

        def __exit__(self, exc_type, exc_val, exc_tb):
            self.close()

        def process(self, rgb_image):
            h, w, _ = rgb_image.shape
            timestamp_ms = max(self._last_timestamp_ms + 1, int((time.monotonic() - self._start_time) * 1000))
            self._last_timestamp_ms = timestamp_ms

            mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb_image)
            detection_result = self._detector.detect_for_video(mp_image, timestamp_ms)

            class _Results:
                def __init__(self):
                    self.multi_hand_landmarks = []
                    self.multi_handedness = []

            results = _Results()
            if detection_result.hand_landmarks:
                for hand_index, hand_lms in enumerate(detection_result.hand_landmarks):
                    converted = [_NormalizedLandmark(lm.x, lm.y, lm.z) for lm in hand_lms]
                    results.multi_hand_landmarks.append(converted)
                    categories = detection_result.handedness[hand_index]
                    results.multi_handedness.append(categories[0].category_name if categories else "")

            return results

        def close(self):
            if hasattr(self, "_detector") and self._detector:
                self._detector.close()

    class _SolutionsProxy:
        Hands = _HandsAdapter

    mp_hands = _SolutionsProxy()


# ==============================================================================
# Visual Skeleton Rendering (Used only when ENABLE_PREVIEW is True)
# ==============================================================================
def draw_hand_skeleton(frame, landmarks, width, height, is_pinching, hand_label):
    coords = []
    for lm in landmarks:
        px = int(lm.x * width)
        py = int(lm.y * height)
        coords.append((px, py))

    # 1. Draw bone connections
    bone_color = (220, 220, 220)
    for p1_idx, p2_idx in HAND_CONNECTIONS:
        if p1_idx < len(coords) and p2_idx < len(coords):
            cv2.line(frame, coords[p1_idx], coords[p2_idx], bone_color, 2)

    # 2. Draw standard joints
    for i, pt in enumerate(coords):
        if i not in (THUMB_TIP, INDEX_TIP):
            cv2.circle(frame, pt, 4, (0, 180, 255), -1)

    # 3. Show only the detected pinch state near the index fingertip.
    # Keep the preview unobstructed: do not draw oversized fingertip markers
    # or the connecting pinch line over the user's fingers.
    index_pt = coords[INDEX_TIP]
    pinch_color = (0, 255, 60) if is_pinching else (0, 70, 255) # Green if pinch, Red if open

    # Status text
    state_str = "PINCH" if is_pinching else "OPEN"
    label_text = f"{hand_label}: {state_str}"
    cv2.putText(frame, label_text, (index_pt[0] - 25, index_pt[1] - 18),
                cv2.FONT_HERSHEY_SIMPLEX, 0.55, pinch_color, 2, cv2.LINE_AA)


# ==============================================================================
# Main Streamer Engine
# ==============================================================================
def main():
    parser = argparse.ArgumentParser(description="MediaPipe AR Hand Tracking & Video Streamer")
    parser.add_argument("--camera", type=int, default=CAMERA_INDEX, help="Camera device index (default: 0)")
    parser.add_argument("--host", type=str, default=DEFAULT_HOST, help=f"Target Unity host (default: {DEFAULT_HOST})")
    parser.add_argument("--port", type=int, default=LANDMARKS_PORT, help=f"Landmarks UDP port (default: {LANDMARKS_PORT})")
    parser.add_argument("--video-port", type=int, default=VIDEO_PORT, help=f"Video UDP port (default: {VIDEO_PORT})")
    parser.add_argument("--stream-video", action="store_true", help="Enable video streaming to Unity port 9051")
    parser.add_argument("--preview", action="store_true", help="Enable local OpenCV visual preview window")
    parser.add_argument("--width", type=int, default=FRAME_WIDTH, help="Camera frame width (default: 640)")
    parser.add_argument("--height", type=int, default=FRAME_HEIGHT, help="Camera frame height (default: 480)")
    parser.add_argument("--opencv-threads", type=int, default=1, help="CPU threads for small image operations; 0 retains OpenCV defaults")
    parser.add_argument("--inline-video", action="store_true", help="Use original inline video encoder for comparison")
    parser.add_argument("--duration", type=float, default=0, help="Diagnostic run duration; 0 runs continuously")
    parser.add_argument("--metrics-file", default="Logs/tracking-performance.json", help="Local rolling performance counters, no images")
    parser.add_argument("--model", default=None, help="Optional task bundle; original model is the default")
    parser.add_argument("--model-complexity", type=int, choices=[0, 1], default=1, help="Legacy API: full landmark model by default; 0 is Lite")
    parser.add_argument("--session-id", default="", help="Unity launch session; prevents input from a stale streamer")
    args = parser.parse_args()
    try:
        session_prefix = video_prefix(args.session_id)
    except ValueError as error:
        parser.error(str(error))
    global MODEL_PATH_OVERRIDE
    MODEL_PATH_OVERRIDE = args.model
    if args.opencv_threads > 0:
        cv2.setNumThreads(args.opencv_threads)

    # Toggle preview window: Enabled only if global ENABLE_PREVIEW is True or --preview flag passed
    show_preview = ENABLE_PREVIEW or args.preview
    stream_video = args.stream_video

    print("=" * 65)
    print("  MAGIC TOUCH AR - HIGH PERFORMANCE HAND TRACKING STREAMER")
    print(f"  Requested camera FPS: {TARGET_FPS} | Resolution: {args.width}x{args.height}")
    print(f"  Headless Mode      : {not show_preview} (ENABLE_PREVIEW={show_preview})")
    print(f"  UDP Coordinates    : {args.host}:{args.port}")
    print(f"  UDP Video Stream   : {args.host}:{args.video_port} (Active: {stream_video})")
    print("=" * 65)

    # UDP sockets
    landmarks_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    landmarks_target = (args.host, args.port)

    video_socket = None
    video_target = None
    if stream_video:
        video_socket = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        video_target = (args.host, args.video_port)

    # Multi-threaded camera capture
    print(f"Initializing camera {args.camera} in background thread...")
    cam = VideoStreamWidget(args.camera, width=args.width, height=args.height, fps=TARGET_FPS)
    print(f"Camera active: {cam.width}x{cam.height} | {cam.backend} | driver FPS: {cam.reported_fps:.1f}")
    if show_preview:
        print("Visual debug window active. Press 'q' or ESC in the window to quit.\n")
    else:
        print("Running headlessly at maximum FPS. Press Ctrl+C in terminal to stop.\n")

    identity = HandIdentityTracker(mirror=True)
    pinch_states = {}
    frame_count = 0
    t_start = time.time()
    last_fps_print = time.time()
    last_video_send = 0.0
    current_fps = 0.0
    camera_fps = 0.0
    inference_ms = 0.0
    video_sender = VideoSender(cam, video_socket, video_target, session_prefix) if stream_video and not args.inline_video else None

    # Prefer the full legacy model; the Tasks adapter loads its selected bundle.
    with mp_hands.Hands(
        static_image_mode=False,
        max_num_hands=2,
        model_complexity=args.model_complexity,
        min_detection_confidence=0.7,
        min_tracking_confidence=0.7
    ) as hands:
        try:
            last_camera_sequence = -1
            metrics_started = run_started = time.perf_counter()
            metrics_frames = metrics_inference = 0
            metrics_sequence = cam.frame_sequence
            while cam.isOpened():
                if args.duration > 0 and time.perf_counter() - run_started >= args.duration:
                    break
                # Non-blocking grab of the latest available frame
                ret, frame, last_camera_sequence = cam.read_latest(last_camera_sequence)
                if not ret or frame is None:
                    time.sleep(0.001)
                    continue

                frame_count += 1
                now_ms = int(time.time() * 1000)

                # Inference on RGB
                rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                inference_started = time.perf_counter()
                results = hands.process(rgb_frame)
                metrics_inference += time.perf_counter() - inference_started
                metrics_frames += 1

                # Process tracked hands
                hands_data = []
                if results.multi_hand_landmarks:
                    raw_hands = []
                    for detection_index, hand_result in enumerate(results.multi_hand_landmarks):
                        hand_lms = getattr(hand_result, "landmark", hand_result)
                        flat_lms = []
                        for lm in hand_lms:
                            flat_lms.append(round(lm.x, 4))
                            flat_lms.append(round(lm.y, 4))

                        # Thumb (4) to Index (8) distance
                        thumb_lm = hand_lms[THUMB_TIP]
                        index_lm = hand_lms[INDEX_TIP]
                        dx = thumb_lm.x - index_lm.x
                        dy = thumb_lm.y - index_lm.y
                        raw_dist = (dx * dx + dy * dy) ** 0.5

                        wrist_x = hand_lms[WRIST].x
                        classifications = results.multi_handedness
                        label = ""
                        if detection_index < len(classifications):
                            entry = classifications[detection_index]
                            label = entry if isinstance(entry, str) else entry.classification[0].label
                        raw_hands.append({
                            "wrist_x": wrist_x,
                            "wrist": (wrist_x, hand_lms[WRIST].y),
                            "label": label,
                            "raw_dist": raw_dist,
                            "landmarks": flat_lms,
                            "raw_lms": hand_lms
                        })

                    # Reserve slots through brief occlusion; never renumber the surviving hand.
                    for assigned_idx, track_id, h in identity.assign(raw_hands, time.monotonic()):
                        previous_id, state = pinch_states.get(assigned_idx, (None, None))
                        if previous_id != track_id:
                            state = PinchState()
                            pinch_states[assigned_idx] = (track_id, state)
                        state.update(h['raw_lms'], cam.width / cam.height)
                        hands_data.append({
                            "index": assigned_idx,
                            "trackId": track_id,
                            "handedness": h["label"],
                            "pinch": state.pinching,
                            "pinchVersion": 1,
                            "releaseCount": state.releases,
                            "pinchRatio": state.ratio if state.ratio != float('inf') else 999.0,
                            "pinchDistance": round(float(h["raw_dist"]), 4),
                            "landmarks": h["landmarks"]
                        })

                        # Render visual skeleton ONLY if preview is enabled
                        if show_preview:
                            draw_hand_skeleton(frame, h["raw_lms"], cam.width, cam.height,
                                               state.pinching, f"P{assigned_idx + 1} r={state.ratio:.2f}")

                # 1. Send JSON landmark packet over UDP (port 9050)
                packet = {
                    "sessionId": args.session_id,
                    "processId": os.getpid(),
                    "timestamp": now_ms,
                    "cameraFps": round(camera_fps, 2),
                    "trackingFps": round(current_fps, 2),
                    "inferenceMs": round(inference_ms, 2),
                    "hands": hands_data
                }
                json_bytes = json.dumps(packet).encode("utf-8")
                landmarks_socket.sendto(json_bytes, landmarks_target)

                # 2. Send JPEG video frame to Unity (port 9051) throttled to 30 FPS
                if stream_video and args.inline_video and (time.time() - last_video_send >= 0.033):
                    last_video_send = time.time()
                    encode_param = [int(cv2.IMWRITE_JPEG_QUALITY), 50]
                    success, encimg = cv2.imencode('.jpg', frame, encode_param)
                    if success:
                        jpeg_bytes = session_prefix + encimg.tobytes()
                        if len(jpeg_bytes) <= 65000:
                            try:
                                video_socket.sendto(jpeg_bytes, video_target)
                            except Exception:
                                pass

                # Calculate real-time FPS
                metrics_now = time.perf_counter()
                if metrics_now - metrics_started >= 2.0:
                    elapsed = metrics_now - metrics_started
                    current_fps = metrics_frames / elapsed
                    camera_fps = (cam.frame_sequence - metrics_sequence) / elapsed
                    inference_ms = metrics_inference * 1000.0 / max(metrics_frames, 1)
                    metrics = dict(camera_fps=round(camera_fps,2),tracking_fps=round(current_fps,2),
                        inference_ms=round(inference_ms,2),hands=len(hands_data),backend=cam.backend,
                        resolution=[cam.width,cam.height],driver_fps=cam.reported_fps,
                        opencv_threads=cv2.getNumThreads(),async_video=video_sender is not None,
                        model=getattr(hands, 'model_path', 'legacy'),timestamp=time.time(),
                        session_id=args.session_id,process_id=os.getpid())
                    os.makedirs(os.path.dirname(os.path.abspath(args.metrics_file)), exist_ok=True)
                    metrics_temp = args.metrics_file + '.' + str(os.getpid()) + '.tmp'
                    with open(metrics_temp, 'w', encoding='utf-8') as report:
                        json.dump(metrics, report, indent=2)
                    os.replace(metrics_temp, args.metrics_file)
                    metrics_started, metrics_sequence = metrics_now, cam.frame_sequence
                    metrics_frames = metrics_inference = 0

                # 3. Headless vs Preview Handling
                if show_preview:
                    # Draw HUD banner
                    fps_color = (0, 255, 0) if current_fps >= 50 else (0, 200, 255)
                    cv2.putText(frame, f"FPS: {current_fps:.1f}", (15, 30),
                                cv2.FONT_HERSHEY_SIMPLEX, 0.75, fps_color, 2, cv2.LINE_AA)
                    cv2.putText(frame, f"Hands: {len(hands_data)} | Res: {cam.width}x{cam.height}", (15, 60),
                                cv2.FONT_HERSHEY_SIMPLEX, 0.55, (255, 255, 255), 1, cv2.LINE_AA)

                    cv2.imshow("MediaPipe AR Hand Tracker", frame)
                    key = cv2.waitKey(1) & 0xFF
                    if key in (27, ord('q'), ord('Q')):
                        print("\nQuitting requested via preview window.")
                        break
                else:
                    # In headless mode: zero OpenCV window rendering overhead
                    # Print status to console every 2.0s
                    now = time.time()
                    if now - last_fps_print >= 2.0:
                        last_fps_print = now
                        hands_cnt = len(hands_data)
                        print(f"[STREAMER] Camera: {camera_fps:.1f} FPS | Tracking: {current_fps:.1f} FPS | Inference: {inference_ms:.1f} ms | Hands: {hands_cnt}", flush=True)

        except KeyboardInterrupt:
            print("\nStreamer interrupted by user.")
        finally:
            if video_sender is not None:
                video_sender.close()
            cam.release()
            landmarks_socket.close()
            if video_socket:
                video_socket.close()
            if show_preview:
                cv2.destroyAllWindows()
            print("MediaPipe streamer stopped cleanly.")


if __name__ == "__main__":
    main()
