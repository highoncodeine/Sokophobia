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
    [AddComponentMenu("Sokophobia/Victory Screen Controller")]
    [DefaultExecutionOrder(-60)]
    public class VictoryScreenController : MonoBehaviour
    {
        [SerializeField] private Texture2D victoryTexture;
        [SerializeField] private string mainMenuScene = "MainMenu";

        [Tooltip("Clickable region in normalized image coordinates, measured from the top-left.")]
        [SerializeField] private Rect playAgainRegion = new Rect(0.33f, 0.64f, 0.34f, 0.10f);
        [SerializeField] private Rect quitRegion = new Rect(0.44f, 0.75f, 0.12f, 0.08f);

        private PlayerHealth _health;
        private string _levelScene;
        private bool _isLoading;
        private bool _previousAudioPause;

        public bool IsVisible { get; private set; }

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _levelScene = gameObject.scene.name;
        }

        // Returns false when another end screen or pause already owns the game.
        public bool ShowVictory()
        {
            if (!isActiveAndEnabled || IsVisible || _health.IsDead || Time.timeScale <= 0f)
                return false;

            if (TryGetComponent<GameOverController>(out var gameOver) && gameOver.IsVisible)
                return false;

            if (victoryTexture == null)
            {
                Debug.LogError("[VictoryScreenController] Assign VictoryScreen.png in the Inspector.", this);
                return false;
            }

            if (EventSystem.current == null)
                new GameObject("Victory_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            Button playAgain = BuildCanvas();
            IsVisible = true;
            _previousAudioPause = AudioListener.pause;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (TryGetComponent<GridPlayerController>(out var movement))
            {
                movement.StopAllCoroutines();
                movement.enabled = false;
            }

            GameEvents.RaiseLevelWon();
            EventSystem.current.SetSelectedGameObject(playAgain.gameObject);
            return true;
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

        private Button BuildCanvas()
        {
            GameObject canvasObject = new GameObject("Victory_Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject artwork = new GameObject("VictoryArtwork", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = artwork.GetComponent<RectTransform>();
            rect.SetParent(canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            artwork.GetComponent<RawImage>().texture = victoryTexture;

            Button playAgain = CreateButton(canvasObject.transform, "PlayAgainHitbox", playAgainRegion, PlayAgain);
            CreateButton(canvasObject.transform, "QuitHitbox", quitRegion, QuitToMainMenu);
            return playAgain;
        }

        private static Button CreateButton(Transform parent, string objectName, Rect region, UnityAction onClick)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Button));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
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

        [ContextMenu("Testing/Show Victory Screen")]
        private void ShowVictoryForTesting()
        {
            if (Application.isPlaying && (Application.isEditor || Debug.isDebugBuild))
                ShowVictory();
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