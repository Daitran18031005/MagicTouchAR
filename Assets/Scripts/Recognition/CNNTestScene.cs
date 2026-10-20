using System;
using System.Collections.Generic;
using System.Linq;
using Unity.InferenceEngine;
using UnityEngine;

namespace MagicTouch.Recognition
{
    /// <summary>Standalone mouse-only diagnostic scene. Never calls gameplay or tracking.</summary>
    public class CNNTestScene : MonoBehaviour
    {
        [SerializeField] private ModelAsset _modelAsset;
        private readonly List<Point> _points = new List<Point>();
        private Worker _worker;
        private Texture2D _preview;
        private bool _drawing;
        private int _stroke;
        private float[] _probabilities;
        private string _message = "Vẽ nhiều nét bằng chuột trái, sau đó bấm NHẬN DIỆN.";
        private Vector2 _scroll;
        private const float CanvasSize = 400;
        private const float BrushRadius = 1.3f;

        private void Awake()
        {
            // An isolated camera prevents a "No cameras rendering" overlay in Game View.
            var camera = gameObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.10f, .12f, .16f);
            camera.cullingMask = 0;
            try
            {
                if (_modelAsset == null) throw new InvalidOperationException("Chưa gán model ONNX.");
                _worker = new Worker(ModelLoader.Load(_modelAsset), BackendType.CPU);
            }
            catch (Exception ex) { _message = "Lỗi nạp model: " + ex.Message; }
        }

        private void OnDestroy()
        {
            _worker?.Dispose();
            if (_preview != null) Destroy(_preview);
        }

        private void OnApplicationFocus(bool focused) { if (!focused) _drawing = false; }

        private void Invalidate()
        {
            _probabilities = null;
            if (_preview != null) Destroy(_preview);
            _preview = null;
            _message = "Nét đã thay đổi. Bấm NHẬN DIỆN để cập nhật kết quả.";
        }

        private void OnGUI()
        {
            _scroll = GUI.BeginScrollView(new Rect(0, 0, Screen.width, Screen.height),
                _scroll, new Rect(0, 0, 940, 700));
            GUI.Label(new Rect(20, 10, 900, 30), "MOBILE MAGIC CNN — SCENE TEST RIÊNG (không camera tracking / không gameplay)");
            GUI.Label(new Rect(20, 42, 900, 45),
                "Giữ chuột trái để vẽ. Nhả chuột chỉ kết thúc một nét, KHÔNG tự nhận diện.\n" +
                "Vẽ E thiếu nét giữa → Nhận diện → thêm nét giữa → Nhận diện lại.");
            var canvas = new Rect(20, 100, CanvasSize, CanvasSize);
            Color oldColor = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(canvas, Texture2D.whiteTexture);
            GUI.color = oldColor;
            DrawStrokes(canvas);
            HandleMouse(canvas);
            if (GUI.Button(new Rect(20, 515, 125, 36), "XÓA"))
            {
                _points.Clear();
                _drawing = false;
                Invalidate();
            }
            if (GUI.Button(new Rect(155, 515, 125, 36), "BỎ NÉT CUỐI") && _points.Count > 0)
            {
                int id = _points[_points.Count - 1].StrokeID;
                _points.RemoveAll(p => p.StrokeID == id);
                Invalidate();
            }
            GUI.enabled = _worker != null && _points.Count >= 3;
            if (GUI.Button(new Rect(290, 515, 130, 36), "NHẬN DIỆN")) Recognize();
            GUI.enabled = true;
            GUI.Label(new Rect(450, 90, 450, 26), "Ảnh CNN nhận: 28×28 (phóng to)");
            if (_preview != null) GUI.DrawTexture(new Rect(450, 120, 168, 168), _preview);
            if (_probabilities != null)
            {
                int row = 0;
                foreach (int i in Enumerable.Range(0, _probabilities.Length).OrderByDescending(i => _probabilities[i]))
                {
                    GUI.Label(new Rect(650, 120 + row * 27, 270, 26),
                        $"{SentisRecognizer.ClassToSymbol[i],-16} {_probabilities[i]:P2}");
                    row++;
                }
            }
            GUI.Label(new Rect(20, 565, 900, 75), _message);
            GUI.Label(new Rect(20, 645, 900, 50),
                "Bộ lọc test mặc định: confidence ≥ 85%, margin ≥ 30%, đường chéo ≥ 35px.\n" +
                "Không kiểm thử thời điểm chốt nét của DrawingManager. Không sửa cấu hình hay model của game.");
            GUI.EndScrollView();
        }

