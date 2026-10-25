using UnityEngine;
using UnityEngine.UI;
using Sokophobia.Grid;

namespace Sokophobia.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Sokophobia/Door Opening Popup")]
    [DefaultExecutionOrder(-50)]
    public class DoorOpeningPopup : MonoBehaviour
    {
        [SerializeField] private Texture2D popupTexture;
        [Tooltip("How many real-time seconds the announcement stays on screen while gameplay is frozen.")]
        [SerializeField, Min(0.1f)] private float displayDuration = 2f;
        [SerializeField] private AudioSource sfxSource;

        private GameObject _canvasObject;
        private bool _pending;
        private bool _hasShown;
        private bool _levelEnded;
        private float _remainingTime;
        private float _previousTimeScale;

        public bool IsVisible { get; private set; }

        private void OnEnable()
        {
            GameEvents.OnPuzzleDoorOpened += HandleDoorOpened;
            GameEvents.OnPlayerDied += HandleEndScreen;
            GameEvents.OnLevelWon += HandleEndScreen;
        }

        private void OnDisable()
        {
            GameEvents.OnPuzzleDoorOpened -= HandleDoorOpened;
            GameEvents.OnPlayerDied -= HandleEndScreen;
            GameEvents.OnLevelWon -= HandleEndScreen;
            _pending = false;
            Dismiss();
        }

        private void HandleDoorOpened()
        {
            if (!_hasShown && !_levelEnded)
                _pending = true;
        }

        private void Update()
        {
            if (IsVisible)
            {
                _remainingTime -= Time.unscaledDeltaTime;
                if (_remainingTime <= 0f)
                    Dismiss();
            }
            else if (_pending && !_levelEnded && Time.timeScale > 0f)
            {
                ShowPopup();
                
            }
        }

        private void ShowPopup()
        {
            sfxSource.Play();
            _pending = false;
            _hasShown = true;
            if (popupTexture == null)
            {
                Debug.LogError("[DoorOpeningPopup] Assign DoorOpening.png in the Inspector.", this);
                return;
            }

            if (_canvasObject == null)
                BuildCanvas();

            _canvasObject.SetActive(true);
            _previousTimeScale = Time.timeScale;
            _remainingTime = Mathf.Max(0.1f, displayDuration);
            IsVisible = true;
            Time.timeScale = 0f;
            GameEvents.RaiseGameplayPopupChanged(true);
        }

        public void Dismiss()
        {
            HidePopup(true);
        }

        private void HandleEndScreen()
        {
            _levelEnded = true;
            _pending = false;
            HidePopup(false);
        }

        private void HidePopup(bool resumeGameplay)
        {
            if (!IsVisible)
                return;

            IsVisible = false;
            if (_canvasObject != null)
                _canvasObject.SetActive(false);

            if (resumeGameplay && !_levelEnded && Time.timeScale == 0f)
                Time.timeScale = _previousTimeScale;

            GameEvents.RaiseGameplayPopupChanged(false);
        }

        private void BuildCanvas()
        {
            _canvasObject = new GameObject("DoorOpening_Canvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObject.transform.SetParent(transform, false);
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;

            CanvasScaler scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject artwork = new GameObject("DoorOpeningArtwork", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(RawImage));
            RectTransform rect = artwork.GetComponent<RectTransform>();
            rect.SetParent(_canvasObject.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            RawImage image = artwork.GetComponent<RawImage>();
            image.texture = popupTexture;
            image.raycastTarget = true;
        }
    }
}