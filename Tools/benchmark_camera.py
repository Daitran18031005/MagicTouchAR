"""Measure local webcam modes without storing camera images."""
import cv2
import time
import json
import statistics
import sys
from pathlib import Path

results = []
modes = [(cv2.CAP_ANY,640,480),(cv2.CAP_DSHOW,640,480),(cv2.CAP_DSHOW,320,240),(cv2.CAP_MSMF,640,480)]
if '--small' in sys.argv: modes = [(cv2.CAP_MSMF,320,240)]
for backend, width, height in modes:
    cap = cv2.VideoCapture(0, backend)
    if not cap.isOpened():
        results.append(dict(backend=backend, error='cannot open'))
        continue
    cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*'MJPG'))
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, width)
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, height)
    accepted = cap.set(cv2.CAP_PROP_FPS,60)
    cap.set(cv2.CAP_PROP_BUFFERSIZE,1)
    for _ in range(8): cap.read()
    intervals = []
    start = last = time.perf_counter()
    while time.perf_counter()-start < 3:
        ok, frame = cap.read()
        now = time.perf_counter()
        if ok: intervals.append(now-last)
        last = now
    result = dict(backend=cap.getBackendName(),requested=[width,height,60],
        actual=[cap.get(cv2.CAP_PROP_FRAME_WIDTH),cap.get(cv2.CAP_PROP_FRAME_HEIGHT),cap.get(cv2.CAP_PROP_FPS)],
        fps_property_accepted=accepted, measured_fps=round(len(intervals)/(time.perf_counter()-start),2),
        median_ms=round(statistics.median(intervals)*1000,2) if intervals else None)
    results.append(result)
    print(json.dumps(result),flush=True)
    cap.release()
Path('Logs/camera-benchmark.json').write_text(json.dumps(results,indent=2))
