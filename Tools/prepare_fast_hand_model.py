"""Build an optional fast palm detector bundle; retain the existing landmark model."""
import hashlib
import io
import json
from pathlib import Path
import urllib.request
import zipfile
import flatbuffers
from mediapipe.tasks.metadata import schema_py_generated as schema

url = 'https://storage.googleapis.com/mediapipe-assets/palm_detection_lite.tflite'
data = urllib.request.urlopen(url, timeout=30).read()
if data[4:8] != b'TFL3': raise ValueError('Not a TFLite model')
target = Path('Tools/hand_landmarker_fast.task')
with zipfile.ZipFile('hand_landmarker.task') as original:
    # Both palm models have the same float32 image and detection tensor contract.
    # Tasks requires the normalization metadata supplied by the original bundle.
    original_palm = original.read('hand_detector.tflite')
    full = schema.ModelT.InitFromObj(schema.Model.GetRootAsModel(original_palm, 0))
    lite = schema.ModelT.InitFromObj(schema.Model.GetRootAsModel(data, 0))
    for name in ('inputs', 'outputs'):
        full_tensors = [full.subgraphs[0].tensors[i] for i in getattr(full.subgraphs[0], name)]
        lite_tensors = [lite.subgraphs[0].tensors[i] for i in getattr(lite.subgraphs[0], name)]
        assert len(full_tensors) == len(lite_tensors)
        for a,b in zip(full_tensors,lite_tensors):
            assert a.type == b.type and list(a.shape) == list(b.shape), 'Incompatible palm tensor contract'
    entry = next(m for m in full.metadata if m.name == b'TFLITE_METADATA')
    buffer = full.buffers[entry.buffer]
    entry.buffer = len(lite.buffers)
    lite.buffers.append(buffer)
    lite.metadata = [m for m in (lite.metadata or []) if m.name != b'TFLITE_METADATA'] + [entry]
    builder = flatbuffers.Builder(0)
    builder.Finish(lite.Pack(builder), file_identifier=b'TFL3')
    data = bytes(builder.Output())
    with zipfile.ZipFile(target, 'w', compression=zipfile.ZIP_STORED) as bundle:
        for name in original.namelist():
            bundle.writestr(name, data if name == 'hand_detector.tflite' else original.read(name))
Path('Tools/hand_landmarker_fast.sources.json').write_text(json.dumps({
    'palm_source':url,'palm_sha256':hashlib.sha256(data).hexdigest(),
    'landmark_source':'Unchanged hand_landmarks_detector.tflite from project hand_landmarker.task'
}, indent=2))
print('Prepared optional model:', target, target.stat().st_size)
