using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sokophobia.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sokophobia/Debug/UI Hitbox Debugger")]
    public class UIHitboxDebugger : MonoBehaviour
    {
        [Tooltip("Show colored button rectangles in the Game view. F7 toggles this while playing.")]
        [SerializeField] private bool showHitboxes;
        [SerializeField] private Key toggleKey = Key.F7;
        [SerializeField] private Color normalColor = new Color(0f, 1f, 0.4f, 1f);
        [SerializeField] private Color hoverColor = new Color(1f, 0.85f, 0f, 1f);
        [SerializeField] private Color disabledColor = new Color(1f, 0.25f, 0.25f, 1f);

        private static UIHitboxDebugger _instance;
        private Button[] _buttons;
        private GUIStyle _labelStyle;
        private GUIStyle _statusStyle;
        private readonly Vector3[] _corners = new Vector3[4];
        private readonly List<RaycastResult> _raycastResults = new List<RaycastResult>();
        private PointerEventData _pointerData;
        private EventSystem _pointerEventSystem;
        private string _underPointer = "No UI";
        private string _lastClick = "None";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeOverlay()
        {
            if (_instance != null)
                return;

            GameObject host = new GameObject("UIHitboxDebugger");
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<UIHitboxDebugger>();
        }

        [ContextMenu("Refresh Button List")]
        private void RefreshButtons()
        {
            _buttons = FindObjectsByType<Button>(FindObjectsInactive.Exclude);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame)
                showHitboxes = !showHitboxes;
        }

        private void LateUpdate()
        {
            if (!showHitboxes)
                return;

            RefreshButtons();
            EventSystem eventSystem = EventSystem.current;
            Mouse mouse = Mouse.current;
            _underPointer = "No UI";
            if (eventSystem != null && mouse != null)
            {
                if (_pointerData == null || _pointerEventSystem != eventSystem)
                {
                    _pointerData = new PointerEventData(eventSystem);
                    _pointerEventSystem = eventSystem;
                }

                _pointerData.Reset();
                _pointerData.position = mouse.position.ReadValue();
                _raycastResults.Clear();
                eventSystem.RaycastAll(_pointerData, _raycastResults);
                if (_raycastResults.Count > 0)
                    _underPointer = GetHierarchyPath(_raycastResults[0].gameObject.transform);
            }
            else if (eventSystem == null)
            {
                _underPointer = "NO EVENT SYSTEM (UI buttons cannot receive clicks)";
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                _lastClick = _underPointer;
        }

        private void OnGUI()
        {
            if (!showHitboxes || _buttons == null || Event.current.type != EventType.Repaint)
                return;

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _labelStyle.normal.textColor = Color.white;
                _statusStyle = new GUIStyle(_labelStyle) { fontSize = 13, wordWrap = true };
            }

            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            int previousDepth = GUI.depth;
            GUI.matrix = Matrix4x4.identity;
            GUI.depth = -10000;

            Vector2 mousePosition = Mouse.current != null
                ? Mouse.current.position.ReadValue() : new Vector2(-1f, -1f);

            foreach (Button button in _buttons)
            {
                if (button == null || !button.gameObject.activeInHierarchy)
                    continue;

                RectTransform rectTransform = button.transform as RectTransform;
                if (rectTransform == null)
                    continue;

                Canvas canvas = button.GetComponentInParent<Canvas>();
                if (canvas == null || !canvas.isActiveAndEnabled)
                    continue;
                canvas = canvas.rootCanvas;
                if (!canvas.isActiveAndEnabled)
                    continue;
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

                rectTransform.GetWorldCorners(_corners);
                Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                foreach (Vector3 corner in _corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    minimum = Vector2.Min(minimum, point);
                    maximum = Vector2.Max(maximum, point);
                }

                Rect screenRect = Rect.MinMaxRect(minimum.x, Screen.height - maximum.y,
                    maximum.x, Screen.height - minimum.y);
                bool interactable = button.isActiveAndEnabled && button.IsInteractable();
                bool hovering = RectTransformUtility.RectangleContainsScreenPoint(rectTransform, mousePosition, camera);
                Color color = !interactable ? disabledColor : hovering ? hoverColor : normalColor;
                DrawRectangle(screenRect, color);

                string action = button.onClick.GetPersistentEventCount() > 0
                    ? button.onClick.GetPersistentMethodName(0) : button.name;
                string label = $"{canvas.name}: {action}  {screenRect.width:0} x {screenRect.height:0}px"
                    + (interactable ? "" : " [DISABLED]");
                Rect labelRect = new Rect(screenRect.xMin, Mathf.Max(0f, screenRect.yMin - 26f),
                    Mathf.Max(360f, screenRect.width), 24f);
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                GUI.DrawTexture(labelRect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(labelRect, label, _labelStyle);
            }

            Rect statusRect = new Rect(8f, 8f, Mathf.Max(1f, Mathf.Min(850f, Screen.width - 16f)), 110f);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(statusRect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(statusRect, $"F7: toggle hitboxes | Screen: {Screen.width} x {Screen.height}\n"
                + "Green: UI button | Yellow: pointer inside | Red: disabled\n"
                + $"Top UI under mouse: {_underPointer}\n"
                + $"Last left-click target: {_lastClick}", _statusStyle);

            GUI.color = previousColor;
            GUI.matrix = previousMatrix;
            GUI.depth = previousDepth;
        }

        private static void DrawRectangle(Rect rect, Color color)
        {
            const float thickness = 2f;
            GUI.color = new Color(color.r, color.g, color.b, 0.2f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), Texture2D.whiteTexture);
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }
            return path;
        }
    }
}