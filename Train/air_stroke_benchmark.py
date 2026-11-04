"""Air-drawing benchmark: stroke_dataset shapes observed the way the live tracker sees them.

Every gesture is drawn at a constant pen speed but only sampled at the tracker frame rate,
with residual jitter and the DrawingManager rules (the first pinch sample only arms the
stroke; points closer than minDrawDistance are skipped). Optionally one noisy open frame
hits the middle of a stroke:
  split  - immediate release, as without debouncing: the stroke ends, two samples are lost
           and the rest becomes a new StrokeID;
  freeze - debounced release (pinchPending): one sample is not drawn, the stroke continues.
The same base shapes are reused in every condition (paired comparison). Seeds are disjoint
from finetune_caret.py (1201/2302/3403). Output: JSON for Unity's TrackingStreamChecks.

  .venv\\Scripts\\python.exe Train/air_stroke_benchmark.py --output Logs/air-strokes.json
"""
import argparse
import json

import numpy as np

try:
    from .stroke_dataset import CLASS_NAMES, generate_strokes
except ImportError:
    from stroke_dataset import CLASS_NAMES, generate_strokes

CONDITIONS = [("none", fps) for fps in (4, 8, 15, 22, 30)] + \
             [(mode, fps) for mode in ("split", "freeze") for fps in (8, 22)]


def observe(strokes, fps, rng, noisy="none", jitter_px=1.5, min_draw=2.0):
    lengths = [float(np.linalg.norm(np.diff(s, axis=0), axis=1).sum()) for s in strokes]
    speed = sum(lengths) / rng.uniform(0.8, 1.6)  # whole symbol in 0.8-1.6 s
    noisy_stroke = int(rng.integers(len(strokes))) if noisy != "none" else -1
    points, stroke_id = [], 0

    def emit(samples):
        for i, (x, y) in enumerate(samples):
            if i and np.hypot(x - points[-1]["X"], y - points[-1]["Y"]) <= min_draw:
                continue
            points.append({"X": round(float(x), 2), "Y": round(float(y), 2), "StrokeID": stroke_id})

    for index, (stroke, length) in enumerate(zip(strokes, lengths)):
        along = np.concatenate([[0.0], np.cumsum(np.linalg.norm(np.diff(stroke, axis=0), axis=1))])
        times = np.arange(rng.uniform(0, 1 / fps), length / speed, 1 / fps)
        samples = np.column_stack([np.interp(times * speed, along, stroke[:, k]) for k in (0, 1)])
        samples = (samples + rng.normal(0, jitter_px, samples.shape))[1:]
        if index == noisy_stroke and len(samples) >= 6:
            k = int(rng.integers(2, len(samples) - 3))
            if noisy == "split":
                emit(samples[:k])
                stroke_id += 1
                samples = samples[k + 2:]
            else:
                samples = np.delete(samples, k, axis=0)
        if len(samples):
            emit(samples)
            stroke_id += 1
    return points


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", required=True)
    parser.add_argument("--per-class", type=int, default=50)
    parser.add_argument("--seed", type=int, default=4404)
    args = parser.parse_args()

    shapes = np.random.default_rng(args.seed)
    base = [(cid, [s * 1.6 for s in generate_strokes(cid, shapes)])
            for cid in range(len(CLASS_NAMES)) for _ in range(args.per_class)]
    samples = []
    for c, (noisy, fps) in enumerate(CONDITIONS):
        rng = np.random.default_rng(args.seed * 100 + c)
        for cid, strokes in base:
            samples.append({"label": cid, "fps": fps, "noisy": noisy, "points": observe(strokes, fps, rng, noisy)})
    with open(args.output, "w", encoding="utf-8") as f:
        json.dump({"classNames": CLASS_NAMES, "samples": samples}, f)
    print(f"wrote {len(samples)} gestures ({len(base)} shapes x {len(CONDITIONS)} conditions) to {args.output}")


if __name__ == "__main__":
    main()
