# Caret model trial — 2026-10-21

The game model was fine-tuned from its existing ONNX weights, not trained from scratch.
The original graph, input/output shapes, class order and asset GUID are retained.

## Changes

- `stroke_dataset.py`: deterministic screen-space curves, varied caret apex/curvature/aspect,
  balanced 11 classes, same padding/centering/quadratic brush as Unity.
- `finetune_caret.py`: import existing fused Conv/Gemm weights; check parity; train with
  replay of other classes, limited teacher distillation and weight anchoring; select best
  checkpoint using validation only. It writes a candidate under Logs and never deploys it.
- `train_magic_touch.py`: improved caret generator, no duplicate augmentation for generated
  caret/line/unknown samples, load best checkpoint before exporting.
- Mandatory O closure gate complements the existing E completeness gate.
- CNNRecognitionTest has a true-label selector and **LƯU SAMPLE** button. It saves exact
  stroke coordinates, unquantized 28x28 inputs, probabilities, model hash and a PNG to
  `Related/Data-Samples/` in Editor, or `Application.persistentDataPath/RecognitionSamples`
  in a player build. Samples are local and excluded from Git, and are not automatically trained on.

## Measured results

11,000 generated training samples; 1,980 validation samples; 3,300 holdout samples
(300 per class, separate random seed and wider shape variation, brush radius 1.3).
At confidence >=85% and margin >=30%, BEFORE geometric gates:

| Correctly classified AND accepted | Original | Candidate |
|---|---:|---:|
| Caret | 23.00% | 99.00% |
| V | 66.00% | 95.00% |
| W | 89.33% | 97.33% |
| O | 100.00% | 100.00% |
| E | 100.00% | 100.00% |

The model passed the configured regression gate (no other valid class lost more than
3 percentage points of correct acceptance). This is a synthetic benchmark, NOT an
estimate of accuracy on real players. Handwriting styles outside these generators still
need live testing; the sample button supports that without a separate data collection session.

10 fixed edge cases also passed in Unity CPU inference, including a curved caret,
an asymmetric caret, two-stroke caret, V/W/O, E, and incomplete E. Pixel parity against
the Python rasterizer: max error 2.831e-6; Unity vs ONNX Runtime logits: 1.144e-5.
The curve cases approximate the reported problem; they are not the original user traces.
JSON/PNG export and one/two-stroke O structure checks passed.

Full report, confusion matrices and per-epoch validation metrics:
`Logs/caret-training-20261021/report.json`.

## Run / reproduce

Open `Assets/Scenes/CNNRecognitionTest.unity`, press Play, draw then click NHẬN DIỆN.
For a bad result select the actual intended symbol (default Caret) and click LƯU SAMPLE.
The game scene uses the same replaced ONNX asset automatically on the next Play.

Training dependencies are separate from camera requirements:

```powershell
.\.venv\Scripts\python.exe -m pip install -r Train/requirements-training.txt
.\.venv\Scripts\python.exe Train/finetune_caret.py --source Logs/caret-training-20261021/original.onnx --output Logs/caret-reproduce
```

Do not add samples from the holdout set to training or select checkpoints using the holdout.
To compare future real samples, keep an untouched set of JSON files outside training.

## Restore original model

Stop Play before restoring. The script verifies both backup and current model hashes,
and refuses to overwrite an unexpected later model:

```powershell
.\Train\restore_caret_model.ps1 -Restore
```

Original weights: `Logs/caret-training-20261021/original.onnx` (keep this folder).
Only the ONNX weights are restored; E/O checks and sample saving remain available.
Unity may warn that MaxPool `ceil_mode=0` is defaulted to 0 during import; the unchanged
original operator graph has the same attribute, and the parity test verifies its behavior.
