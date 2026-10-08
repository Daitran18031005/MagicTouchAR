using UnityEngine;

namespace MagicTouch.UI
{
    /// <summary>Keeps interactive content inside device cutouts without cropping the backdrop.</summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class ReferenceSafeArea : MonoBehaviour
    {
        private Rect _last;
        private Vector2Int _screen;
        private void OnEnable() { ApplyNow(); }
        public void ApplyNow() { _screen = Vector2Int.zero; Apply(); }
        private void Update() { Apply(); }
        private void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (safe == _last && size == _screen) return;
            _last = safe; _screen = size;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / size.x, safe.yMin / size.y);
            rect.anchorMax = new Vector2(safe.xMax / size.x, safe.yMax / size.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
