using System;
using System.Collections.Generic;
using System.Linq;
using MagicTouch.Recognition;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEngine;

namespace MagicTouch.EditorTools
{
    // Owns only editor resources: no scene objects, singletons, camera or game events.
    public class MobileMagicCNNTestWindow : EditorWindow
    {
        private const string ModelPath = "Assets/Models/magic_touch_model.onnx";
        private const float CanvasSize = 360f;
        private readonly List<Point> _points = new List<Point>();
        private Worker _worker;
        private Texture2D _preview;
        private int _strokeId;
        private bool _drawing;
        private float[] _probabilities;
        private string _status = "Vẽ bằng chuột trái, nhả chuột để kết thúc từng nét.";
        private float _confidence = .85f;
        private float _margin = .30f;
        private float _brushRadius = 1.3f;
        private float _minDiagonal = 35f;
        private float _aspect = 3.5f;
        private Vector2 _scroll;

        [MenuItem("Window/Magic Touch/MobileMagicCNN Test")]
        private static void Open()
        {
            var window = CreateInstance<MobileMagicCNNTestWindow>();
            window.titleContent = new GUIContent("MobileMagicCNN Test");
            window.minSize = new Vector2(740, 650);
            window.ShowUtility();
        }

        private void OnDisable()
        {
            _worker?.Dispose();
            _worker = null;
            if (_preview != null) DestroyImmediate(_preview);
        }

        private void OnLostFocus() { _drawing = false; }

        private void InvalidateResult()
        {
            _probabilities = null;
            _status = "Nét hoặc cấu hình đã thay đổi. Bấm Nhận diện để kiểm tra lại.";
            if (_preview != null) DestroyImmediate(_preview);
            _preview = null;
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.LabelField("MobileMagicCNN — kiểm thử độc lập với game", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Không cần Play. Không tự chốt khi nhả chuột: có thể vẽ nhiều nét rồi bấm Nhận diện.\n" +
                "Dùng model ONNX và bộ raster hóa của game. Điểm CNN được hiển thị kể cả khi bộ lọc từ chối.\n" +
                "Cấu hình dưới đây chỉ dành cho test (mặc định theo code), không thay đổi Inspector của game.",
                MessageType.Info);
            EditorGUI.BeginChangeCheck();
            _confidence = EditorGUILayout.Slider("Min confidence", _confidence, 0, 1);
            _margin = EditorGUILayout.Slider("Min margin", _margin, 0, 1);
            _brushRadius = EditorGUILayout.Slider("Brush radius (28×28)", _brushRadius, .1f, 3);
            _minDiagonal = EditorGUILayout.Slider("Min diagonal (canvas pixels)", _minDiagonal, 0, 100);
            _aspect = EditorGUILayout.Slider("Line aspect threshold", _aspect, 1, 10);
            if (EditorGUI.EndChangeCheck()) InvalidateResult();

            EditorGUILayout.BeginHorizontal();
            Rect canvas = GUILayoutUtility.GetRect(CanvasSize, CanvasSize,
                GUILayout.Width(CanvasSize), GUILayout.Height(CanvasSize));
            DrawCanvas(canvas);
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(300));
            EditorGUILayout.LabelField("Ảnh đầu vào thực tế: 28×28");
            Rect previewRect = GUILayoutUtility.GetRect(168, 168, GUILayout.Width(168), GUILayout.Height(168));
            EditorGUI.DrawRect(previewRect, Color.black);
            if (_preview != null) GUI.DrawTexture(previewRect, _preview, ScaleMode.StretchToFill);
            if (_probabilities != null)
            {
                foreach (int i in Enumerable.Range(0, _probabilities.Length).OrderByDescending(i => _probabilities[i]))
                    EditorGUILayout.LabelField($"{SentisRecognizer.ClassToSymbol[i]}", $"{_probabilities[i]:P2}");
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Xóa"))
            {
                _points.Clear();
                _strokeId = 0;
                _drawing = false;
                InvalidateResult();
            }
            if (GUILayout.Button("Bỏ nét cuối") && _points.Count > 0)
            {
                int last = _points[_points.Count - 1].StrokeID;
                _points.RemoveAll(p => p.StrokeID == last);
                InvalidateResult();
            }
            using (new EditorGUI.DisabledScope(_points.Count < 3))
                if (GUILayout.Button("Nhận diện")) Recognize();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(_status, MessageType.Info);
            EditorGUILayout.LabelField("Test E: vẽ thiếu nét giữa → Nhận diện → thêm nét giữa → Nhận diện.", EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Đây là test CNN + bộ lọc nhận diện, không mô phỏng tracking hay thời điểm chốt nét của DrawingManager.",
                EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void DrawCanvas(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(.06f, .07f, .09f));
            GUI.Label(new Rect(rect.x + 8, rect.y + 6, 280, 22), "Vùng vẽ — chuột trái, nhiều nét");
            Handles.BeginGUI();
            Handles.color = Color.white;
            for (int i = 1; i < _points.Count; i++)
            {
                Point a = _points[i - 1], b = _points[i];
                if (a.StrokeID != b.StrokeID) continue;
                Handles.DrawAAPolyLine(3f,
                    new Vector3(rect.x + a.X, rect.yMax - a.Y),
                    new Vector3(rect.x + b.X, rect.yMax - b.Y));
            }
            Handles.EndGUI();
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                _drawing = true;
                _strokeId++;
                AddPoint(rect, e.mousePosition);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _drawing)
            {
                if (rect.Contains(e.mousePosition)) AddPoint(rect, e.mousePosition);
                else _drawing = false; // Do not join a line across time spent outside the canvas.
                e.Use();
            }
            else if (e.rawType == EventType.MouseUp && _drawing)
            {
                if (rect.Contains(e.mousePosition)) AddPoint(rect, e.mousePosition);
                _drawing = false;
                Repaint();
            }
        }

