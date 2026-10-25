using UnityEngine;
using UnityEngine.UI;
using Sokophobia.Interaction;

namespace Sokophobia.UI
{
    [AddComponentMenu("Sokophobia/Crosshair HUD")]
    [DisallowMultipleComponent]
    public class CrosshairHUD : MonoBehaviour
    {
        private GameObject _canvasObject;
        private Image _centerDot;
        private Image[] _ticks;
        private Text _promptText;
        private Text _batteryText;
        
        private PlayerInteractor _interactor;
        private FlashlightBattery _battery;

        private readonly Color _normalColor = new Color(1f, 1f, 1f, 0.7f);
        private readonly Color _targetColor = new Color(1f, 0.8f, 0f, 1f);

        private void Awake()
        {
            BuildCanvas();
        }

        private void Start()
        {
            _interactor = GetComponent<PlayerInteractor>();
            _battery = GetComponent<FlashlightBattery>();
        }

        private void Update()
        {
            bool gameplayActive = Time.timeScale > 0f;
            _canvasObject.SetActive(gameplayActive);
            if (!gameplayActive) return;

            if (_interactor != null)
            {
                Color c = _interactor.HasTarget ? _targetColor : _normalColor;
                
                _centerDot.color = c;
                foreach (var tick in _ticks) tick.color = c;

                _promptText.text = _interactor.HasTarget ? $"[E] {_interactor.CurrentPrompt}" : "";
            }

            if (_battery != null && _batteryText != null)
            {
                int percent = Mathf.RoundToInt(_battery.Charge01 * 100f);
                _batteryText.text = $"BATTERY: {percent}%";
                _batteryText.color = percent < 20 ? Color.red : Color.white;
            }
        }

        private void BuildCanvas()
        {
            _canvasObject = new GameObject("CrosshairCanvas", typeof(Canvas), typeof(CanvasScaler));
            _canvasObject.transform.SetParent(transform, false);
            
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            _centerDot = CreateImage("Dot", new Vector2(4f, 4f), _canvasObject.transform, _normalColor);
            
            _ticks = new Image[4];
            _ticks[0] = CreateImage("TickTop", new Vector2(2f, 8f), _canvasObject.transform, _normalColor, new Vector2(0f, 10f));
            _ticks[1] = CreateImage("TickBottom", new Vector2(2f, 8f), _canvasObject.transform, _normalColor, new Vector2(0f, -10f));
            _ticks[2] = CreateImage("TickLeft", new Vector2(8f, 2f), _canvasObject.transform, _normalColor, new Vector2(-10f, 0f));
            _ticks[3] = CreateImage("TickRight", new Vector2(8f, 2f), _canvasObject.transform, _normalColor, new Vector2(10f, 0f));

            GameObject textObj = new GameObject("PromptText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObj.transform.SetParent(_canvasObject.transform, false);
            RectTransform rt = textObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -40f);
            rt.sizeDelta = new Vector2(400f, 50f);
            
            _promptText = textObj.GetComponent<Text>();
            _promptText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _promptText.alignment = TextAnchor.MiddleCenter;
            _promptText.color = Color.white;
            _promptText.fontSize = 18;

            GameObject batObj = new GameObject("BatteryText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            batObj.transform.SetParent(_canvasObject.transform, false);
            RectTransform brt = batObj.GetComponent<RectTransform>();

            brt.anchorMin = new Vector2(1f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(1f, 0f);
            brt.anchoredPosition = new Vector2(-30f, 30f);
            brt.sizeDelta = new Vector2(200f, 40f);

            _batteryText = batObj.GetComponent<Text>();
            _batteryText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _batteryText.alignment = TextAnchor.LowerRight;
            _batteryText.color = Color.white;
            _batteryText.fontSize = 20;
            _batteryText.fontStyle = FontStyle.Bold;
            _batteryText.text = "BATTERY: --";
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
            img.raycastTarget = false;

            return img;
        }
    }
}