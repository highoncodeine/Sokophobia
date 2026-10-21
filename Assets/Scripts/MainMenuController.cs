using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private Texture2D controlsTexture;

    private GameObject _controlsCanvas;
    private EventSystem _menuEventSystem;
    private bool _previousNavigationEvents;
    private bool _showingControls;
    private bool _isLoading;
    private int _controlsShownFrame;

    private void Start()
    {
        if (bgmSource != null && !bgmSource.isPlaying)
        {
            bgmSource.Play();
        }
    }
    
    public void PlayGame()
    {
        if (_showingControls || _isLoading)
            return;

        if (controlsTexture == null)
        {
            Debug.LogError("[MainMenuController] Assign Controls.png in the Inspector.", this);
            return;
        }

        if (_controlsCanvas == null)
            BuildControlsCanvas();

        _menuEventSystem = EventSystem.current;
        if (_menuEventSystem != null)
        {
            _previousNavigationEvents = _menuEventSystem.sendNavigationEvents;
            _menuEventSystem.SetSelectedGameObject(null);
            _menuEventSystem.sendNavigationEvents = false;
        }

        _controlsCanvas.SetActive(true);
        _showingControls = true;
        _controlsShownFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (!_showingControls || _isLoading || Time.frameCount <= _controlsShownFrame)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            ContinueToGame();
    }

    private void ContinueToGame()
    {
        _isLoading = true;
        RestoreMenuNavigation();
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene("SampleScene");
    }

    public void QuitGame()
    {
        if (_showingControls || _isLoading)
            return;

        Application.Quit();
    }

    private void BuildControlsCanvas()
    {
        _controlsCanvas = new GameObject("Controls_Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _controlsCanvas.transform.SetParent(transform, false);
        Canvas canvas = _controlsCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;

        CanvasScaler scaler = _controlsCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform background = CreateFullscreenRect("ControlsBackground");
        background.gameObject.AddComponent<Image>().color = Color.black;

        RectTransform artwork = CreateFullscreenRect("ControlsArtwork");
        RawImage image = artwork.gameObject.AddComponent<RawImage>();
        image.texture = controlsTexture;
        image.raycastTarget = true;
    }

    private RectTransform CreateFullscreenRect(string objectName)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(_controlsCanvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private void RestoreMenuNavigation()
    {
        if (_menuEventSystem != null)
            _menuEventSystem.sendNavigationEvents = _previousNavigationEvents;

        _menuEventSystem = null;
    }

    private void OnDisable()
    {
        RestoreMenuNavigation();
        _showingControls = false;
        if (_controlsCanvas != null)
            _controlsCanvas.SetActive(false);
    }
}
