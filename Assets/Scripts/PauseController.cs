using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Sokophobia.Grid;

public class PauseController : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;

    [Tooltip("Clickable regions in normalized image coordinates, measured from the top-left.")]
    [SerializeField] private Rect resumeRegion = new Rect(0.38f, 0.39f, 0.24f, 0.085f);
    [SerializeField] private Rect quitRegion = new Rect(0.44f, 0.49f, 0.12f, 0.08f);

    private bool isPaused;
    private bool isGameOver;
    private bool isPopupVisible;

    private Button resumeButton;

    private void OnEnable()
    {
        GameEvents.OnPlayerDied += HandleGameOver;
        GameEvents.OnLevelWon += HandleGameOver;
        GameEvents.OnGameplayPopupChanged += HandlePopupChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= HandleGameOver;
        GameEvents.OnLevelWon -= HandleGameOver;
        GameEvents.OnGameplayPopupChanged -= HandlePopupChanged;
    }

    private void HandlePopupChanged(bool visible)
    {
        isPopupVisible = visible;

        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = visible;
    }

    private void HandleGameOver()
    {
        isGameOver = true;
        isPaused = false;
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
            
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Start()
    {
        CreateRuntimeHitboxes();
        if (isGameOver || isPopupVisible)
            return;

        Time.timeScale = 1f;
        SetPaused(false);
    }

    private void Update()
    {
        if (isGameOver || isPopupVisible)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetPaused(!isPaused);
            return;
        }
    }

    private void CreateRuntimeHitboxes()
    {
        if (pauseMenu == null) return;

        resumeButton = CreateHitbox("ResumeHitboxRuntime", resumeRegion, ResumeGame);
        CreateHitbox("QuitHitboxRuntime", quitRegion, QuitToMainMenu);
    }

    private Button CreateHitbox(string objectName, Rect region, UnityEngine.Events.UnityAction onClick)
    {
        Transform existing = pauseMenu.transform.Find(objectName);
        GameObject hitboxObject = existing != null ? existing.gameObject :
            new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        hitboxObject.transform.SetParent(pauseMenu.transform, false);

        RectTransform hitboxRect = hitboxObject.GetComponent<RectTransform>();
        hitboxRect.anchorMin = new Vector2(region.xMin, 1f - region.yMax);
        hitboxRect.anchorMax = new Vector2(region.xMax, 1f - region.yMin);
        hitboxRect.offsetMin = Vector2.zero;
        hitboxRect.offsetMax = Vector2.zero;

        Image hitboxImage = hitboxObject.GetComponent<Image>();
        hitboxImage.color = Color.clear;
        hitboxImage.raycastTarget = true;

        Button hitboxButton = hitboxObject.GetComponent<Button>();
        hitboxButton.targetGraphic = hitboxImage;
        hitboxButton.transition = Selectable.Transition.None;
        hitboxButton.onClick.RemoveListener(onClick);
        hitboxButton.onClick.AddListener(onClick);
        return hitboxButton;
    }

    public void ResumeGame()
    {
        SetPaused(false);
    }

    public void QuitToMainMenu()
    {
        if (isGameOver || isPopupVisible) return;

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void SetPaused(bool paused)
    {
        if (isGameOver || isPopupVisible) return;

        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;

        if (pauseMenu != null)
        {
            pauseMenu.SetActive(paused);
        }

        if (paused && resumeButton != null)
        {
            if (EventSystem.current == null)
                new GameObject("Pause_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}