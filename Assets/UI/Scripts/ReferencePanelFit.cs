using UnityEngine;

namespace MagicTouch.UI
{
    /// <summary>Fits an authored pixel layout uniformly inside its parent at any aspect ratio.</summary>
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class ReferencePanelFit : MonoBehaviour
    {
        public Vector2 designSize = new Vector2(940f, 1240f);
        public float margin = 32f;
        private Vector2 _lastSize;
        private Vector2 _lastDesign;
        private float _lastMargin = -1f;

        private void OnEnable() { Refit(); }
        public void Refit() { _lastSize = new Vector2(-1f, -1f); Fit(); }
        private void LateUpdate() { Fit(); }
        private void Fit()
        {
            var rect = (RectTransform)transform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            Vector2 size = parent.rect.size;
            if (size == _lastSize && designSize == _lastDesign && margin == _lastMargin) return;
            _lastSize = size; _lastDesign = designSize; _lastMargin = margin;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = designSize;
            float scale = Mathf.Max(.01f, Mathf.Min((size.x - margin * 2f) / designSize.x,
                (size.y - margin * 2f) / designSize.y));
            rect.localScale = Vector3.one * scale;
        }
    }
}