        private void AddPoint(Rect rect, Vector2 mouse)
        {
            // Match Unity screen coordinates (Y up); the shared rasterizer flips to image rows.
            _points.Add(new Point(mouse.x - rect.x, rect.yMax - mouse.y, _strokeId));
            InvalidateResult();
            Repaint();
        }

        private void Recognize()
        {
            try
            {
                if (_worker == null)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ModelAsset>(ModelPath);
                    if (asset == null) throw new InvalidOperationException("Không tìm thấy model: " + ModelPath);
                    _worker = new Worker(ModelLoader.Load(asset), BackendType.CPU);
                }
                float[] pixels = GesturePreprocessor.RasterizeTo28x28(_points, _brushRadius);
                if (_preview != null) DestroyImmediate(_preview);
                _preview = new Texture2D(28, 28, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < 28; y++)
                    for (int x = 0; x < 28; x++)
                    {
                        float value = pixels[y * 28 + x];
                        _preview.SetPixel(x, 27 - y, new Color(value, value, value, 1));
                    }
                _preview.Apply();
                using var input = new Tensor<float>(new TensorShape(1, 1, 28, 28), pixels);
                _worker.Schedule(input);
                using var output = _worker.PeekOutput().ReadbackAndClone() as Tensor<float>;
                float[] logits = output.DownloadToArray();
                if (logits.Length != SentisRecognizer.ClassToSymbol.Length)
                    throw new InvalidOperationException("Số lớp model không khớp ClassToSymbol.");
                float max = logits.Max();
                _probabilities = logits.Select(v => Mathf.Exp(v - max)).ToArray();
                float sum = _probabilities.Sum();
                for (int i = 0; i < _probabilities.Length; i++) _probabilities[i] /= sum;
                int[] ranked = Enumerable.Range(0, logits.Length).OrderByDescending(i => _probabilities[i]).ToArray();
                int best = ranked[0];
                float gap = _probabilities[best] - _probabilities[ranked[1]];
                GestureSymbol symbol = SentisRecognizer.ClassToSymbol[best];
                string verdict;
                var bounds = GesturePreprocessor.CalculateBounds(_points);
                if (bounds.Diagonal < _minDiagonal) verdict = "Từ chối: nét quá nhỏ.";
                else if (GestureCompletionPolicy.IsStraight(_points) && bounds.Height > bounds.Width * _aspect)
                    verdict = "VerticalLine — quy tắc hình học bỏ qua CNN.";
                else if (GestureCompletionPolicy.IsStraight(_points) && bounds.Width > bounds.Height * _aspect)
                    verdict = "HorizontalLine — quy tắc hình học bỏ qua CNN.";
                else if (symbol == GestureSymbol.Unknown || symbol == GestureSymbol.None)
                    verdict = "Từ chối: lớp Unknown/None.";
                else if (_probabilities[best] < _confidence) verdict = "Từ chối: confidence dưới ngưỡng.";
                else if (gap < _margin) verdict = "Từ chối: chênh lệch Top 1–2 dưới ngưỡng.";
                else if (!GestureCompletionPolicy.HasRequiredStructure(_points, symbol))
                    verdict = "Từ chối: E chưa đủ thân dọc và ba nét ngang.";
                else verdict = "Chấp nhận: " + symbol;
                _status = $"CNN thô: {symbol} ({_probabilities[best]:P2}); margin: {gap:P2}\n" +
                    $"Sau bộ lọc với cấu hình test: {verdict}\n" +
                    $"Số điểm: {_points.Count}; số nét: {_points.Select(p => p.StrokeID).Distinct().Count()}.";
            }
            catch (Exception ex)
            {
                _probabilities = null;
                _status = "Không chạy được model: " + ex.Message;
                _worker?.Dispose();
                _worker = null;
                Debug.LogException(ex);
            }
        }
    }
}
