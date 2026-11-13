using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using MagicTouch.UI;

namespace MagicTouch.Online
{
    /// <summary>
    /// Dựng màn hình online lúc chạy bằng cách nhân bản các thành phần pixel có sẵn trong menu
    /// (khung Tablet + ruy băng của Settings, nút Banner, nút xanh của Game Over, nhãn chữ),
    /// nên giao diện mới cùng phong cách mà không phải sửa SampleScene.
    /// </summary>
    internal sealed class OnlineUiKit
    {
        // Chữ sáng (có bóng đổ) cho tiêu đề ô; chữ tối (không bóng) cho thông tin phụ trên nền giấy vàng của khung Tablet.
        public static readonly Color Cream = new Color32(255, 240, 187, 255);
        public static readonly Color Ink = new Color32(62, 36, 22, 255);
        public static readonly Color Muted = new Color32(98, 62, 36, 255);
        public static readonly Color Good = new Color32(26, 112, 44, 255);
        public static readonly Color Bad = new Color32(168, 32, 28, 255);

        private readonly GameObject _panelTemplate;
        private readonly Button _bannerTemplate;
        private readonly Button _greenTemplate;
        private readonly TextMeshProUGUI _labelTemplate;
        private readonly Image _fieldLook;

        /// <summary>Font thường (có đủ ký tự, kể cả tiếng Việt, * và _) cho ô nhập và tên người chơi.</summary>
        public readonly TMP_FontAsset TextFont;

        private OnlineUiKit(GameObject panel, Button banner, Button green, TextMeshProUGUI label)
        {
            _panelTemplate = panel; _bannerTemplate = banner; _greenTemplate = green; _labelTemplate = label;
            _fieldLook = banner.image;
            TextFont = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset : label.font;
        }

        public static OnlineUiKit TryCreate(MenuManager menu, out string missing)
        {
            missing = null;
            var settings = menu.settingsPanel;
            var gameOver = menu.gameOverPanel;
            var settingsDesign = settings != null ? settings.transform.Find("SafeArea/Design") : null;
            var gameOverDesign = gameOver != null ? gameOver.transform.Find("SafeArea/Design") : null;
            var banner = settingsDesign != null ? settingsDesign.Find("BackButton")?.GetComponent<Button>() : null;
            var green = gameOverDesign != null ? gameOverDesign.Find("PlayAgainButton")?.GetComponent<Button>() : null;
            var label = settingsDesign != null ? settingsDesign.Find("MusicLabel")?.GetComponent<TextMeshProUGUI>() : null;
            bool hasFrame = settingsDesign != null && settingsDesign.Find("Tablet") != null && settingsDesign.Find("HeaderRibbon") != null;

            if (!hasFrame) missing = "Settings/SafeArea/Design/Tablet + HeaderRibbon";
            else if (banner == null) missing = "Settings/SafeArea/Design/BackButton";
            else if (green == null) missing = "GameOver/SafeArea/Design/PlayAgainButton";
            else if (label == null) missing = "Settings/SafeArea/Design/MusicLabel";
            return missing == null ? new OnlineUiKit(settings, banner, green ?? banner, label) : null;
        }

        // ------------------------------------------------------------ khung màn hình
        /// <summary>Tạo màn hình mới giống Settings: nền tối, khung Tablet, ruy băng tiêu đề. Trả về vùng thiết kế để đặt nội dung.</summary>
        public RectTransform MakeScreen(string name, string title, Vector2 designSize, float tabletHeight, float headerY, out GameObject root)
        {
            root = Object.Instantiate(_panelTemplate, _panelTemplate.transform.parent);
            root.name = name;
            root.SetActive(false);
            var design = (RectTransform)root.transform.Find("SafeArea/Design");

            var keep = new HashSet<string> { "Tablet", "HeaderRibbon" };
            for (int i = design.childCount - 1; i >= 0; i--)
            {
                var child = design.GetChild(i);
                if (keep.Contains(child.name)) continue;
                child.gameObject.SetActive(false);
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }

            var fit = design.GetComponent<ReferencePanelFit>();
            if (fit != null) { fit.designSize = designSize; fit.Refit(); }
            else design.sizeDelta = designSize;

            ((RectTransform)design.Find("Tablet")).sizeDelta = new Vector2(770, tabletHeight);
            var ribbon = (RectTransform)design.Find("HeaderRibbon");
            ribbon.anchoredPosition = new Vector2(0, headerY);
            var header = ribbon.GetComponentInChildren<TMP_Text>(true);
            if (header != null) header.text = title;
            return design;
        }

        // ------------------------------------------------------------ nút
        public Button BannerButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityAction action, float fontSize = 32)
            => CloneButton(_bannerTemplate, parent, name, label, pos, size, action, fontSize);

