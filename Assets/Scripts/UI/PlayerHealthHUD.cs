using UnityEngine;
using UnityEngine.UI;
using Sokophobia.Grid;

namespace Sokophobia.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    [AddComponentMenu("Sokophobia/Player Health HUD")]
    [DefaultExecutionOrder(-70)]
    public class PlayerHealthHUD : MonoBehaviour
    {
        [Header("Artwork")]
        [SerializeField] private Texture2D fullHealthTexture;
        [SerializeField] private Texture2D emptyHealthTexture;

        [Tooltip("Visible artwork bounds in the original PNG, measured from its bottom-left corner.")]
        [SerializeField] private Rect artworkPixels = new Rect(39f, 327f, 1862f, 410f);

        [Tooltip("Left and right edges of the ten-segment bar in the original PNG, excluding the heart.")]
        [SerializeField] private Vector2 segmentRangePixels = new Vector2(267f, 1877f);

        [Header("Layout (relative to 1920 x 1080)")]
        [SerializeField] private Vector2 bottomLeftMargin = new Vector2(24f, 24f);
        [SerializeField, Min(100f)] private float barWidth = 400f;

        private const int SegmentCount = 10;
        private PlayerHealth _health;
        private GameObject _canvasObject;
        private RectTransform _fullClip;
        private float _barHeight;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            ValidateLayout();

            if (fullHealthTexture == null || emptyHealthTexture == null)
            {
                Debug.LogError("[PlayerHealthHUD] Assign the full and empty health PNGs in the Inspector.", this);
                enabled = false;
                return;
            }

            BuildCanvas();
        }

        private void OnValidate()
        {
            ValidateLayout();
        }

        private void ValidateLayout()
        {
            if (artworkPixels.width <= 0f || artworkPixels.height <= 0f)
                artworkPixels = new Rect(39f, 327f, 1862f, 410f);

            barWidth = Mathf.Max(100f, barWidth);
        }

        private void OnEnable()
        {
            if (_health == null || _fullClip == null)
                return;

            _health.OnHealthChanged += HandleHealthChanged;
            _canvasObject.SetActive(true);
            HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
        }

        private void OnDisable()
        {
            if (_health != null)
                _health.OnHealthChanged -= HandleHealthChanged;

            if (_canvasObject != null)
                _canvasObject.SetActive(false);
        }

        private void HandleHealthChanged(int current, int maximum)
        {
            int visibleSegments = Mathf.Clamp(
                Mathf.CeilToInt((float)current / Mathf.Max(1, maximum) * SegmentCount),
                0, SegmentCount);

            _fullClip.gameObject.SetActive(visibleSegments > 0);
            if (visibleSegments == 0)
                return;

            float clipWidth = barWidth;
            if (visibleSegments < SegmentCount)
            {
                float cutX = Mathf.Lerp(segmentRangePixels.x, segmentRangePixels.y,
                    (float)visibleSegments / SegmentCount);
                clipWidth *= Mathf.Clamp01((cutX - artworkPixels.x) / artworkPixels.width);
            }

            _fullClip.sizeDelta = new Vector2(clipWidth, _barHeight);
        }

        private void BuildCanvas()
        {
            _canvasObject = new GameObject("PlayerHealth_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _canvasObject.transform.SetParent(transform, false);
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _barHeight = barWidth * artworkPixels.height / artworkPixels.width;
            RectTransform root = CreateRect("HealthBar", _canvasObject.transform);
            root.anchoredPosition = bottomLeftMargin;
            root.sizeDelta = new Vector2(barWidth, _barHeight);

            CreateImage("EmptyHealth", root, emptyHealthTexture);
            _fullClip = CreateRect("FullHealthClip", root);
            _fullClip.sizeDelta = new Vector2(barWidth, _barHeight);
            _fullClip.gameObject.AddComponent<RectMask2D>();
            CreateImage("FullHealth", _fullClip, fullHealthTexture);
        }

        private RawImage CreateImage(string objectName, Transform parent, Texture2D texture)
        {
            RectTransform rect = CreateRect(objectName, parent);
            rect.sizeDelta = new Vector2(barWidth, _barHeight);
            RawImage image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.uvRect = new Rect(artworkPixels.x / texture.width,
                artworkPixels.y / texture.height,
                artworkPixels.width / texture.width,
                artworkPixels.height / texture.height);
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(string objectName, Transform parent)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            return rect;
        }
    }
}