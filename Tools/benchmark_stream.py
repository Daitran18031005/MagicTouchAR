"""Measure the tracker stream the way Unity receives it (no images stored, run with Unity closed).

Starts Tools/mediapipe_streamer.py with a session id, listens on the landmark/video UDP ports
and reports packet rate, gaps that would trip HandTrackingBridge's stale-packet timeout (old fixed
0.25 s rule and the adaptive rule), the wire format (pinchPending etc.) and tagged JPEG frames.

  .venv\\Scripts\\python.exe Tools\\benchmark_stream.py --cpu-policy auto --seconds 25
"""
import argparse
import json
import os
import socket
import statistics
import subprocess
import sys
import time
import uuid

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
REQUIRED = ("pinch", "pinchPending", "pinchVersion", "releaseCount", "landmarks")


def adaptive_trips(gaps):
    """Gaps over HandTrackingBridge.StalePacketTimeout, using the interval smoothed before each gap."""
    interval, trips = 1 / 30, 0
    for gap in gaps:
        if gap > min(max(3 * interval, 0.25), 0.75):
            trips += 1
        if gap < 1:
            interval += (gap - interval) * 0.2
    return trips


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cpu-policy", choices=["auto", "default"], default="auto")
    parser.add_argument("--seconds", type=float, default=25)
    parser.add_argument("--port", type=int, default=9050)
    parser.add_argument("--video-port", type=int, default=9051)
    parser.add_argument("--output", default=None)
    args = parser.parse_args()
    output = args.output or os.path.join(ROOT, "Logs", f"stream-benchmark-{args.cpu_policy}.json")
    session = uuid.uuid4().hex

    landmarks = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    landmarks.bind(("127.0.0.1", args.port))
    landmarks.settimeout(0.02)
    video = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    video.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 1 << 21)
    video.bind(("127.0.0.1", args.video_port))
    video.setblocking(False)

    streamer = subprocess.Popen(
        [sys.executable, "-u", os.path.join(ROOT, "Tools", "mediapipe_streamer.py"), "--stream-video",
         "--port", str(args.port), "--video-port", str(args.video_port), "--session-id", session,
         "--cpu-policy", args.cpu_policy, "--duration", str(args.seconds + 120),
         "--metrics-file", os.path.join(ROOT, "Logs", f"stream-benchmark-{args.cpu_policy}-tracker.json")],
        cwd=ROOT, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    arrivals, timestamps, with_hand, pending, missing = [], [], 0, 0, 0
    video_packets = video_bad = video_bytes = 0
    started, give_up = None, time.perf_counter() + 150  # camera + model start-up can take a minute
    try:
        while started is None or time.perf_counter() - started < args.seconds:
            if started is None and time.perf_counter() > give_up:
                sys.exit("No packets from the streamer (camera busy or Unity still running?).")
            try:
                packet = json.loads(landmarks.recv(65535))
                if packet.get("sessionId") == session:
                    started = started or time.perf_counter()
                    arrivals.append(time.perf_counter())
                    timestamps.append(packet["timestamp"])
                    with_hand += bool(packet["hands"])
                    for hand in packet["hands"]:
                        missing += not all(key in hand for key in REQUIRED)
                        pending += bool(hand.get("pinchPending"))
            except socket.timeout:
                pass
            while True:
                try:
                    frame = video.recv(70000)
                except BlockingIOError:
                    break
                if started is not None:
                    video_packets += 1
                    video_bytes += len(frame)
                    video_bad += frame[:4] != b"MTAR" or frame[4:36] != session.encode() or frame[36:38] != b"\xff\xd8"
    finally:
        streamer.terminate()
        streamer.wait(10)

    gaps = [b - a for a, b in zip(arrivals, arrivals[1:])]
    ordered, duration = sorted(gaps), arrivals[-1] - arrivals[0]
    report = dict(
        cpu_policy=args.cpu_policy, seconds=round(duration, 1), landmark_packets=len(arrivals),
        landmark_fps=round(len(arrivals) / duration, 2),
        gap_ms_p50=round(statistics.median(gaps) * 1000, 1),
        gap_ms_p90=round(ordered[int(0.9 * (len(gaps) - 1))] * 1000, 1), gap_ms_max=round(ordered[-1] * 1000, 1),
        hands_lost_fixed_250ms_per_min=round(sum(g > 0.25 for g in gaps) / duration * 60, 1),
        hands_lost_adaptive_per_min=round(adaptive_trips(gaps) / duration * 60, 1),
        timestamps_increasing=all(b > a for a, b in zip(timestamps, timestamps[1:])),
        packets_with_hand=with_hand, missing_fields=missing, pending_samples=pending,
        video_fps=round(video_packets / duration, 2), video_bad=video_bad,
        video_kb_avg=round(video_bytes / max(1, video_packets) / 1024, 1))
    os.makedirs(os.path.dirname(os.path.abspath(output)), exist_ok=True)
    with open(output, "w", encoding="utf-8") as f:
        json.dump(report, f, indent=2)
    print(json.dumps(report), flush=True)


if __name__ == "__main__":
    main()