        public Button GreenButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityAction action, float fontSize = 36)
            => CloneButton(_greenTemplate, parent, name, label, pos, size, action, fontSize);

        private static Button CloneButton(Button template, Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityAction action, float fontSize)
        {
            var go = Object.Instantiate(template.gameObject, parent);
            go.name = name;
            go.SetActive(true);
            Place((RectTransform)go.transform, pos, size);
            var button = go.GetComponent<Button>();
            Retarget(button, label, action);
            var text = go.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text != null)
            {
                Place(text.rectTransform, Vector2.zero, size - new Vector2(44, 16));
                SetSize(text, fontSize);
            }
            return button;
        }

        /// <summary>Đổi chữ và hành động của một nút có sẵn (bỏ cả các sự kiện đã gán trong scene, chỉ khi đang chạy).</summary>
        public static void Retarget(Button button, string label, UnityAction action)
        {
            button.onClick = new Button.ButtonClickedEvent();
            if (action != null) button.onClick.AddListener(action);
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null && label != null) text.text = label;
        }

        // ------------------------------------------------------------ chữ
        /// <summary>Chữ font pixel (chỉ chữ Latin không dấu, số và . , : ; ! ? + - / ( ) &amp; % = ').</summary>
        public TextMeshProUGUI Label(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = Object.Instantiate(_labelTemplate.gameObject, parent);
            go.name = name;
            go.SetActive(true);
            var label = go.GetComponent<TextMeshProUGUI>();
            Place(label.rectTransform, pos, size);
            label.text = text;
            label.alignment = align;
            label.raycastTarget = false;
            label.lineSpacing = 24; // font pixel: các dòng sát nhau nếu để mặc định
            SetSize(label, fontSize);
            SetColor(label, color);
            return label;
        }

        /// <summary>Đổi màu chữ; chữ tối thì tắt bóng đổ đen (bóng đen dưới chữ tối trông bẩn).</summary>
        public static void SetColor(TMP_Text label, Color color)
        {
            label.color = color;
            var shadow = label.GetComponent<Shadow>();
            if (shadow != null) shadow.enabled = color.grayscale > .45f;
        }

        /// <summary>Chữ font thường, dùng cho tên người chơi (có thể có dấu tiếng Việt hoặc ký tự đặc biệt).</summary>
        public TextMeshProUGUI PlainText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = TextFont;
            label.fontStyle = FontStyles.Bold;
            label.text = text;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            SetSize(label, fontSize);
            Place(label.rectTransform, pos, size);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, .75f);
            shadow.effectDistance = new Vector2(3, -3);
            return label;
        }

        private static void SetSize(TMP_Text text, float size)
        {
            text.fontSize = size;
            text.fontSizeMax = size;
            text.fontSizeMin = Mathf.Max(8, size * .55f);
            text.enableAutoSizing = true;
        }

        // ------------------------------------------------------------ ô nhập
        public TMP_InputField Field(Transform parent, string name, Vector2 pos, Vector2 size, string placeholder, bool password, int limit,
            TMP_InputField.OnValidateInput validate = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.SetActive(false); // gán đủ thành phần rồi mới bật, tránh lỗi của TMP_InputField khi OnEnable
            go.transform.SetParent(parent, false);
            Place((RectTransform)go.transform, pos, size);

            var bg = go.AddComponent<Image>();
            bg.sprite = _fieldLook.sprite;
            bg.type = _fieldLook.type;
            bg.pixelsPerUnitMultiplier = _fieldLook.pixelsPerUnitMultiplier;
            bg.color = new Color32(64, 50, 98, 255);

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            area.transform.SetParent(go.transform, false);
            var areaRect = (RectTransform)area.transform;
            areaRect.anchorMin = Vector2.zero; areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = new Vector2(30, 8); areaRect.offsetMax = new Vector2(-30, -8);

            TextMeshProUGUI MakeText(string objName, Color color, FontStyles style)
            {
                var t = new GameObject(objName, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                t.transform.SetParent(area.transform, false);
                var r = t.rectTransform;
                r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                t.font = TextFont; t.fontSize = 34; t.color = color; t.fontStyle = style;
                t.alignment = TextAlignmentOptions.MidlineLeft;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                t.raycastTarget = false;
                return t;
            }

            var hint = MakeText("Placeholder", new Color(1f, .94f, .73f, .45f), FontStyles.Italic);
            hint.text = placeholder;
            var value = MakeText("Text", Cream, FontStyles.Bold);

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.textViewport = areaRect;
            input.textComponent = value;
            input.placeholder = hint;
            input.fontAsset = TextFont;
            input.pointSize = 34;
            input.characterLimit = limit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = password ? TMP_InputField.ContentType.Password : TMP_InputField.ContentType.Standard;
            input.asteriskChar = '*';
            input.caretColor = Cream;
            input.customCaretColor = true;
            input.caretWidth = 3;
            input.selectionColor = new Color(.55f, .8f, 1f, .45f);
            input.onFocusSelectAll = false;
            if (validate != null) input.onValidateInput = validate;
            var colors = input.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, .95f, .8f);
            colors.selectedColor = new Color(1f, .92f, .65f);
            input.colors = colors;

            go.SetActive(true);
            return input;
        }

        // ------------------------------------------------------------ khác
        public static Image Box(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Place(image.rectTransform, pos, size);
            return image;
        }

        public static void Place(RectTransform rect, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            rect.localScale = Vector3.one;
        }
    }
}