        private void DrawStrokes(Rect canvas)
        {
            for (int i = 1; i < _points.Count; i++)
            {
                Point a = _points[i - 1], b = _points[i];
                if (a.StrokeID != b.StrokeID) continue;
                Vector2 start = new Vector2(canvas.x + a.X, canvas.yMax - a.Y);
                Vector2 end = new Vector2(canvas.x + b.X, canvas.yMax - b.Y);
                Matrix4x4 matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg, start);
                GUI.DrawTexture(new Rect(start.x, start.y - 1.5f, Vector2.Distance(start, end), 3), Texture2D.whiteTexture);
                GUI.matrix = matrix;
            }
        }

        private void HandleMouse(Rect canvas)
        {
            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && canvas.Contains(e.mousePosition))
            {
                _drawing = true;
                _stroke++;
                AddPoint(canvas, e.mousePosition);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && _drawing)
            {
                if (canvas.Contains(e.mousePosition)) AddPoint(canvas, e.mousePosition);
                else _drawing = false;
                e.Use();
            }
            else if (e.rawType == EventType.MouseUp && _drawing)
            {
                if (canvas.Contains(e.mousePosition)) AddPoint(canvas, e.mousePosition);
                _drawing = false;
            }
        }

        private void AddPoint(Rect canvas, Vector2 mouse)
        {
            _points.Add(new Point(mouse.x - canvas.x, canvas.yMax - mouse.y, _stroke));
            Invalidate();
        }

        private void Recognize()
        {
            try
            {
                float[] pixels = GesturePreprocessor.RasterizeTo28x28(_points, BrushRadius);
                if (_preview != null) Destroy(_preview);
                _preview = new Texture2D(28, 28, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                for (int y = 0; y < 28; y++)
                    for (int x = 0; x < 28; x++)
                    {
                        float v = pixels[y * 28 + x];
                        _preview.SetPixel(x, 27 - y, new Color(v, v, v, 1));
                    }
                _preview.Apply();
                using var input = new Tensor<float>(new TensorShape(1, 1, 28, 28), pixels);
                _worker.Schedule(input);
                using var output = _worker.PeekOutput().ReadbackAndClone() as Tensor<float>;
                float[] logits = output.DownloadToArray();
                if (logits.Length != SentisRecognizer.ClassToSymbol.Length)
                    throw new InvalidOperationException("Số lớp ONNX không khớp mapping.");
                float max = logits.Max();
                _probabilities = logits.Select(v => Mathf.Exp(v - max)).ToArray();
                float sum = _probabilities.Sum();
                for (int i = 0; i < logits.Length; i++) _probabilities[i] /= sum;
                int[] order = Enumerable.Range(0, logits.Length).OrderByDescending(i => _probabilities[i]).ToArray();
                int best = order[0];
                float margin = _probabilities[best] - _probabilities[order[1]];
                GestureSymbol symbol = SentisRecognizer.ClassToSymbol[best];
                var bounds = GesturePreprocessor.CalculateBounds(_points);
                string result;
                if (bounds.Diagonal < 35) result = "Từ chối: nét quá nhỏ.";
                else if (GestureCompletionPolicy.IsStraight(_points) && bounds.Height > bounds.Width * 3.5f)
                    result = "VerticalLine (quy tắc hình học, bỏ qua CNN).";
                else if (GestureCompletionPolicy.IsStraight(_points) && bounds.Width > bounds.Height * 3.5f)
                    result = "HorizontalLine (quy tắc hình học, bỏ qua CNN).";
                else if (symbol == GestureSymbol.Unknown || symbol == GestureSymbol.None) result = "Từ chối: Unknown.";
                else if (_probabilities[best] < .85f) result = "Từ chối: confidence thấp.";
                else if (margin < .30f) result = "Từ chối: margin thấp.";
                else if (!GestureCompletionPolicy.HasRequiredStructure(_points, symbol))
                    result = "Từ chối: E chưa đủ thân dọc và ba nét ngang.";
                else result = "Chấp nhận " + symbol;
                _message = $"CNN thô: {symbol} ({_probabilities[best]:P2}), margin: {margin:P2}\n" +
                    $"Sau bộ lọc test: {result}\nSố nét: {_points.Select(p => p.StrokeID).Distinct().Count()}";
            }
            catch (Exception ex) { _probabilities = null; _message = "Lỗi nhận diện: " + ex.Message; }
        }
    }
}
