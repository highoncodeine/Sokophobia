using UnityEngine;
using UnityEngine.UI;
using Sokophobia.Enemy;

namespace Sokophobia.UI
{
    [AddComponentMenu("Sokophobia/Crosshair HUD")]
    [DisallowMultipleComponent]
    public class CrosshairHUD : MonoBehaviour
    {
        private GameObject _canvasObject;
        private Image _centerDot;
        private Image _tickTop, _tickBottom, _tickLeft, _tickRight;

        private FlashlightStunner _flashlight; // Optional, for reactive color

        private readonly Color _normalColor = new Color(1f, 1f, 1f, 0.7f);
        private readonly Color _targetColor = new Color(0f, 1f, 1f, 1f); // Cyan when aiming at bot

        private void Awake()
        {
            BuildCanvas();
        }

        private void Start()
        {
            // Auto-find the stunner if it exists in the scene
            _flashlight = FindAnyObjectByType<FlashlightStunner>();
        }

        private void Update()
        {
            // Hide when paused, game over, or popup is open (TimeScale becomes 0)
            bool gameplayActive = Time.timeScale > 0f;
            _canvasObject.SetActive(gameplayActive);

            if (!gameplayActive) return;

            // Color logic: React to flashlight target
            bool isTargeted = _flashlight != null && _flashlight.Charge01 > 0.01f; 
            // (You can change this later to check _flashlight.BeamOnTargetNow if OpenCode implemented it)

            Color c = isTargeted ? _targetColor : _normalColor;
            _centerDot.color = c;
            _tickTop.color = c;
            _tickBottom.color = c;
            _tickLeft.color = c;
            _tickRight.color = c;
        }

        private void BuildCanvas()
        {
            // 1. Create the Canvas
            _canvasObject = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler));
            _canvasObject.transform.SetParent(transform, false);
            
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Setting to 90 keeps it below EnemyAIHUD (100) but above PauseMenu (0)
            canvas.sortingOrder = 90; 

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // 2. Create the pieces
            _centerDot = CreateImage("Dot", new Vector2(4f, 4f), _canvasObject.transform, _normalColor);
            
            // 4 ticks around the center
            _tickTop    = CreateImage("TickTop", new Vector2(2f, 8f), _canvasObject.transform, _normalColor, new Vector2(0f, 10f));
            _tickBottom = CreateImage("TickBottom", new Vector2(2f, 8f), _canvasObject.transform, _normalColor, new Vector2(0f, -10f));
            _tickLeft   = CreateImage("TickLeft", new Vector2(8f, 2f), _canvasObject.transform, _normalColor, new Vector2(-10f, 0f));
            _tickRight  = CreateImage("TickRight", new Vector2(8f, 2f), _canvasObject.transform, _normalColor, new Vector2(10f, 0f));
        }

        private Image CreateImage(string name, Vector2 size, Transform parent, Color color, Vector2 offset = default)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);

            RectTransform rt = obj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;

            Image img = obj.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false; // Crucial: Don't block clicks

            return img;
        }
    }
}