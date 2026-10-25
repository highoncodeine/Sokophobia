using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sokophobia.Grid;

namespace Sokophobia.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerHealth))]
    [AddComponentMenu("Sokophobia/Game Over Controller")]
    [DefaultExecutionOrder(-60)]
    public class GameOverController : MonoBehaviour
    {
        [SerializeField] private Texture2D gameOverTexture;
        [SerializeField] private string mainMenuScene = "MainMenu";

        [Tooltip("Clickable region in normalized image coordinates, measured from the top-left.")]
        [SerializeField] private Rect playAgainRegion = new Rect(0.33f, 0.54f, 0.34f, 0.10f);
        [SerializeField] private Rect quitRegion = new Rect(0.44f, 0.65f, 0.12f, 0.08f);

        private PlayerHealth _health;
        private GameObject _canvasObject;
        private Button _playAgainButton;
        private string _levelScene;
        private bool _isLoading;
        private bool _previousAudioPause;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _levelScene = gameObject.scene.name;
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerDied += ShowGameOver;
        }

        private void Start()
        {
            if (_health.IsDead)
                ShowGameOver();
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerDied -= ShowGameOver;
        }

        private void ShowGameOver()
        {
            if (IsVisible)
                return;

            if (TryGetComponent<VictoryScreenController>(out var victory) && victory.IsVisible)
                return;

            if (gameOverTexture == null)
            {
                Debug.LogError("[GameOverController] Assign GameOver.png in the Inspector.", this);
                return;
            }

            EnsureEventSystem();
            BuildCanvas();
            IsVisible = true;
            _previousAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            EventSystem.current.SetSelectedGameObject(_playAgainButton.gameObject);
        }

        public void PlayAgain()
        {
            LoadScene(_levelScene);
        }

        public void QuitToMainMenu()
        {
            LoadScene(mainMenuScene);
        }

        private void LoadScene(string sceneName)
        {
            if (!IsVisible || _isLoading)
                return;

            _isLoading = true;
            Time.timeScale = 1f;
            AudioListener.pause = _previousAudioPause;
            SceneManager.LoadScene(sceneName);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null)
                new GameObject("GameOver_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void BuildCanvas()
        {
            _canvasObject = new GameObject("GameOver_Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject artwork = new GameObject("GameOverArtwork", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = artwork.GetComponent<RectTransform>();
            rect.SetParent(_canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            artwork.GetComponent<RawImage>().texture = gameOverTexture;

            _playAgainButton = CreateButton("PlayAgainHitbox", playAgainRegion, PlayAgain);
            CreateButton("QuitHitbox", quitRegion, QuitToMainMenu);
        }

        private Button CreateButton(string objectName, Rect region, UnityAction onClick)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(_canvasObject.transform, false);
            rect.anchorMin = new Vector2(region.xMin, 1f - region.yMax);
            rect.anchorMax = new Vector2(region.xMax, 1f - region.yMin);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = go.GetComponent<Image>();
            image.color = Color.clear;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            return button;
        }

        private void OnDestroy()
        {
            if (IsVisible)
            {
                Time.timeScale = 1f;
                AudioListener.pause = _previousAudioPause;
            }
        }
    }
}